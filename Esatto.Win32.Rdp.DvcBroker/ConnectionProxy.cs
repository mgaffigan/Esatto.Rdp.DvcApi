using Esatto.Win32.Rdp.DvcApi.Broker;
using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcBroker
{
    internal class ConnectionProxy : IDisposable
    {
        private readonly string ServiceName;
        private readonly RawDynamicVirtualClientChannel Connection;
        private IBrokeredDvcChannelServiceInstance Target;
        public IBrokeredDvcChannelConnection CallbackProxy { get; }

        public ConnectionProxy(string serviceName, RawDynamicVirtualClientChannel connection)
        {
            this.ServiceName = serviceName;
            this.Connection = connection;
            this.CallbackProxy = new ClientCallbackProxy(this);
        }

        internal void SetHandler(IBrokeredDvcChannelServiceInstance target)
        {
            if (Target != null)
            {
                throw new InvalidOperationException();
            }

            this.Target = target;
            Connection.MessageReceived += this.Connection_MessageReceived;
            Connection.SenderDisconnected += this.Connection_SenderDisconnected;
            Connection.Exception += this.Connection_Exception;
        }

        private void Connection_Exception(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error((Exception)e.ExceptionObject, "Exception on read thread");
        }

        private void Connection_SenderDisconnected(object sender, CloseReceivedEventArgs e)
        {
            try
            {
                Target.HandleClientDisconnected();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not notify service that client has disconnected");
            }
        }

        private void Connection_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            try
            {
                Target.HandleMessage(e.Message);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not notify service of a new message");
            }
        }

        public void Dispose()
        {
            Connection.Close();
        }

        private class ClientCallbackProxy : IBrokeredDvcChannelConnection
        {
            private ConnectionProxy Parent;

            public ClientCallbackProxy(ConnectionProxy connectionProxy)
            {
                this.Parent = connectionProxy;
            }

            public void SendMessage(byte[] message)
            {
                Parent.Connection.SendMessage(message);
            }
        }
    }
}