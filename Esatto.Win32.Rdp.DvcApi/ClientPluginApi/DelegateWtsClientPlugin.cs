#define TRACE

using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Collections.Generic;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    sealed class DelegateWtsClientPlugin : WtsClientPlugin
    {
        private readonly IDynamicVirtualClientChannelFactory[] ChannelHosts;
        private readonly List<DelegateWtsListenerCallback> Callbacks;

        public DelegateWtsClientPlugin(IDynamicVirtualClientChannelFactory[] channelHosts)
        {
            this.ChannelHosts = channelHosts;
            this.Callbacks = new List<DelegateWtsListenerCallback>(channelHosts.Length);
        }

        public override void Initialize(IWTSVirtualChannelManager pChannelMgr)
        {
            foreach (var chFactory in ChannelHosts)
            {
                try
                {
                    var callback = new DelegateWtsListenerCallback(chFactory);
                    // keep a reference out of paranoia
                    Callbacks.Add(callback);
                    pChannelMgr.CreateListener(chFactory.ChannelName, 0, callback);
                }
                catch (Exception ex)
                {
                    DynamicVirtualClientApplication.Log($"Failed to create listener for '{chFactory.ChannelName}': {ex}");
                }
            }
        }
    }
}
