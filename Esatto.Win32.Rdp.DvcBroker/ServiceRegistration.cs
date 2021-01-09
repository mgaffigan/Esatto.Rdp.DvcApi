using System;
using System.Runtime.InteropServices;
using System.Text;
using Esatto.Rdp.DvcApi.Broker;
using Esatto.Rdp.DvcApi;

namespace Esatto.Rdp.DvcBroker
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

        internal bool TryAcceptConnection(IAsyncDvcChannel connection)
        {
            AssertAlive();

            var connectionProxy = new ConnectionProxy(ServiceName, connection);
            try
            {
                connectionProxy.SetHandler(Target.AcceptConnection(connectionProxy.CallbackProxy));
                return true;
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x800706ba) /* RPC_S_SERVER_UNAVAILABLE */)
            {
                // caller needs to ditch this instance
                try
                {
                    connection.SendMessage(Encoding.UTF8.GetBytes("ERROR Service is no longer available"));
                }
                catch (OperationCanceledException) { /* no-op */ }
                catch (ObjectDisposedException) { /* no-op */ }
                catch (DvcChannelDisconnectedException) { /* no-op */ }
                catch (Exception ex2)
                {
                    Logger.Error(ex2, "Could not notify RDS endpoint of failed conncetion attempt");
                }

                // this will tear down connection, too.
                connectionProxy.Dispose();
                return false;
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
                return true;
            }
        }
    }
}