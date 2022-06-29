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
    public class PartialReliableHandler : ChannelHandler
    {
        //Properties

        private readonly uint maxFails;

        //State

        private uint currentFails;
        private uint counting;


        #region Constructor
        public PartialReliableHandler(Host myHost, uint windowSize, uint currentSequenceNumber, uint currentAck, int waitTime, int timerTime, EndPoint endPoint, ConnectionInfo connectionInfo, uint maxFailsPercentage) : base(myHost, windowSize, currentSequenceNumber, currentAck, waitTime, timerTime, endPoint, connectionInfo)
        {
            maxFails = (uint)((float)windowSize * ((float)(100 - maxFailsPercentage) / 100f));
            currentFails = 0;
            counting = 0;

        }

        #endregion
        #region Custom Receive ( Partial Reliable algorithm)
        public override void Receive(MessageObject message)
        {

            uint sequence = message.getSequenceNumber();
            currentAckMutex.WaitOne();
            uint distance = sequence - (currentAck + 1);

            if (sequence <= currentAck) 
            {

                if (uint.MaxValue - currentAck <= windowSize && (uint.MaxValue - currentAck) + sequence <= windowSize)
                {
                    distance = (uint.MaxValue - currentAck) + sequence;
                }
                else
                {
                    currentAckMutex.ReleaseMutex();
                    return;
                }

            }

            if (distance == 0) 
            {
                counting++;
                if (counting >= windowSize)
                {
                    counting = 0;
                    currentFails = 0;
                }
                currentAck = sequence;
                currentAckMutex.ReleaseMutex();

                SuccessMessage(message);


                return;
            }
            if (distance > 0)
            {
                uint newFails;
                if (distance + counting + 1 > windowSize)
                {
                    uint h1 = windowSize - counting;
                    uint h2 = distance - h1;
                    newFails = currentFails + h1;
                    if (newFails <= maxFails && h2 <= maxFails)
                    {
                        counting = h2 + 1;
                        currentFails = h2;
                        currentAck = sequence;
                        currentAckMutex.ReleaseMutex();
                        SuccessMessage(message);
                        return;
                    }
                    else
                    {
                        currentAckMutex.ReleaseMutex();
                        receiverBuffer.AddUnique(message);

                        return;
                    }
                }
                else 
                {
                    newFails = currentFails + distance;
                    if (newFails > maxFails)
                    {
                        currentAckMutex.ReleaseMutex();
                        receiverBuffer.AddUnique(message);

                        return;
                    }
                    else
                    {
                        currentFails = newFails;
                        counting += distance + 1;
                        currentAck = sequence;
                        currentAckMutex.ReleaseMutex();
                        if (counting == windowSize)
                        {
                            counting = 0;
                            currentFails = 0;
                        }
                        SuccessMessage(message);
                        return;
                    }
                }
            }
            currentAckMutex.ReleaseMutex();
        }

    }
    #endregion
}
