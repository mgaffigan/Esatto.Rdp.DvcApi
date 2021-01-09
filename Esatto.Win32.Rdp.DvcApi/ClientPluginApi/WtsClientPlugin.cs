#define TRACE

using Esatto.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esatto.Rdp.DvcApi.ClientPluginApi
{
    // Runs on RDS Client
    // Class called by MSTSC to control plugin lifetime
    internal sealed class WtsClientPlugin : IWTSPlugin
    {
        private readonly IReadOnlyDictionary<string, Action<IAsyncDvcChannel>> Registrations;
        private readonly List<WtsListenerCallback> Callbacks;

        public WtsClientPlugin(Dictionary<string, Action<IAsyncDvcChannel>> registeredServices)
        {
            this.Registrations = new ReadOnlyDictionary<string, Action<IAsyncDvcChannel>>(registeredServices);
            this.Callbacks = new List<WtsListenerCallback>(registeredServices.Count);
        }

        public void Initialize(IWTSVirtualChannelManager pChannelMgr)
        {
            foreach (var registration in Registrations)
            {
                try
                {
                    var callback = new WtsListenerCallback(registration.Key, registration.Value);
                    // keep a reference out of paranoia
                    Callbacks.Add(callback);
                    pChannelMgr.CreateListener(callback.ChannelName, 0, callback);
                }
                catch (Exception ex)
                {
                    PluginApplication.Log($"Failed to create listener for '{registration.Key}': {ex}");
                }
            }
        }

        public void Connected()
        {
            // no-op
        }

        public void Disconnected(uint dwDisconnectCode)
        {
            // no-op
        }

        public void Terminated()
        {
            // no-op
        }
    }
}
