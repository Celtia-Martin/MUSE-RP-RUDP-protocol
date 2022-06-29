using Muse_RP.Exceptions;
using Muse_RP.Hosts;
using Muse_RP.Message;
using Muse_RP.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Muse_RP.Channels
{
    public abstract class ChannelHandler : IDisposable
    {
        //References

        protected Connection myConnection;
        protected Host myHost;

        //Properties

        protected uint windowSize;
        protected int timerTime;
        protected int waitTime;

        //State
        protected bool timerStopped;
        protected bool channelActivated;
        protected System.Timers.Timer messageTimer;
        protected Thread sendingThread;
        protected uint currentSequenceNumber;
        protected uint currentRecognised;
        protected uint currentAck;
        protected int currentDuplicate;

        //Connection Info

        protected int theirPort;
        protected string ipAddress;
        protected Socket udpSocket;
        protected EndPoint endPoint;
        protected IPAddress address;
        protected ConnectionInfo connectionInfo;

        //Mutex

        protected Mutex currentAckMutex;
        protected Mutex currentSequenceMutex;
        protected Mutex timeoutThreadMutex;
        protected Mutex senderBufferMutex;
        protected Mutex sendedBufferMutex;
        protected Mutex currentRecognisedMutex;

        //Buffers

        protected SortedMuseBuffer<ReceivedData> senderBuffer;
        protected SortedMuseBuffer<ReceivedData> sendedBuffer;
        protected SortedMuseBuffer<MessageObject> receiverBuffer;
        protected MuseBuffer<SenderMessageData> waitingMessageBuffer;


        #region Constructor
        protected ChannelHandler(Host myHost, uint windowSize, uint currentSequenceNumber, uint currentAck, int waitTime, int timerTime, EndPoint endPoint, ConnectionInfo connectionInfo)
        {
            this.windowSize = windowSize;
            this.currentSequenceNumber = currentSequenceNumber;
            this.currentRecognised = currentSequenceNumber;
            this.currentAck = currentAck;
            this.waitTime = waitTime;
            this.timerTime = timerTime;
            this.myHost = myHost;
            this.endPoint = endPoint;
            this.connectionInfo = connectionInfo;

            timerStopped = true;
            channelActivated = false;
            currentDuplicate = 0;

            sendedBuffer = new SortedMuseBuffer<ReceivedData>(new MessageDataComparer(windowSize));
            senderBuffer = new SortedMuseBuffer<ReceivedData>(new MessageDataComparer(windowSize));
            waitingMessageBuffer = new MuseBuffer<SenderMessageData>();
            receiverBuffer = new SortedMuseBuffer<MessageObject>(new MessageObjectComparer(windowSize));

            currentAckMutex = new Mutex(false);
            currentSequenceMutex = new Mutex(false);
            timeoutThreadMutex = new Mutex(false);
            sendedBufferMutex = new Mutex(false);
            senderBufferMutex = new Mutex(false);
            currentRecognisedMutex = new Mutex(false);
        }
        #endregion
        #region Abstract Methods
        /// <summary>
        /// Process the message received and send it to upper layer if it meets the requirements 
        /// </summary>
        /// <param name="message">The message to process</param>
        public abstract void Receive(MessageObject message);

        #endregion
        #region Public Methods

        public void SetConnectionInfo(ConnectionInfo connectionInfo)
        {
            this.connectionInfo = connectionInfo; ;
        }

        public void SetTimerTime(int milliseconds)
        {

            timerTime = milliseconds;
            timeoutThreadMutex.WaitOne();
            messageTimer.Interval = timerTime;
            timeoutThreadMutex.ReleaseMutex();
        }
        public bool IsActivated() { return channelActivated; }
        public ConnectionInfo getConnectionInfo() { return connectionInfo; }
        public Connection getConnection() { return myConnection; }

        /// <summary>
        /// The channel starts listening
        /// </summary>
        /// <param name="udpSocket">The socket binded to the channel</param>
        /// <param name="myConnection">The connection that manages the channel</param>
        public void Start(Socket udpSocket, Connection myConnection)
        {
            try
            {
                this.myConnection = myConnection;
                this.udpSocket = udpSocket;
                channelActivated = true;
                sendingThread = new Thread(() => SendingThread());
                sendingThread.Start();
                timeoutThreadMutex.WaitOne();
                messageTimer = new System.Timers.Timer(timerTime);
                messageTimer.Elapsed += (s, e) => TimerFinished(s, e);
                messageTimer.AutoReset = false;
                timeoutThreadMutex.ReleaseMutex();

            }
            catch (Exception e)
            {
                Console.WriteLine("Excepcion starting channelhandler: " + e.Message);
            }
            Console.WriteLine("Channel started");


        }

        /// <summary>
        /// Stop the channel, it stops listening and sending.
        /// </summary>
        public void Stop()
        {
            channelActivated = false;
            sendingThread?.Interrupt();
            StopTimer();
            senderBuffer.Clear();
            sendedBuffer.Clear();
            receiverBuffer.Clear();

        }

        /// <summary>
        /// Adds the data to the message buffer
        /// </summary>
        /// <param name="type">Type of message</param>
        /// <param name="isEnd">If it ends the connection</param>
        /// <param name="isInit">If it begins the connection</param>
        /// <param name="data">Data to send</param>
        public void Send(ushort type, bool isEnd, bool isInit, byte[] data)
        {
            waitingMessageBuffer.Add(new SenderMessageData(data, type, false, false, isInit, isEnd));
        }
        /// <summary>
        /// Sends a ping message
        /// </summary>
        /// <param name="pingMessage"></param>
        public void SendPing(MessageObject pingMessage)
        {
            SendBytes(pingMessage.toByteArray(), 0, true);
        }

        /// <summary>
        /// Sends the actual ACK number
        /// </summary>
        public void SendAck()
        {
            currentAckMutex.WaitOne();
            currentSequenceMutex.WaitOne();

            Console.WriteLine("Sending ACK, Current ACK and LastRecognised " + currentAck + " " + currentRecognised);
            MessageObject newAck = new MessageObject(0, currentSequenceNumber, currentAck, true, false, false, false, null);

            currentSequenceMutex.ReleaseMutex();
            currentAckMutex.ReleaseMutex();

            SendBytes(newAck.toByteArray(), newAck.getSequenceNumber(), true);
        }

        /// <summary>
        /// Process the message and checks if it's end, ping, end or data
        /// </summary>
        /// <param name="message"></param>
        public void ProcessMessage(MessageObject message)
        {
            if (message.isEnd())
            {
                myHost.ReceiveEnd(message, myConnection.endPoint);


                return;
            }
            else if (message.isPing())
            {
                myHost.ReceivePing(message, myConnection.endPoint);

                return;
            }
            else if (message.isAck())
            {
                ReceiveACK(message.getAck());
                return;
            }
            else
            {

                Receive(message); 

                SendAck();
            }
        }

        /// <summary>
        /// Process the target ACK following the sliding window algorithm
        /// </summary>
        /// <param name="ack">The ACK number</param>
        public void ReceiveACK(uint ack)
        {
            Console.WriteLine("ACK received: " + ack);
            currentRecognisedMutex.WaitOne();
            if (ack < currentRecognised)
            {
                if (uint.MaxValue - currentRecognised <= windowSize)
                {
                    CorrectAckReceived(ack); 
                }
                currentRecognisedMutex.ReleaseMutex();
                return;
            }
            else if (ack > currentRecognised && ack - currentRecognised <= windowSize)
            {

                CorrectAckReceived(ack);

            }
            else if (ack == currentRecognised)
            {
                currentDuplicate++;
                if (currentDuplicate == 3)
                {
                    currentDuplicate = 0;
                    StopTimer();
                    ReSend();
                }
            }
            currentRecognisedMutex.ReleaseMutex();
        }

        #endregion
        #region Protected Methods

        /// <summary>
        /// Send a message if there is space in the sending window
        /// </summary>
        /// <param name="type">Type of data</param>
        /// <param name="isAck">True if it is an ACK message</param>
        /// <param name="isPing">True if it is a Ping  message</param>
        /// <param name="isInit">True if it is an Init message</param>
        /// <param name="isEnd">True if it is an End message</param>
        /// <param name="data">Message data</param>
        /// <returns></returns>
        protected bool Send(ushort type, bool isAck, bool isPing, bool isInit, bool isEnd, byte[] data)
        {
            MessageObject message;
            try
            {
                message = new MessageObject(type, 0, 0, isAck, isPing, isInit, isEnd, data); 

            }
            catch (OversizedMessageDataException OMDE)
            {
                Console.WriteLine("Exception sending:" + OMDE);

                return false;
            }
            currentSequenceMutex.WaitOne();

            if (!isAck)
            {
                currentSequenceNumber++;
            }

            uint sequenceNumber = currentSequenceNumber;

            currentSequenceMutex.ReleaseMutex();
            message.setSequenceNumber(sequenceNumber);
            currentAckMutex.WaitOne();
            message.setAck(currentAck);
            byte[] dataToSend = message.toByteArray();
            currentRecognisedMutex.WaitOne();

            if (sequenceNumber - currentRecognised <= windowSize)
            {
                currentRecognisedMutex.ReleaseMutex();
                currentAckMutex.ReleaseMutex();
                SendBytes(dataToSend, message.getSequenceNumber(), false);
                return true;
            }
            else
            {
                currentRecognisedMutex.ReleaseMutex();
                currentAckMutex.ReleaseMutex();

                senderBufferMutex.WaitOne();

                senderBuffer.Add(new ReceivedData(dataToSend, message.getSequenceNumber()));

                senderBufferMutex.ReleaseMutex();

                return false;
            }
        }

        /// <summary>
        /// Sends the bytes with the attached socket
        /// </summary>
        /// <param name="data">The message in bytes</param>
        /// <param name="sequence">The number of sequence of the message</param>
        /// <param name="isAckOrPing">True if it's an ACK or Ping message</param>
        protected void SendBytes(byte[] data, uint sequence, bool isAckOrPing)
        {
            if (data == null)
            {
                return;
            }
            try
            {
                udpSocket.SendTo(data, endPoint);

                if (!isAckOrPing)
                {
                    StartTimer();
                    sendedBufferMutex.WaitOne();

                    sendedBuffer.Add(new ReceivedData(data, sequence));

                    sendedBufferMutex.ReleaseMutex();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception sending data: " + e.Message);
            }


        }

        /// <summary>
        /// Resends the oldest message waiting validation
        /// </summary>
        protected void ReSend()
        {

            sendedBufferMutex.WaitOne();
            ReceivedData data = sendedBuffer.Dequeue();
            if (data == null)
            {
                sendedBufferMutex.ReleaseMutex();
                return;
            }

            SendBytes(data.data, data.sequence, false);

            sendedBufferMutex.ReleaseMutex();

            Console.WriteLine("Resend " + data.sequence + "; Current ACK and LastRecognised " + currentAck + " " + currentRecognised);


        }
        /// <summary>
        /// Sends the message to the Application layer
        /// </summary>
        /// <param name="message"></param>
        /// <param name="source">The message's emisor connection info </param>
        protected void ToUpperLayer(MessageObject message, Connection source)
        {
            myHost.Receive(message, source);

        }
        /// <summary>
        /// Sends the message to the Application layer and checks if there are other messages waiting validation
        /// </summary>
        /// <param name="message"></param>
        protected void SuccessMessage(MessageObject message)
        {
            ToUpperLayer(message, myConnection);
            Console.WriteLine("Success: " + message.getSequenceNumber());

            if (receiverBuffer.getSize() > 0)
            {
                Receive(receiverBuffer.Dequeue());
            }

        }
        #endregion
        #region Threads
        /// <summary>
        /// Sending that sends messages of the waiting messages buffer
        /// </summary>
        protected void SendingThread()
        {
            try
            {
                SenderMessageData toSend;
                while (channelActivated)
                {

                    while (waitingMessageBuffer.getSize() > 0)
                    {
                        toSend = waitingMessageBuffer.Dequeue();
                        Send(toSend.type, toSend.isAck, toSend.isPing, toSend.isInit, toSend.isEnd, toSend.data);
                    }
                    Thread.Sleep(waitTime);
                }
            }
            catch (ThreadInterruptedException e)
            {
                //Channel closed
            }
            catch(Exception e)
            {
                Console.WriteLine("Channel stopped because there was an exception: " + e.Message);
                Stop();
            }


        }

        /// <summary>
        /// Stops the message timer
        /// </summary>
        protected void StopTimer()
        {

            timeoutThreadMutex.WaitOne();
            messageTimer.Stop();
            timerStopped = true;
            timeoutThreadMutex.ReleaseMutex();

        }
        /// <summary>
        /// Starts the message timer
        /// </summary>
        protected void StartTimer()
        {

            timeoutThreadMutex.WaitOne();

            if (!timerStopped)
            {
                timeoutThreadMutex.ReleaseMutex();
                return;
            }
            messageTimer.Stop();
            messageTimer.Start();
            timerStopped = false;
            timeoutThreadMutex.ReleaseMutex();
        }
        /// <summary>
        /// Stops the timer and resends the oldest message without validation
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        protected void TimerFinished(Object source, System.Timers.ElapsedEventArgs e)
        {
            StopTimer();
            ReSend();
        }


        #endregion
        #region Private Methods

        /// <summary>
        /// Processes the valid ACK received
        /// </summary>
        /// <param name="ack"></param>
        private void CorrectAckReceived(uint ack)
        {
            StopTimer();
            sendedBufferMutex.WaitOne();
            currentRecognisedMutex.WaitOne();

            sendedBuffer.Remove(ack - currentRecognised);


            sendedBufferMutex.ReleaseMutex();

            currentRecognised = ack;

            currentRecognisedMutex.ReleaseMutex();

            currentDuplicate = 0;


            senderBufferMutex.WaitOne();
            sendedBufferMutex.WaitOne();

            if (sendedBuffer.getSize() > 0)
            {
                StartTimer();
            }

            while (senderBuffer.getSize() > 0 && sendedBuffer.getSize() < windowSize)
            {

                ReceivedData data = senderBuffer.Dequeue();
                SendBytes(data.data, data.sequence, false);
            }
            sendedBufferMutex.ReleaseMutex();
            senderBufferMutex.ReleaseMutex();


        }

        public void Dispose()
        {
            currentAckMutex.Dispose();
            currentRecognisedMutex.Dispose();
            currentSequenceMutex.Dispose();
            sendedBufferMutex.Dispose();
            senderBufferMutex.Dispose();
            timeoutThreadMutex.Dispose();

        }
        #endregion
    }
}
