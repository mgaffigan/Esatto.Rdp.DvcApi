using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.Broker
{
    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDvcChannelBroker
    {
        IBrokeredDvcServiceRegistration RegisterService(string name, IBrokeredDvcService service);
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBrokeredDvcServiceRegistration
    {
        void Unregister();
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBrokeredDvcService
    {
        void Ping();
        IBrokeredDvcChannelServiceInstance AcceptConnection(IBrokeredDvcChannelConnection cleitn);
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBrokeredDvcChannelServiceInstance
    {
        void HandleMessage(byte[] message);
        void HandleClientDisconnected();
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IBrokeredDvcChannelConnection
    {
        void SendMessage(byte[] message);
    }
}