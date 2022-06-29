using Muse_RP.Channels;
using Muse_RP.Hosts;
using Muse_RP.Message;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Timers;

namespace Muse_RP.Utils
{
    public class PingController : IDisposable
    {
        //References
        private readonly ChannelHandler myChannel;
        private readonly Host myHost;

        //Properties
        private readonly float timePing;
        private bool replied;
        private int contNotReplies;
        private uint responseWaiting;
        private readonly int maxNotReplies;

        //Times
        private int timer;

        //Objects
        private System.Timers.Timer pingTimer;
        private readonly Stopwatch clock;

        //Mutex
        private readonly Mutex clockMutex;

        public PingController(ChannelHandler myChannel, Host myHost, float timePing, int timer, int timeout)
        {
            this.myChannel = myChannel;
            this.myHost = myHost;
            this.timePing = timePing;
            clockMutex = new Mutex(false);
            clock = new Stopwatch();
            this.timer = timer;
            maxNotReplies = (int)(Math.Round(((float)timeout / (float)timePing)) + 1);
            contNotReplies = -1;
        }


        #region Public methods

        #region Timer methods
        /// <summary>
        /// Starts pinging the attached host
        /// </summary>
        public void StartPinging()
        {
            pingTimer = new System.Timers.Timer(timePing);
            pingTimer.AutoReset = true;
            pingTimer.Elapsed += OnPing;
            pingTimer.Start();
        }
        /// <summary>
        /// Stops pinging
        /// </summary>
        public void StopPinging()
        {
            pingTimer.Stop();
            pingTimer.Close();
            clock.Stop();

        }

        #endregion
        #region Timer Event
        /// <summary>
        /// Handles a Ping message
        /// </summary>
        /// <param name="message">The ping message</param>
        /// <param name="conn">Connection with the source</param>
        public void OnPingReceived(MessageObject message, Connection conn)
        {
            bool isMine = BitConverter.ToBoolean(message.getData(), 0);
            if (isMine)
            {
                uint id = message.getSequenceNumber();
                clockMutex.WaitOne();
                if (id != responseWaiting)
                {
                    clockMutex.ReleaseMutex();
                    return;
                }

                clock.Stop();

                float lastTime = clock.ElapsedMilliseconds;
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Ping received: " + lastTime);
                Console.ForegroundColor = ConsoleColor.White;
                replied = true;
                clockMutex.ReleaseMutex();
                timer = (int)lastTime * 2;
                myHost.SetChannelHandlersTimer(myChannel.getConnectionInfo(), timer);
            }
            else
            {
                myHost.SendPing(conn, message);
            }

        }

        #endregion
        #endregion

        #region Private

        #region Timer event
        /// <summary>
        /// Sends a ping to the attached host or closes the connection
        /// </summary>
        /// <param name="source"></param>
        /// <param name="e"></param>
        private void OnPing(Object source, System.Timers.ElapsedEventArgs e)
        {

            clockMutex.WaitOne();
            contNotReplies = replied ? 0 : contNotReplies + 1;
            if (contNotReplies >= maxNotReplies)
            {
                clockMutex.ReleaseMutex();
                myHost.OnTimeOut(myChannel);

                return;
            }
            replied = false;
            responseWaiting++;

            MessageObject newPingMessage = new MessageObject(0, responseWaiting, 0, false, true, false, false, BitConverter.GetBytes(false));

            myChannel.SendPing(newPingMessage);
            clock.Restart();
            clockMutex.ReleaseMutex();

        }

        public void Dispose()
        {
            clockMutex.Dispose();
        }
        #endregion

        #endregion
    }
}
