using Esatto.Rdp.DvcApi;
using Esatto.Rdp.DvcApi.Broker;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcBroker
{
    internal class ConnectionProxy : IDisposable
    {
        private readonly string ServiceName;
        private readonly IAsyncDvcChannel Connection;
        private IBrokeredDvcChannelServiceInstance Target;
        public IBrokeredDvcChannelConnection CallbackProxy { get; }

        private bool isDisposed;
        private Task ReadThread;

        public ConnectionProxy(string serviceName, IAsyncDvcChannel connection)
        {
            this.ServiceName = serviceName;
            this.Connection = connection;
            this.CallbackProxy = new ClientCallbackProxy(this);
            this.ReadThread = RunReadThread();
        }

        private async Task RunReadThread()
        {
            try
            {
                while (true)
                {
                    var message = await Connection.ReadMessageAsync().ConfigureAwait(false);
                    Target.HandleMessage(message);
                }
            }
            catch (ObjectDisposedException) { /* no-op */ }
            catch (OperationCanceledException) { /* no-op */ }
            catch (DvcChannelDisconnectedException) { /* no-op */ }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x800706ba) /* RPC_S_SERVER_UNAVAILABLE */)
            {
                Logger.Error(ex, "Service process has exited");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not notify service of a new message");
            }

            // Since this would otherwise cause a deadlock since we'd be waiting on our own thread
            ReadThread = Task.CompletedTask;
            // no-op if already disposing
            Dispose();
        }

        internal void SetHandler(IBrokeredDvcChannelServiceInstance target)
        {
            if (Target != null)
            {
                throw new InvalidOperationException();
            }

            this.Target = target;
            Connection.Disconnected += this.Connection_SenderDisconnected;
        }

        private void Connection_SenderDisconnected(object sender, EventArgs e)
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

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;

            try
            {
                Connection.Dispose();
            }
            catch (ObjectDisposedException) { /* no-op */ }
            catch (DvcChannelDisconnectedException) { /* no-op */ }

            try
            {
                ReadThread.Wait();
            }
            catch (AggregateException ae) 
                when (ae.InnerException is ObjectDisposedException
                    || ae.InnerException is DvcChannelDisconnectedException
                    || ae.InnerException is TaskCanceledException) 
            { 
                // no-op, should throw on dispose or disconnect
            }
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