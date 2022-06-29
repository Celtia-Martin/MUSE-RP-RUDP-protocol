using Muse_RP.Hosts;
using Muse_RP.Message;
using Muse_RP.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Muse_RP.Channels
{
    public class SocketHandler
    {
        //References

        private readonly Host myHost;

        //Properties

        private readonly int maxBytes = 1000;
        private readonly int waitTime = 1;

        //Buffer

        private readonly MuseBuffer<ReceiverMessageData> receivedQueue;

        //State

        private bool socketActivated;

        //Threads 

        private Thread receiverThread;
        private Thread processingThread;

        //ChannelHandler Storage

        private readonly ConcurrentDictionary<Connection, ChannelHandler> channelHandlers;

        //Connection Info

        private Socket udpSocket;
        private readonly int myPort;
        private readonly bool reliable;

        #region Constructor
        public SocketHandler(Host myHost, int myPort, bool reliable)
        {
            this.myHost = myHost;
            this.myPort = myPort;
            this.reliable = reliable;

            channelHandlers = new ConcurrentDictionary<Connection, ChannelHandler>();

            receivedQueue = new MuseBuffer<ReceiverMessageData>();

        }
        #endregion
        #region Public Methods
        #region Getters Setters
        /// <summary>
        /// Updates the message timer of the target channels
        /// </summary>
        /// <param name="conn">The connection of the target channels</param>
        /// <param name="milliseconds">New timer value</param>
        /// <returns></returns>
        public bool SetTimerTimeToChannel(Connection conn, int milliseconds)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler value))
            {
                value.SetTimerTime(milliseconds);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Updates the connection info of the channels
        /// </summary>
        /// <param name="conn">The connection of the target channels</param>
        /// <param name="connectionInfo">New ConnectionInfo value</param>
        /// <returns></returns>
        public bool SetChannelHandlerChannelInfo(Connection conn, ConnectionInfo connectionInfo)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler value))
            {
                value.SetConnectionInfo(connectionInfo);
                return true;
            }
            return false;
        }
        public bool IsActivated() { return socketActivated; }
        public ConnectionInfo GetConnectionInfoOfChannel(Connection conn)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler value))
            {

                return value.getConnectionInfo();
            }
            return null;
        }
        public int GetReliablePortFromChannel(Connection conn)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                return handler.getConnectionInfo().reliablePort;

            }
            return -1;
        }
        public int GetNoReliablePortFromChannel(Connection conn)
        {

            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                return handler.getConnectionInfo().noReliablePort;

            }
            return -1;
        }
        /// <summary>
        /// Adds new channel to the socket
        /// </summary>
        /// <param name="conn">Connection of the new channel</param>
        /// <param name="handler">Channel handler</param>
        /// <returns></returns>
        public bool AddHandler(Connection conn, ChannelHandler handler)
        {
            if (channelHandlers.ContainsKey(conn))
            {
                return false;
            }
            else
            {
                channelHandlers.TryAdd(conn, handler);
                handler.Start(udpSocket, conn);
                return true;
            }

        }
        /// <summary>
        /// Removes new channel to the socket
        /// </summary>
        /// <param name="conn">Connection of the channel</param>
        public bool RemoveHandler(Connection conn)
        {
            if(channelHandlers.TryRemove(conn, out ChannelHandler handler))
            {
                handler.Stop();
                return true;
            }
           
            return false;
        }
        #endregion
        #region Start Stop
        /// <summary>
        /// The socket starts listening
        /// </summary>
        public void Start()
        {
            socketActivated = true;

            udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, myPort);

            udpSocket.Bind(localEndPoint);

            receiverThread = new Thread(() => ReceiveThread());
            processingThread = new Thread(() => ProcessReceivedThread());

            receiverThread.Start();
            processingThread.Start();
        }
        /// <summary>
        /// The socket stops listening
        /// </summary>
        public void Stop()
        {
            socketActivated = false;
            receiverThread.Interrupt();
            receivedQueue.Clear();

            foreach (ChannelHandler channel in channelHandlers.Values)
            {
                channel.Stop();
            }
            udpSocket.Close();
            channelHandlers.Clear();
        }
        #endregion
        #region Send Methods
        /// <summary>
        /// The socket sends the message through the correct channel
        /// </summary>
        /// <param name="conn">The connection with the destination</param>
        /// <param name="type">Type of the message</param>
        /// <param name="data">Data to send in bytes</param>
        /// <param name="isInit">True if it's Init message</param>
        /// <param name="isEnd">True if it's End message</param>
        /// <returns></returns>
        public bool SendMessage(Connection conn, ushort type, byte[] data, bool isInit, bool isEnd)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                handler.Send(type, isEnd, isInit, data);
                return true;
            }
            return false;
        }
        /// <summary>
        /// Sends the current ACK to the destination of the connection
        /// </summary>
        /// <param name="conn">Connection with the destination</param>
        public void SendForcedAck(Connection conn)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                handler.SendAck();
            }
        }
        /// <summary>
        /// Sends a message without channel, directly through the socket
        /// </summary>
        /// <param name="data">Data to send</param>
        /// <param name="endPoint">Destination</param>
        public void SendWithoutChannel(byte[] data, EndPoint endPoint)
        {
            udpSocket.SendTo(data, endPoint);
        }
        /// <summary>
        /// Sends a ping message
        /// </summary>
        /// <param name="conn">Connection with the destination</param>
        /// <param name="message">Ping message</param>
        public void SendPing(Connection conn, MessageObject message)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                handler.SendPing(message);
            }
        }
        #endregion
        #region Receive Methods
        /// <summary>
        /// Processes the target ACK 
        /// </summary>
        /// <param name="conn">Connection with the source</param>
        /// <param name="ack">Target ACK</param>
        public void ReceiveForcedAck(Connection conn, uint ack)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                handler.ReceiveACK(ack);

            }
        }
        /// <summary>
        /// Processes the target message
        /// </summary>
        /// <param name="message">Message received</param>
        /// <param name="conn">Connection with the source</param>
        public void ReceiveForcedMessage(MessageObject message, Connection conn)
        {
            if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                handler.Receive(message);

            }
        }
        #endregion
        #endregion
        #region Private Methods 
        /// <summary>
        /// Processes the data received
        /// </summary>
        /// <param name="data">Data received</param>
        private void ProcessMessage(ReceiverMessageData data)
        {
            Connection conn = new Connection(data.senderInfo, reliable);
            MessageObject message = new MessageObject(data.data);

            if (message.isInit())
            {
                myHost.ReceiveInit(message, data.senderInfo, reliable);
            }
            else if (channelHandlers.TryGetValue(conn, out ChannelHandler handler))
            {
                handler.ProcessMessage(message);
            }
        }
        #endregion
        #region Threads
        /// <summary>
        /// Listening Thread
        /// </summary>
        private void ReceiveThread()
        {
            try
            {
                byte[] buffer = new byte[maxBytes];
                int size;

                EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

                try
                {
                    while (socketActivated)
                    {

                        size = udpSocket.ReceiveFrom(buffer, ref endPoint);
                        receivedQueue.Add(new ReceiverMessageData(buffer.Take(size).ToArray(), endPoint));

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Socket stopped because there was an exception receiving: " + e.Message);
                    Stop();
                }
            }
            catch (ThreadInterruptedException e)
            {
                //Socket closed
            }
        }
        /// <summary>
        /// Processing thread of the received messages
        /// </summary>
        private void ProcessReceivedThread()
        {
            try
            {
                while (socketActivated)
                {
                    while (receivedQueue.getSize() > 0)
                    {

                        ProcessMessage(receivedQueue.Dequeue());

                    }
                    Thread.Sleep(waitTime);
                }
            }
            catch (ThreadInterruptedException e)
            {
                //Socket closed
            }
            catch (Exception e)
            {
                Console.WriteLine("Channel stopped because there was an exception processing: " + e.Message);
                Stop();
            }
        }
        #endregion
    }
}
