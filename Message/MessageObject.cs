using Muse_RP.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Muse_RP.Message
{
    [Serializable]
    public struct MessageObject
    {
        //Properties

        public static readonly int cabeceraSize = 11;
        public static readonly int maxBytesData = 1500;

        //Header

        private ushort type; //2 bytes
        private uint sequenceNumber; //4 bytes
        private uint ackNumber; //4 bytes

        //Flags
   
        private BitArray flags;
        private byte[] flagsToByte;//1 byte

        //Data
        private byte[] data;

        #region Constructors
        public MessageObject(ushort type, uint sequenceNumber, uint ackNumber, bool isAck, bool isPing, bool isInit, bool isEnd, byte[] data)
        {
            if (data != null && data.Length > maxBytesData)
            {
                throw new OversizedMessageDataException("Oversized Message Data");
            }
            flags = new BitArray(new bool[] { isAck, isInit, isEnd, isPing, false, false, false, false });
            flagsToByte = new byte[1];
            flags.CopyTo(flagsToByte, 0);
            this.type = type;
            this.sequenceNumber = sequenceNumber;
            this.ackNumber = ackNumber;
            this.data = (!(data == null)) ? data : new byte[0];

        }
        public MessageObject(byte[] package) //Deserializacion
        {
            if (package.Length < cabeceraSize)
            {
                throw new IncorrectMessageFormatException("Incorrect Message Format: Not Enough Bytes");
            }
            if (package.Length > cabeceraSize)
            {
                data = package.Skip(cabeceraSize).ToArray();
            }
            else
            {
                data = null;
            }
            try
            {
                sequenceNumber = BitConverter.ToUInt32(package.Take(4).ToArray(), 0);
                ackNumber = BitConverter.ToUInt32(package.Skip(4).Take(4).ToArray(), 0);
                flagsToByte = new byte[1];
                flagsToByte[0] = package[8];
                flags = new BitArray(flagsToByte);
                type = BitConverter.ToUInt16(package.Skip(9).Take(2).ToArray(), 0);

            }
            catch (Exception e)
            {
                throw new IncorrectMessageFormatException("Incorrect Header Format");
            }


        }
        #endregion
        #region Setters and Getters
        public ushort getType() { return type; }
        public bool isAck()
        {
            return flags.Get(0);
        }
        public bool isInit()
        {
            return flags.Get(1);
        }
        public bool isEnd()
        {
            return flags.Get(2);
        }

        public bool isPing()
        {
            return flags.Get(3);
        }
        public uint getSequenceNumber() { return sequenceNumber; }
        public void setSequenceNumber(uint sequenceNumber) { this.sequenceNumber = sequenceNumber; }
        public void setAck(uint ack) { this.ackNumber = ack; }
        public void setData(byte[] data) { this.data = data; }
        public uint getAck() { return ackNumber; }
        public byte[] getData() { return data; }

        #endregion
        #region To byte[]
        /// <summary>
        /// Gets the message in bytes
        /// </summary>
        /// <returns>The message to bytes</returns>
        public byte[] toByteArray()
        {
            int size = ((data != null)) ? data.Length : 0;
            byte[] package = new byte[cabeceraSize + size];
            package[0] = (byte)sequenceNumber;
            package[1] = (byte)(sequenceNumber >> 8);
            package[2] = (byte)(sequenceNumber >> 16);
            package[3] = (byte)(sequenceNumber >> 24);
            package[4] = (byte)ackNumber;
            package[5] = (byte)(ackNumber >> 8);
            package[6] = (byte)(ackNumber >> 16);
            package[7] = (byte)(ackNumber >> 24);
            package[8] = flagsToByte[0];
            package[9] = (byte)type;
            package[10] = (byte)(type >> 8);
            if (data != null)
            {
                data.CopyTo(package, cabeceraSize);
            }
            return package;

        }
        #endregion
        #region Equals and Hash
        public override bool Equals(object obj)
        {
            return obj is MessageObject @object &&
                   sequenceNumber == @object.sequenceNumber;
        }

        public override int GetHashCode()
        {
            return 1435870881 + sequenceNumber.GetHashCode();
        }
        #endregion

    }
    #region CustomComparer
    public class MessageObjectComparer : IComparer<MessageObject>
    {
        private readonly uint windowSize;
        public MessageObjectComparer(uint windowSize)
        {
            this.windowSize = windowSize;
        }
        public int Compare(MessageObject x, MessageObject y)
        {
            if ((uint.MaxValue - x.getSequenceNumber() < windowSize - 1) && (y.getSequenceNumber() < windowSize - 1))
            {
                return -1;
            }
            if ((uint.MaxValue - y.getSequenceNumber() < windowSize - 1) && (x.getSequenceNumber() < windowSize - 1))
            {
                return 1;
            }
            return x.getSequenceNumber().CompareTo(y.getSequenceNumber());
        }
    }
    #endregion

}
