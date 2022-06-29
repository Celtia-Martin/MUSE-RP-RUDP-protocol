using Muse_RP.Hosts;
using System;
using System.Collections.Generic;
using System.Text;

namespace Muse_RP.Message
{
    public class MessageHandler
    {
        //Storage
        private readonly Dictionary<ushort, MessageDelegate> handlers;

        #region Constructor
        public MessageHandler()
        {
            handlers = new Dictionary<ushort, MessageDelegate>();
        }
        #endregion
        #region Add Remove
        public void AddHandler(ushort type, MessageDelegate messageDelegate)
        {
            if (handlers.TryGetValue(type, out MessageDelegate value))
            {
                value += messageDelegate;
                handlers.Remove(type);
                handlers.Add(type, value);
            }
            else
            {
                handlers.Add(type, messageDelegate);
            }
        }
        public bool RemoveHandler(ushort type)
        {
            if (handlers.TryGetValue(type, out MessageDelegate value))
            {
                handlers.Remove(type);
                return true;
            }
            return false;
        }
        #endregion
        #region Invoke
        public bool InvokeHandler(ushort type, MessageObject message, Connection source)
        {
            if (handlers.TryGetValue(type, out MessageDelegate value))
            {
                value.Invoke(message, source);
                return true;
            }
            else
            {
                return false;
            }
        }
        #endregion

    }
}
