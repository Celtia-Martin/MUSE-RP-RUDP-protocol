using Muse_RP.Channels;
using Muse_RP.Message;
using Muse_RP.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace Muse_RP.Hosts
{
    public class Server : Host
    {
        //Properties
        private int clientIDCont;

        //Delegates

        private OnClientConnectedDelegate onClientConnectedDelegate;
        private OnClientDisconnected onClientDisconnected;

        //Storage
        private readonly ConcurrentDictionary<int, Connection> waitingConnections;
        private readonly ConcurrentDictionary<Connection, PingController> pingControllers;

        //DEBUG BORRAR LUEGO
        public OnConnectionFailure onSendEnd;
        public OnConnectionFailure onEndReceived;


        //
        #region Public Methods
        #region Constructor
        public Server(HostOptions hostOptions, bool usesThreads) : base(hostOptions, usesThreads)
        {
            clientIDCont = 0;
            waitingConnections = new ConcurrentDictionary<int, Connection>();
            pingControllers = new ConcurrentDictionary<Connection, PingController>();
        }
        #endregion
        #region Senders
        /// <summary>
        /// Sends the message to all the clients connected
        /// </summary>
        /// <param name="type">Type of the message</param>
        /// <param name="reliableChannel">True if it's through a reliable channel</param>
        /// <param name="data">Data to send</param>
        public void SendToAll(ushort type, bool reliableChannel, byte[] data)
        {
            Connection newConnection;
            foreach (ConnectionInfo connectionInfo in connectionsInfo.Values)
            {
                newConnection = new Connection(connectionInfo.IP, reliableChannel ? connectionInfo.reliablePort : connectionInfo.noReliablePort, reliableChannel);
                if (reliableChannel)
                {
                    reliableSocketHandler.SendMessage(newConnection, type, data, false, false);
                }
                else
                {
                    noReliableSocketHandler.SendMessage(newConnection, type, data, false, false);
                }


            }
        }
        /// <summary>
        /// Sends an End message to all the clients connected
        /// </summary>
        public void SendEndToAll()
        {
            Connection newConnection;
            foreach (ConnectionInfo connectionInfo in connectionsInfo.Values)
            {
                newConnection = new Connection(connectionInfo.IP, connectionInfo.reliablePort, true);
                reliableSocketHandler.SendMessage(newConnection, 0, null, false, true);

            }
        }
        /// <summary>
        /// Sends the message to the target client
        /// </summary>
        /// <param name="type">Tyope of the message</param>
        /// <param name="reliableChannel">True if it's through a reliable channel</param>
        /// <param name="conn">Connection with the client</param>
        /// <param name="data">Data to send</param>
        public void SendTo(ushort type, bool reliableChannel, ConnectionInfo conn, byte[] data)
        {
            Connection newConnection = new Connection(conn.IP, reliableChannel ? conn.reliablePort : conn.noReliablePort, reliableChannel);
            if (connections.ContainsKey(newConnection.ToString()))
            {
                if (reliableChannel)
                {
                    reliableSocketHandler.SendMessage(newConnection, type, data, false, false);
                }
                else
                {
                    noReliableSocketHandler.SendMessage(newConnection, type, data, false, false);
                }
            }
            else
            {
                Console.WriteLine("Conexion no establecida. Imposible mandar");
            }
        }
        /// <summary>
        /// Sends the message to the target client
        /// </summary>
        /// <param name="type">Tyope of the message</param>
        /// <param name="reliableChannel">True if it's through a reliable channel</param>
        /// <param name="conn">Connection with the client</param>
        /// <param name="data">Data to send</param>
        public void SendTo(ushort type, bool reliableChannel, Connection conn, byte[] data)
        {
            if (connections.ContainsKey(conn.ToString()))
            {
                if (reliableChannel)
                {
                    reliableSocketHandler.SendMessage(conn, type, data, false, false);
                }
                else
                {
                    noReliableSocketHandler.SendMessage(conn, type, data, false, false);
                }

            }

        }
        /// <summary>
        /// Send end to the target client
        /// </summary>
        /// <param name="conn">Connection with the client</param>
        public void SendEnd(Connection conn)
        {
            Connection reliableConn = new Connection(conn.IP, reliableSocketHandler.GetReliablePortFromChannel(conn), true);
            Connection noReliableConn = new Connection(conn.IP, reliableSocketHandler.GetNoReliablePortFromChannel(conn), false);

            MessageObject message = new MessageObject(0, 0, 0, false, false, false, true, null);
            reliableSocketHandler.SendWithoutChannel(message.toByteArray(), conn.endPoint);

            RemoveConnections(reliableConn, noReliableConn);


        }
        #endregion
        #region Overrides
        public override void Start()
        {
            reliableSocketHandler.Start();
            noReliableSocketHandler.Start();
            if (usesThreads) { StartProcessingThread(); }
            onInitHandler += OnInit;
            onPingHandler += OnPing;
            onEndHandler += (mesage, conn) => OnEnd(mesage, conn);

        }
        public override void Stop()
        {
            reliableSocketHandler.Stop();
            noReliableSocketHandler.Stop();
            foreach (PingController pingController in pingControllers.Values)
            {
                pingController.StopPinging();
            }
            Console.WriteLine("Server stopped");
        }

        public override void TryConnect()
        {
            throw new NotImplementedException();
        }
        #endregion
        #region Delegates
        public void AddOnClientConnectedDelegate(OnClientConnectedDelegate onClientConnected)
        {
            onClientConnectedDelegate += onClientConnected;
        }
        public void RemoveOnClientConnectedDelegate(OnClientConnectedDelegate onClientConnected)
        {
            onClientConnectedDelegate -= onClientConnected;
        }
        public void AddOnClientDisconnectedDelegate(OnClientDisconnected onClientDisconnected)
        {
            this.onClientDisconnected += onClientDisconnected;
        }
        public void RemoveOnClientDisconnectedDelegate(OnClientDisconnected onClientDisconnected)
        {
            this.onClientDisconnected -= onClientDisconnected;
        }

        #endregion
        #endregion
        #region Private Methods
        #region Delegates
        /// <summary>
        /// Handles a Ping message
        /// </summary>
        /// <param name="message">Ping message</param>
        /// <param name="conn">Connection with the client</param>
        private void OnPing(MessageObject message, Connection conn)
        {
            if (pingControllers.TryGetValue(conn, out PingController controller))
            {
                controller.OnPingReceived(message, conn);

            }
        }
        /// <summary>
        /// Handles an Init message
        /// </summary>
        /// <param name="message">Init message</param>
        /// <param name="conn">Connection with the source</param>
        /// <param name="reliable">True if it's through a reliable channel</param>
        private void OnInit(MessageObject message, Connection conn, bool reliable)
        {
            if (!reliable)
            {

                if (hostOptions.maxConnections <= connectionsInfo.Count)
                {
                    return;

                }


                ChannelInfo clientInfo = new ChannelInfo(message.getData());
                conn.ID = clientInfo.ID;


                PartialReliableHandler newPartialReliableChannel = new PartialReliableHandler(this, hostOptions.windowSize, message.getAck(), message.getSequenceNumber(), hostOptions.waitTime, hostOptions.timerTime, conn.endPoint, null, hostOptions.reliablePercentage);
                if (noReliableSocketHandler.AddHandler(conn, newPartialReliableChannel))
                {
                    noReliableSocketHandler.SendMessage(conn, 0, null, true, false);

                    if (waitingConnections.TryRemove(clientInfo.ID, out Connection value))
                    {
                        ConnectionInfo newInfo = new ConnectionInfo(conn.IP, value.port, conn.port);
                        reliableSocketHandler.SetChannelHandlerChannelInfo(value, newInfo);
                        noReliableSocketHandler.SetChannelHandlerChannelInfo(conn, newInfo);

                        connections.TryAdd(conn.ToString(), conn);
                        connectionsInfo.TryAdd(newInfo.ToString(), newInfo);

                        Console.WriteLine("Connected No Reliable channel: " + conn.ToString());

                        onClientConnectedDelegate?.Invoke(newInfo);

                    }
                    else
                    {
                        noReliableSocketHandler.SendForcedAck(conn);
                    }
                }
                else
                {
                    noReliableSocketHandler.SendForcedAck(conn);
                }

            }
            else
            {
                if (hostOptions.maxConnections <= connectionsInfo.Count)
                {
                    return;

                }
                ChannelInfo infoClient = new ChannelInfo(message.getData());
                int noReliablePort = infoClient.noReliablePort;
                int reliablePort = infoClient.reliablePort;

                ReliableChannelHandler newReliableChannel = new ReliableChannelHandler(this, hostOptions.windowSize, message.getAck(), message.getSequenceNumber(), hostOptions.waitTime, hostOptions.timerTime, conn.endPoint, null);

                if (reliableSocketHandler.AddHandler(conn, newReliableChannel))
                {
                    ChannelInfo channelInfo = new ChannelInfo(hostOptions.reliablePort, hostOptions.noReliablePort, hostOptions.windowSize, hostOptions.reliablePercentage, reliablePort, noReliablePort, clientIDCont);
                    conn.ID = clientIDCont;
                    clientIDCont++;
                    connections.TryAdd(conn.ToString(), conn);
                    waitingConnections.TryAdd(channelInfo.ID, conn);
                    reliableSocketHandler.SendMessage(conn, 0, channelInfo.getBytes(), true, false);
                    Console.WriteLine("Reliable channel connected " + conn.IP + ":" + conn.port);
                    PingController pingController = new PingController(newReliableChannel, this, hostOptions.timePing, hostOptions.timerTime, hostOptions.timeOut);
                    pingController.StartPinging();
                    pingControllers.TryAdd(conn, pingController);

                }


            }
        }
        /// <summary>
        /// Handles an End message
        /// </summary>
        /// <param name="message">The End message</param>
        /// <param name="conn">Connection with the client</param>
        private void OnEnd(MessageObject message, Connection conn)
        {
            onEndReceived?.Invoke();
            ConnectionInfo connectionInfo = reliableSocketHandler.GetConnectionInfoOfChannel(conn);
            if (connectionInfo == null) { return; }
            Connection noReliableConn = new Connection(conn.IP, connectionInfo.noReliablePort, false);
            RemoveConnections(conn, noReliableConn);

        }
        /// <summary>
        /// Handles the TimeoutEvent
        /// </summary>
        /// <param name="channel">Channel that triggered the event</param>
        public override void OnTimeOut(ChannelHandler channel)
        {
            Console.WriteLine("On TimeOut");
            Connection reliableConnection = new Connection(channel.getConnection().IP, channel.getConnectionInfo().reliablePort, true);
            Connection noReliableConnection = new Connection(channel.getConnection().IP, channel.getConnectionInfo().noReliablePort, false);

            if (connections.TryGetValue(reliableConnection.ToString(), out Connection value))
            {
                onSendEnd?.Invoke();
                SendEnd(value);

            }

            RemoveConnections(reliableConnection, noReliableConnection);
        }

        #endregion
        #region Auxs
        /// <summary>
        /// Remove all the connections with a client
        /// </summary>
        /// <param name="reliableConnection">Reliable connection with the client</param>
        /// <param name="noReliableConnection">No Reliable connection with the client</param>
        private void RemoveConnections(Connection reliableConnection, Connection noReliableConnection)
        {
            connections.TryRemove(reliableConnection.ToString(), out Connection deletedReliableConnection);
            connections.TryRemove(noReliableConnection.ToString(), out Connection deletedNoRelConnection);
            if (pingControllers.TryRemove(reliableConnection, out PingController pingController))
            {
                pingController.StopPinging();
            }
            ConnectionInfo info = new ConnectionInfo(reliableConnection.IP, reliableConnection.port, noReliableConnection.port);
            if (connectionsInfo.TryGetValue(info.ToString(), out ConnectionInfo value))
            {
                onClientDisconnected?.Invoke(value);
            }

            connectionsInfo.TryRemove(info.ToString(), out ConnectionInfo deletedinfo);

            reliableSocketHandler.RemoveHandler(reliableConnection);
            noReliableSocketHandler.RemoveHandler(noReliableConnection);

            Console.WriteLine("Connection with " + reliableConnection.IP+":"+reliableConnection.port + " removed");
        }
        #endregion
        #endregion

    }
}
