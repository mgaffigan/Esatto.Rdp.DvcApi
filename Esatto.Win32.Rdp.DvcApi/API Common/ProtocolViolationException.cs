﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcApi
{
    [Serializable]
    public class ProtocolViolationException : FormatException
    {
        public ProtocolViolationException() { }
        public ProtocolViolationException(string message) : base(message) { }
        public ProtocolViolationException(string message, Exception inner) : base(message, inner) { }
        protected ProtocolViolationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
