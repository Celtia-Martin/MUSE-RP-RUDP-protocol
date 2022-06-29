using System;
using System.Collections.Generic;
using System.Text;

namespace Muse_RP.Exceptions
{

    [Serializable]
    public class OversizedMessageDataException : Exception
    {

        public OversizedMessageDataException(string message) : base(message) { }
        public OversizedMessageDataException(string message, Exception inner) : base(message, inner) { }


        protected OversizedMessageDataException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }


}
