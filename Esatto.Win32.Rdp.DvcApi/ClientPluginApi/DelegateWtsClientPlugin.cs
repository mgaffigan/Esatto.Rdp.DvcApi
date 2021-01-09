#define TRACE

using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    // Runs on RDS Client
    // Class called by MSTSC to control plugin lifetime
    internal sealed class DelegateWtsClientPlugin : IWTSPlugin
    {
        private readonly IReadOnlyDictionary<string, Action<IAsyncDvcChannel>> Registrations;
        private readonly List<DelegateWtsListenerCallback> Callbacks;

        public DelegateWtsClientPlugin(Dictionary<string, Action<IAsyncDvcChannel>> registeredServices)
        {
            this.Registrations = new ReadOnlyDictionary<string, Action<IAsyncDvcChannel>>(registeredServices);
            this.Callbacks = new List<DelegateWtsListenerCallback>(registeredServices.Count);
        }

        public void Initialize(IWTSVirtualChannelManager pChannelMgr)
        {
            foreach (var registration in Registrations)
            {
                try
                {
                    var callback = new DelegateWtsListenerCallback(registration.Key, registration.Value);
                    // keep a reference out of paranoia
                    Callbacks.Add(callback);
                    pChannelMgr.CreateListener(callback.ChannelName, 0, callback);
                }
                catch (Exception ex)
                {
                    DynamicVirtualClientApplication.Log($"Failed to create listener for '{registration.Key}': {ex}");
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
