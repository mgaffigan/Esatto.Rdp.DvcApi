using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    public abstract class WtsClientPlugin : IWTSPlugin
    {
        public virtual void Connected()
        {
        }

        public virtual void Disconnected(uint dwDisconnectCode)
        {
        }

        public virtual void Initialize(IWTSVirtualChannelManager pChannelMgr)
        {
        }

        public virtual void Terminated()
        {
        }
    }
}
