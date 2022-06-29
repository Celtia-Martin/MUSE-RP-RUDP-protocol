using Muse_RP.Hosts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Muse_RP.Message
{
    public delegate void MessageDelegate(MessageObject message, Connection source);
    public delegate void InitMessageDelegate(MessageObject message, Connection source, bool reliable);
    public delegate void OnConnectedDelegate();
    public delegate void OnClientConnectedDelegate(ConnectionInfo client);
    public delegate void OnClientDisconnected(ConnectionInfo client);
    public delegate void OnServerDisconneced();
    public delegate void OnConnectionFailure();
}
