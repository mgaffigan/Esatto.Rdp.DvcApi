using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.TSVirtualChannels
{
    public enum CHANNEL_FLAG : uint
    {
        First = 0x01,
        Last = 0x02,
        Only = First | Last,
        None = 0
    }
}
