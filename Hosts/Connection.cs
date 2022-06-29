using Muse_RP.Channels;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Muse_RP.Hosts
{
    public class Connection
    {
        public int ID;
        public readonly string IP;
        public readonly int port;
        public bool reliable;
        public EndPoint endPoint;

        #region Constructors
        public Connection(EndPoint endPoint, bool reliable)
        {
            this.endPoint = endPoint;
            IPEndPoint iPEnd = endPoint as IPEndPoint;
            IP = iPEnd.Address.ToString(); ;
            this.port = iPEnd.Port;
            this.reliable = reliable;

        }

        public Connection(string iP, int port, bool reliable)
        {
            IP = iP;
            this.port = port;
            this.reliable = reliable;
        }
        #endregion

        #region Equals and HashCode
        public override bool Equals(object obj)
        {
            return obj is Connection connection &&
                   IP == connection.IP &&
                   port == connection.port;
        }

        public override int GetHashCode()
        {
            int hashCode = 1658706914;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(IP);
            hashCode = hashCode * -1521134295 + port.GetHashCode();
            return hashCode;
        }
        #endregion
        #region Overrides
        public new string ToString()
        {
            return IP + port;
        }

        #endregion
    }

}
