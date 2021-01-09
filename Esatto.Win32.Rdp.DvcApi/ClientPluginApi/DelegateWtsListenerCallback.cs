#define TRACE

using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    // Runs on RDS Client to accept incomming connections
    // Thunk from mstsc Plugin to handle a new connection attempt
    internal sealed class DelegateWtsListenerCallback : IWTSListenerCallback
    {
        public string ChannelName { get; }
        private readonly Action<IAsyncDvcChannel> AcceptChannel;

        public DelegateWtsListenerCallback(string channelName, Action<IAsyncDvcChannel> handleAccept)
        {
            NativeMethods.ValidateChannelName(channelName);

            this.ChannelName = channelName;
            this.AcceptChannel = handleAccept ?? throw new ArgumentNullException(nameof(handleAccept));
        }

        // Called from COM
        void IWTSListenerCallback.OnNewChannelConnection(IWTSVirtualChannel pChannel,
            [MarshalAs(UnmanagedType.BStr)] string data,
            [MarshalAs(UnmanagedType.Bool)] out bool pAccept, out IWTSVirtualChannelCallback pCallback)
        {
            try
            {
                var channel = new RawDynamicVirtualClientChannel(ChannelName, pChannel);
                AcceptChannel(channel);

                pAccept = true;
                pCallback = channel.Proxy;
            }
            catch (Exception ex)
            {
                DynamicVirtualClientApplication.Log($"Failure while creating client channel for '{ChannelName}': {ex}");

                pAccept = false;
                pCallback = null;
            }
        }
    }
}
