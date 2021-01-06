using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.TSVirtualChannels
{
    public struct CHANNEL_PDU_HEADER
    {
        public int length;
        public CHANNEL_FLAG flags;

        public static CHANNEL_PDU_HEADER FromBuffer(byte[] data, int offset)
        {
            return new CHANNEL_PDU_HEADER
            {
                length = BitConverter.ToInt32(data, offset),
                flags = (CHANNEL_FLAG)BitConverter.ToInt32(data, offset + 4)
            };
        }
    }
}
