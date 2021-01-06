#define TRACE

using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Runtime.InteropServices;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    sealed class DelegateWtsListenerCallback : IWTSListenerCallback
    {
        private readonly IDynamicVirtualClientChannelFactory Factory;

        public DelegateWtsListenerCallback(IDynamicVirtualClientChannelFactory factory)
        {
            this.Factory = factory;
        }

        public void OnNewChannelConnection(IWTSVirtualChannel pChannel,
            [MarshalAs(UnmanagedType.BStr)] string data,
            [MarshalAs(UnmanagedType.Bool)] out bool pAccept, out IWTSVirtualChannelCallback pCallback)
        {
            try
            {
                var proxy = new DelegateWtsVirtualChannelCallback(pChannel, Factory);
                proxy.ReadChannel = Factory.CreateChannel(proxy.WriteMessage, proxy.CloseWriteChannel);

                pAccept = true;
                pCallback = proxy;
            }
            catch (Exception ex)
            {
                Log($"Failure while creating client channel for '{Factory.ChannelName}': {ex}");

                pAccept = false;
                pCallback = null;
            }
        }
    }
}
