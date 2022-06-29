using Muse_RP.Channels;
using Muse_RP.Message;
using Muse_RP.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Muse_RP.Hosts
{

    public class Client : Host, IDisposable
    {
        //Statics
        private static int minPort = 49152;
        private static int maxPort = 65535;

        //Server Info
        private readonly ConnectionInfo serverInfo;
        private Connection[] serverConnections;
        private ChannelInfo serverChannelInfo;

        //Properties
        private readonly int connectionTimeout;

        //State
        private bool connected;
        private bool tryingConnect;

        //Ping controller
        private PingController clientPingController;

        //Init Connection Vars
        private SocketHandler initSockethandler;
        private int connectionTries;
        private int currentConnectionTries;
        private Thread connectionThread;

        //Mutex

        private readonly Mutex isConnectingMutex;

        //Delegates

        private OnConnectedDelegate onConnected;
        private OnServerDisconneced onServerDisconnected;
        private OnConnectionFailure onConnectionFailure;

        #region Constructor
        public Client(HostOptions hostOptions, ConnectionInfo serverInfo, int connectionTimeout, int connectionTries, bool usesThreads) : base(hostOptions, usesThreads)
        {
            this.serverInfo = serverInfo;
            this.connectionTimeout = connectionTimeout;
            this.connectionTries = connectionTries;
            currentConnectionTries = connectionTries;

            connected = false;
            tryingConnect = false;
            isConnectingMutex = new Mutex(false);
            serverConnections = new Connection[2];
        }
        #endregion
        #region Public Methods
        #region Getters Setters

        public void AddOnConnectedHandler(OnConnectedDelegate handler)
        {
            onConnected += handler;
        }
        public void RemoveOnConnectedHandler(OnConnectedDelegate handler)
        {
            onConnected -= handler;
        }
        public void AddOnDisconnectedHandler(OnServerDisconneced handler)
        {
            onServerDisconnected += handler;
        }
        public void RemoveOnDisconnectedHandler(OnServerDisconneced handler)
        {
            onServerDisconnected -= handler;
        }
        public void AddOnConnectionFailureHandler(OnConnectionFailure handler)
        {
            onConnectionFailure += handler;
        }
        public void RemoveOnConnectionFailureHandler(OnConnectionFailure handler)
        {
            onConnectionFailure -= handler;
        }
        public bool IsConnected() { return connected; }
        #endregion
        #region Overrides
        
        public override void Start()
        {

            onInitHandler += OnInit;
            onEndHandler += (m, c) => OnEnd();
            onPingHandler += OnPing;
            if (usesThreads)
            {
                StartProcessingThread();
            }
        }
        public override void Stop()
        {
            OnEnd();
        }

        public override void TryConnect()
        {
            isConnectingMutex.WaitOne();
            if (tryingConnect)
            {
                isConnectingMutex.ReleaseMutex();
                return;
            }
            tryingConnect = true;
            isConnectingMutex.ReleaseMutex();

            Console.WriteLine("Trying to connect");
            connectionThread = new Thread(() => TryConnectThread());
            connectionThread.Start();
        }
        public override void OnTimeOut(ChannelHandler channel)
        {
            Console.WriteLine("On TimeOut");
            SendEnd();
            OnEnd();
        }
        #endregion
        #region Senders
        /// <summary>
        /// Sends the target that to the server 
        /// </summary>
        /// <param name="type">Type of the message</param>
        /// <param name="reliableChannel">True if is through the reliable channel</param>
        /// <param name="data">Data to send</param>
        public void SendToServer(ushort type, bool reliableChannel, byte[] data)
        {
            if (connected)
            {
                if (reliableChannel)
                {
                    reliableSocketHandler.SendMessage(serverConnections[0], type, data, false, false);
                }
                else
                {
                    noReliableSocketHandler.SendMessage(serverConnections[1], type, data, false, false);
                }

            }
        }
        /// <summary>
        /// Sends an End message to the server
        /// </summary>
        public void SendEnd()
        {

            MessageObject message = new MessageObject(0, 0, 0, false, false, false, true, null);

            if (reliableSocketHandler.IsActivated())
            {
                reliableSocketHandler.SendWithoutChannel(message.toByteArray(), serverConnections[0].endPoint);
            }
            OnEnd();

        }
        #endregion

        #endregion
        #region Private Methods
        #region Threads
        /// <summary>
        /// Tries to connect to server
        /// </summary>
        /// <param name="sequenceNumber">The number by which sequencing numbers will begin to increase</param>
        /// <param name="ackNumber">The number by which ACK numbers will begin to increase</param>
        /// <param name="ports">Array of two elements with the reliable and no-reliable ports</param>
        /// <param name="serverEndPoint">Endpoint of the server</param>
        private void ConnectionTry(uint sequenceNumber, uint ackNumber, int[] ports, EndPoint serverEndPoint)
        {
            initSockethandler = new SocketHandler(this, ports[0], true);
            ChannelInfo newInfo = new ChannelInfo(ports[0], ports[1], 0, 0, 0, 0, 0);
            MessageObject initMessage = new MessageObject(0, sequenceNumber, ackNumber, false, false, true, false, newInfo.getBytes());

            initSockethandler.Start();
            initSockethandler.SendWithoutChannel(initMessage.toByteArray(), serverEndPoint);

            Thread.Sleep(connectionTimeout);
            currentConnectionTries--;

        }
        /// <summary>
        /// Thread of connection with server
        /// </summary>
        private void TryConnectThread()
        {
            try
            {

                uint[] sequenceAckNumbers = GetRandomSequenceAck();
                int[] ports = GetRandomPorts();
                EndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(serverInfo.IP), serverInfo.reliablePort);

                while (!connected && currentConnectionTries > 0)
                {
                    ConnectionTry(sequenceAckNumbers[0], sequenceAckNumbers[1], ports, serverEndPoint);
                    ports = GetRandomPorts();
                    initSockethandler.Stop();
                }

                isConnectingMutex.WaitOne();
                tryingConnect = false;
                isConnectingMutex.ReleaseMutex();

                if (!connected)
                {
                    Console.WriteLine("No response from the server");
                    currentConnectionTries = connectionTries;
                    onConnectionFailure?.Invoke();
                }
            }
            catch (ThreadInterruptedException e)
            {
                Console.WriteLine("Connected");
            }
        }
        #endregion
        #region Aux ( Pasar a script de utils?)
        private uint[] GetRandomSequenceAck()
        {
            uint[] result = new uint[2];
            Random random = new Random();
            uint firstBits = (uint)random.Next(1 << 30);
            uint lastBits = (uint)random.Next(1 << 2);
            result[0] = (firstBits << 2) | lastBits;
            firstBits = (uint)random.Next(1 << 30);
            lastBits = (uint)random.Next(1 << 2);
            result[1] = (firstBits << 2) | lastBits;
            return result;

        }
        private int[] GetRandomPorts()
        {
            int[] result = new int[2];
            Random random = new Random();
            result[0] = random.Next(minPort, maxPort);
            result[1] = random.Next(minPort, maxPort);
            if (result[0] == result[1])
            {
                if (result[0] == maxPort)
                {
                    result[0]--;
                }
                else
                {
                    result[0]++;
                }

            }
            return result;

        }

        #endregion
        #region Delegates
        /// <summary>
        /// Handles End Event
        /// </summary>
        private void OnEnd()
        {

            connected = false;
            reliableSocketHandler.Stop();
            noReliableSocketHandler.Stop();
            onServerDisconnected?.Invoke();
            clientPingController.StopPinging();
            Console.WriteLine("Desconnected");


        }
        /// <summary>
        /// Handles a Ping message
        /// </summary>
        /// <param name="message">Ping message</param>
        /// <param name="conn">Connection with the source</param>
        private void OnPing(MessageObject message, Connection conn)
        {
            if (connected)
            {
                clientPingController.OnPingReceived(message, conn);
            }
        }
        /// <summary>
        /// Handles Init Event
        /// </summary>
        /// <param name="message">Message of init</param>
        /// <param name="conn">Connection with the source</param>
        /// <param name="reliable">True if it's through the reliable channel</param>
        private void OnInit(MessageObject message, Connection conn, bool reliable)
        {
            if (connected)
            {
                if (reliable)
                {
                    reliableSocketHandler.SendForcedAck(conn);
                }
                else
                {
                    noReliableSocketHandler.SendForcedAck(conn);
                }
                return;
            }

            if (!reliable)
            {
                Console.WriteLine("Connected with both channels");
                connected = true;
                noReliableSocketHandler.ReceiveForcedAck(conn, message.getAck());
                noReliableSocketHandler.ReceiveForcedMessage(message, conn);
                noReliableSocketHandler.SendForcedAck(conn);
                clientPingController.StartPinging();
                onConnected?.Invoke();
            }
            else
            {


                isConnectingMutex.WaitOne();
                tryingConnect = false;
                isConnectingMutex.ReleaseMutex();
                initSockethandler.Stop();
                connectionThread?.Interrupt();

                serverConnections[0] = conn;

                serverChannelInfo = new ChannelInfo(message.getData());

                hostOptions.reliablePercentage = serverChannelInfo.reliablePercentage;
                hostOptions.reliablePort = serverChannelInfo.yourReliablePort;
                hostOptions.noReliablePort = serverChannelInfo.yourNoReliablePort;
                serverInfo.noReliablePort = serverChannelInfo.noReliablePort;

                reliableSocketHandler = new SocketHandler(this, serverChannelInfo.yourReliablePort, true);
                noReliableSocketHandler = new SocketHandler(this, serverChannelInfo.yourNoReliablePort, false);

                reliableSocketHandler.Start();
                noReliableSocketHandler.Start();

                ReliableChannelHandler reliableChannel = new ReliableChannelHandler(this, serverChannelInfo.windowSize, message.getAck(), message.getSequenceNumber(), hostOptions.waitTime, hostOptions.timerTime, conn.endPoint, null);

                if (!reliableSocketHandler.AddHandler(conn, reliableChannel))
                {
                    return;
                }

                Console.WriteLine("Reliable channel connected");

                reliableSocketHandler.SendForcedAck(conn);

                uint[] sequenceAckNumbers = GetRandomSequenceAck();

                EndPoint noReliableEndPoint = new IPEndPoint(IPAddress.Parse(serverInfo.IP), serverInfo.noReliablePort);

                Connection serverNoReliableConnection = new Connection(noReliableEndPoint, false);
                serverConnections[1] = serverNoReliableConnection;

                PartialReliableHandler noReliableChannel = new PartialReliableHandler(this, serverChannelInfo.windowSize, sequenceAckNumbers[0], sequenceAckNumbers[1], hostOptions.waitTime, hostOptions.timerTime, noReliableEndPoint, null, serverChannelInfo.reliablePercentage);

                if (!noReliableSocketHandler.AddHandler(serverNoReliableConnection, noReliableChannel))
                {
                    return;
                }

                noReliableChannel.Send(0, false, true, serverChannelInfo.getBytes());
                reliableChannel.SetConnectionInfo(serverInfo);
                noReliableChannel.SetConnectionInfo(serverInfo);
                clientPingController = new PingController(reliableChannel, this, hostOptions.timePing, hostOptions.timerTime, hostOptions.timeOut);
            }
        }

        public void Dispose()
        {
            isConnectingMutex.Dispose();
        }
        #endregion
        #endregion
    }
}
