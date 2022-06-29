using Muse_RP.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Muse_RP.Utils
{
    public struct ChannelInfo
    {
        //Properties
        public int reliablePort;
        public int noReliablePort;

        public uint windowSize;
        public uint reliablePercentage;

        public int yourReliablePort;
        public int yourNoReliablePort;

        public int ID;

        #region Constructors
        public ChannelInfo(byte[] data)
        {
            if ((data.Length != 28))
            {
                throw new IncorrectMessageFormatException();

            }
            reliablePort = BitConverter.ToInt32(data.Take(4).ToArray(), 0);
            noReliablePort = BitConverter.ToInt32(data.Skip(4).Take(4).ToArray(), 0);
            windowSize = BitConverter.ToUInt32(data.Skip(8).Take(4).ToArray(), 0);
            reliablePercentage = BitConverter.ToUInt32(data.Skip(12).Take(4).ToArray(), 0);
            yourReliablePort = BitConverter.ToInt32(data.Skip(16).Take(4).ToArray(), 0);
            yourNoReliablePort = BitConverter.ToInt32(data.Skip(20).Take(4).ToArray(), 0);
            ID = BitConverter.ToInt32(data.Skip(24).Take(4).ToArray(), 0);

        }
        public ChannelInfo(int reliablePort, int noReliablePort, uint windowSize, uint reliablePercentage, int yourReliablePort, int yourNoReliablePort, int ID)
        {
            this.reliablePort = reliablePort;
            this.noReliablePort = noReliablePort;
            this.windowSize = windowSize;
            this.reliablePercentage = reliablePercentage;
            this.yourReliablePort = yourReliablePort;
            this.yourNoReliablePort = yourNoReliablePort;
            this.ID = ID;
        }
        #endregion
        #region To Bytes
        /// <summary>
        /// Returns the channelInfo object to byte array
        /// </summary>
        /// <returns>The channel info in bytes</returns>
        public byte[] getBytes()
        {
            byte[] package = new byte[28];

            package[0] = (byte)reliablePort;
            package[1] = (byte)(reliablePort >> 8);
            package[2] = (byte)(reliablePort >> 16);
            package[3] = (byte)(reliablePort >> 24);

            package[4] = (byte)noReliablePort;
            package[5] = (byte)(noReliablePort >> 8);
            package[6] = (byte)(noReliablePort >> 16);
            package[7] = (byte)(noReliablePort >> 24);

            package[8] = (byte)windowSize;
            package[9] = (byte)(windowSize >> 8);
            package[10] = (byte)(windowSize >> 16);
            package[11] = (byte)(windowSize >> 24);

            package[12] = (byte)reliablePercentage;
            package[13] = (byte)(reliablePercentage >> 8);
            package[14] = (byte)(reliablePercentage >> 16);
            package[15] = (byte)(reliablePercentage >> 24);

            package[16] = (byte)yourReliablePort;
            package[17] = (byte)(yourReliablePort >> 8);
            package[18] = (byte)(yourReliablePort >> 16);
            package[19] = (byte)(yourReliablePort >> 24);


            package[20] = (byte)yourNoReliablePort;
            package[21] = (byte)(yourNoReliablePort >> 8);
            package[22] = (byte)(yourNoReliablePort >> 16);
            package[23] = (byte)(yourNoReliablePort >> 24);

            package[24] = (byte)ID;
            package[25] = (byte)(ID >> 8);
            package[26] = (byte)(ID >> 16);
            package[27] = (byte)(ID >> 24);

            return package;
        }
        #endregion



    }
}
