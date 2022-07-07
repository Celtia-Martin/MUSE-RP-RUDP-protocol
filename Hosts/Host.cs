using Muse_RP.Channels;
using Muse_RP.Message;
using Muse_RP.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace Muse_RP.Hosts
{
    public abstract class Host
    {

        //Storage

        protected ConcurrentDictionary<string, Connection> connections;
        protected ConcurrentDictionary<string, ConnectionInfo> connectionsInfo;
        protected MuseBuffer<UpperLayerMessageData> messagesToProcess;

        //Delegates


        protected MessageDelegate onPingHandler;
        protected MessageDelegate onEndHandler;
        protected InitMessageDelegate onInitHandler;

        //Sockets

        protected SocketHandler reliableSocketHandler;
        protected SocketHandler noReliableSocketHandler;

        //Properties

        protected HostOptions hostOptions;

        //State

        protected bool hostActivated;
        protected bool usesThreads;
        protected Thread processingThread;



        #region Constructor
        protected Host(HostOptions hostOptions, bool usesThreads)
        {
            this.hostOptions = hostOptions;
            this.usesThreads = usesThreads;

            connections = new ConcurrentDictionary<string, Connection>();
            connectionsInfo = new ConcurrentDictionary<string, ConnectionInfo>();

            reliableSocketHandler = new SocketHandler(this, hostOptions.reliablePort, true);
            noReliableSocketHandler = new SocketHandler(this, hostOptions.noReliablePort, false);

            messagesToProcess = new MuseBuffer<UpperLayerMessageData>();

        }
        #endregion
        #region Abstract Methods
        /// <summary>
        /// Starts the host behavior
        /// </summary>
        public abstract void Start();
        /// <summary>
        /// Tries to connect to the specified host
        /// </summary>
        public abstract void TryConnect();
        /// <summary>
        /// Handles the timeout event of a channel
        /// </summary>
        /// <param name="channel">The channel who activated the timeout event</param>
        public abstract void OnTimeOut(ChannelHandler channel);
        /// <summary>
        /// Stops the host
        /// </summary>
        public abstract void Stop();
        #endregion
        #region Public Methods
        #region Getters and Setters

        public HostOptions GetHostOptions() { return hostOptions; }
        public float GetTimeOut() { return hostOptions.timeOut; }
        #endregion
        #region Add  Special Messages Handlers
        public void AddPingHandler(MessageDelegate handler)
        {
            onPingHandler += handler;
        }
        public void AddInitHandler(InitMessageDelegate handler)
        {
            onInitHandler += handler;

        }
        public void AddEndHandler(MessageDelegate handler)
        {
            onEndHandler += handler;
        }
        public void RemovePingHandler(MessageDelegate handler)
        {
            onPingHandler -= handler;
        }
        public void RemoveInitHandler(InitMessageDelegate handler)
        {
            onInitHandler -= handler;

        }
        public void RemoveEndHandler(MessageDelegate handler)
        {
            onEndHandler -= handler;
        }
        /// <summary>
        /// Handles an Init message
        /// </summary>
        /// <param name="msg">The init message</param>
        /// <param name="connInfo">The connection info of the source</param>
        /// <param name="reliable">True if it's reliable channel</param>
        public void ReceiveInit(MessageObject msg, EndPoint connInfo, bool reliable)
        {
            onInitHandler?.Invoke(msg, new Connection(connInfo, reliable), reliable);
        }
        /// <summary>
        /// Handles an End message
        /// </summary>
        /// <param name="msg">The end message</param>
        /// <param name="connInfo">The connection info of the source</param>
        public void ReceiveEnd(MessageObject msg, EndPoint connInfo)
        {
            onEndHandler?.Invoke(msg, new Connection(connInfo, true));
        }
        /// <summary>
        /// Handles a Ping message
        /// </summary>
        /// <param name="msg">The ping message</param>
        /// <param name="connInfo">The connection info of the source</param>
        public void ReceivePing(MessageObject msg, EndPoint connInfo)
        {
            onPingHandler?.Invoke(msg, new Connection(connInfo, false));
        }
        #endregion
        #region ChannelHandlers

        /// <summary>
        /// Updates the message timer of the channels with a host
        /// </summary>
        /// <param name="connInfo">The connection info of the other host</param>
        /// <param name="milliseconds">The new timer</param>
        public void SetChannelHandlersTimer(ConnectionInfo connInfo, int milliseconds)
        {
            Connection reliableConn = new Connection(connInfo.IP, connInfo.reliablePort, true);
            Connection noReliableConn = new Connection(connInfo.IP, connInfo.noReliablePort, true);
            reliableSocketHandler.SetTimerTimeToChannel(reliableConn, milliseconds);
            noReliableSocketHandler.SetTimerTimeToChannel(noReliableConn, milliseconds);
        }
        public void AddReliableConnectionHandler(Connection conn, ChannelHandler handler)
        {
            reliableSocketHandler.AddHandler(conn, handler);
        }
        public void AddNoReliableConnectionHandler(Connection conn, ChannelHandler handler)
        {
            noReliableSocketHandler.AddHandler(conn, handler);
        }
        public void RemoveReliableConnectionHandler(Connection conn)
        {
            reliableSocketHandler.RemoveHandler(conn);
        }
        public void RemoveNoReliableConnectionHandler(Connection conn)
        {
            noReliableSocketHandler.RemoveHandler(conn);
        }
        #endregion
        #region Other
        /// <summary>
        /// Sends a message to a destination
        /// </summary>
        /// <param name="reliable">True if it's with a realiable channel</param>
        /// <param name="destination">Destination connection </param>
        /// <param name="type">Type of the message</param>
        /// <param name="data">Data of the message</param>
        /// <param name="isInit">True if it's Init message</param>
        /// <param name="isEnd">True if it's End message</param>
        public void Send(bool reliable, Connection destination, ushort type, byte[] data, bool isInit, bool isEnd)
        {
            if (reliable)
            {
                reliableSocketHandler.SendMessage(destination, type, data, isInit, isEnd);
            }
            else
            {
                noReliableSocketHandler.SendMessage(destination, type, data, isInit, isEnd);
            }

        }
        /// <summary>
        /// Sends a ping to a destination
        /// </summary>
        /// <param name="conn">Connection with the destination</param>
        /// <param name="pingMessage">The ping message</param>
        public void SendPing(Connection conn, MessageObject pingMessage)
        {
            pingMessage.setData(BitConverter.GetBytes(true));
            reliableSocketHandler.SendPing(conn, pingMessage);
        }
        /// <summary>
        /// Adds the message to the buffer of messages to process
        /// </summary>
        /// <param name="message">The message</param>
        /// <param name="source">Connection with the source</param>
        public void Receive(MessageObject message, Connection source)
        {
            messagesToProcess.Add(new UpperLayerMessageData(message, source));
        }
        /// <summary>
        /// Starts the thread to process the received messages
        /// </summary>
        public void StartProcessingThread()
        {
            hostActivated = true;
            processingThread = new Thread(() => MessageProcesser());
            processingThread.Start();
        }
        /// <summary>
        /// Processes the next message in the buffer
        /// </summary>
        public void ProcessNextMessage()
        {
            try
            {
                UpperLayerMessageData messageData;
                messageData = messagesToProcess.Dequeue();
                if (messageData != null)
                {
                    hostOptions.HandleMessage(messageData.message, messageData.source);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Host stopped because there was an exception processing: " + e.Message);
                Stop();
            }


        }
        #endregion
        #endregion
        #region Threads
        /// <summary>
        /// Message processer thread
        /// </summary>
        private void MessageProcesser()
        {
            while (hostActivated)
            {

                while (messagesToProcess.getSize() > 0)
                {
                    ProcessNextMessage(); 
                }

                Thread.Sleep(hostOptions.waitTime);
            }
        }

    }

    #endregion


}
