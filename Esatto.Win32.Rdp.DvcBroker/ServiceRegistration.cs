using Esatto.Win32.Rdp.DvcApi.Broker;
using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;

namespace Esatto.Win32.Rdp.DvcApi.Broker
{
    internal class ServiceRegistration : IDisposable
    {
        public string ServiceName { get; }
        public IBrokeredDvcServiceRegistration Proxy { get; }
        private IBrokeredDvcService Target;

        public ServiceRegistration(string name, IBrokeredDvcService service)
        {
            this.ServiceName = name;
            this.Target = service;
        }

        internal bool CheckIsAlive()
        {
            try
            {
                Target.Ping();
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Could not ping {ServiceName}");

                Target = null;
                return false;
            }
        }

        internal void AssertAlive()
        {
            if (Target == null)
            {
                throw new ObjectDisposedException("Channel has disconnected");
            }
        }

        public void Dispose()
        {
            Target = null;
        }

        internal void AcceptConnection(RawDynamicVirtualClientChannel obj)
        {
            AssertAlive();

            var connectionProxy = new ConnectionProxy(ServiceName, obj);
            try
            {
                connectionProxy.SetHandler(Target.AcceptConnection(connectionProxy.CallbackProxy));
            }
            catch (Exception ex)
            {
                try
                {
                    connectionProxy.Dispose();
                }
                catch (Exception ex2)
                {
                    Logger.Error(ex2, $"Could not tear down new connection for {ServiceName}");
                }
                Logger.Error(ex, $"Could not handle new connection for {ServiceName}");
            }
        }
    }
}