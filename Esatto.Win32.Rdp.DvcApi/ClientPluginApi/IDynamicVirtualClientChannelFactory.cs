using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    public delegate void DynamicVirtualChannelWriteCallback(byte[] data, int offset, int count);

    public interface IDynamicVirtualClientChannelFactory
    {
        string ChannelName { get; }

        IDynamicVirtualClientChannel CreateChannel(DynamicVirtualChannelWriteCallback writeCallback, Action closeCallback);
    }
}
