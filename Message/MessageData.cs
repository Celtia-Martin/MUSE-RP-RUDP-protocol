using Muse_RP.Hosts;
using Muse_RP.Message;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Muse_RP.Utils
{
    #region Sender Message Data
    public class SenderMessageData
    {
        public byte[] data;
        public ushort type;
        public bool isAck;
        public bool isPing;
        public bool isInit;
        public bool isEnd;

        public SenderMessageData(byte[] data, ushort type, bool isAck, bool isPing, bool isInit, bool isEnd)
        {
            this.data = data;
            this.type = type;
            this.isAck = isAck;
            this.isPing = isPing;
            this.isInit = isInit;
            this.isEnd = isEnd;
        }
    }
    #endregion
    #region Receiver Message Data
    public class ReceiverMessageData
    {

        public byte[] data;
        public EndPoint senderInfo;

        public ReceiverMessageData(byte[] data, EndPoint senderInfo)
        {
            this.data = data;
            this.senderInfo = senderInfo;
        }
    }
    #endregion
    #region Upper Layer Message Data
    public class UpperLayerMessageData
    {
        public MessageObject message;
        public Connection source;

        public UpperLayerMessageData(MessageObject message, Connection source)
        {
            this.message = message;
            this.source = source;
        }
    }
    #endregion
    #region Received Data
    public class ReceivedData
    {
        public byte[] data;
        public uint sequence;

        public ReceivedData(byte[] data, uint sequence)
        {
            this.data = data;
            this.sequence = sequence;
        }

        public override bool Equals(object obj)
        {
            return obj is ReceivedData data &&
                   sequence == data.sequence;
        }

        public override int GetHashCode()
        {
            return -1159274584 + sequence.GetHashCode();
        }

    }
    #endregion
    #region CustomComparer
    public class MessageDataComparer : IComparer<ReceivedData>
    {
        private readonly uint windowSize;

        public MessageDataComparer(uint windowSize)
        {
            this.windowSize = windowSize;
        }

        public int Compare(ReceivedData x, ReceivedData y)
        {
            if ((uint.MaxValue - x.sequence < windowSize - 1) && (y.sequence < windowSize - 1))
            {

                return -1;
            }
            if ((uint.MaxValue - y.sequence < windowSize - 1) && (x.sequence < windowSize - 1))
            {

                return 1;
            }

            return x.sequence.CompareTo(y.sequence);
        }

    }
    #endregion
}
