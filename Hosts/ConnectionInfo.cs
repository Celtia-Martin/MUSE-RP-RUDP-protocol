using Muse_RP.Message;
using System;
using System.Collections.Generic;
using System.Text;

namespace Muse_RP.Hosts
{
    public class ConnectionInfo
    {
        public readonly string IP;
        public int reliablePort;
        public int noReliablePort;

        #region Constructor
        public ConnectionInfo(string iP, int reliablePort, int noReliablePort)
        {
            IP = iP;
            this.reliablePort = reliablePort;
            this.noReliablePort = noReliablePort;
        }

        #endregion
        #region Equals and Hashcode
        public override bool Equals(object obj)
        {
            return obj is ConnectionInfo info &&
                   IP == info.IP &&
                   reliablePort == info.reliablePort &&
                   noReliablePort == info.noReliablePort;
        }

        public override int GetHashCode()
        {
            int hashCode = 544969203;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(IP);
            hashCode = hashCode * -1521134295 + reliablePort.GetHashCode();
            hashCode = hashCode * -1521134295 + noReliablePort.GetHashCode();
            return hashCode;
        }
        #endregion
        #region Overrides
        public new string ToString()
        {
            return IP + reliablePort + noReliablePort;
        }
        #endregion


    }
}
