using Muse_RP.Hosts;
using Muse_RP.Message;
using Muse_RP.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Muse_RP.Channels
{
    public class ReliableChannelHandler : ChannelHandler
    {

        #region Constructor
        public ReliableChannelHandler(Host myHost, uint windowSize, uint currentSequenceNumber, uint currentAck, int waitTime, int timerTime, EndPoint endPoint, ConnectionInfo connectionInfo) : base(myHost, windowSize, currentSequenceNumber, currentAck, waitTime, timerTime, endPoint, connectionInfo)
        {

        }
        #endregion
        #region Custom Receive( Reliable algorithm )
        public override void Receive(MessageObject message)
        {

            uint sequence = message.getSequenceNumber();

            currentAckMutex.WaitOne();
            if (sequence < currentAck)
            {
                if (currentAck == uint.MaxValue && sequence == 0)
                {
                    currentAck = sequence;
                    SuccessMessage(message);
                }
                else if (uint.MaxValue - currentAck < windowSize && (uint.MaxValue - currentAck) + sequence <= windowSize)
                {
                    receiverBuffer.AddUnique(message);
                }

            }
            else if (sequence > currentAck + 1) 
            {
                if (sequence - currentAck <= windowSize)
                {
                    receiverBuffer.AddUnique(message);
                }

            }
            else if (sequence == currentAck + 1)
            {
                currentAck = sequence;

                SuccessMessage(message);

            }
            currentAckMutex.ReleaseMutex();


        }

    }
    #endregion
}
