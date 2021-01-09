using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Esatto.Rdp.DvcApi.TSVirtualChannels.NativeMethods;

namespace Esatto.Rdp.DvcApi.TSVirtualChannels
{
    class WtsVitrualChannelSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WtsVitrualChannelSafeHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            return WTSVirtualChannelClose(handle);
        }
    }
}
