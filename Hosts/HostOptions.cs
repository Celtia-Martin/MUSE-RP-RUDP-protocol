using Muse_RP.Message;
using System;
using System.Collections.Generic;
using System.Text;

namespace Muse_RP.Hosts
{
    public struct HostOptions
    {
        //Properties
        public int maxConnections;
        public int timeOut;
        public int timePing;
        public int waitTime;
        public int reliablePort;
        public int noReliablePort;
        public uint windowSize;
        public int timerTime;
        public uint reliablePercentage;
        public MessageHandler messageHandler;

        #region Constructor
        public HostOptions(int maxConnections, int timeOut, int timePing, int waitTime, int reliablePort, int noReliablePort, uint windowSize, int timerTime, uint reliablePercentage, MessageHandler messageHandler)
        {
            this.maxConnections = maxConnections;
            this.timeOut = timeOut;
            this.timePing = timePing;
            this.waitTime = waitTime;
            this.reliablePort = reliablePort;
            this.noReliablePort = noReliablePort;
            this.windowSize = windowSize;
            this.timerTime = timerTime;
            this.reliablePercentage = reliablePercentage;
            this.messageHandler = messageHandler;
        }
        #endregion
        #region Public Methods
        /// <summary>
        /// Matches the message with its corresponding handler
        /// </summary>
        /// <param name="message">The target message</param>
        /// <param name="source">Connection with the source</param>
        public void HandleMessage(MessageObject message, Connection source)
        {
            messageHandler.InvokeHandler(message.getType(), message, source);
        }
        #endregion
    }
}
