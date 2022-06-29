using System;
using System.Collections.Generic;
using System.Text;

namespace Muse_RP.Exceptions
{
    [Serializable]
    public class IncorrectMessageFormatException : Exception
    {
        public IncorrectMessageFormatException() : base() { }
        public IncorrectMessageFormatException(string message) : base(message) { }
        public IncorrectMessageFormatException(string message, Exception inner) : base(message, inner) { }


        protected IncorrectMessageFormatException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
