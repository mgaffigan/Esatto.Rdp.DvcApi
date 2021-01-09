using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.Broker
{
    // Runs on RDS Client in Client app (not plugin process)
    public class BrokeredServiceRegistration : IDisposable
    {
        private IBrokeredDvcServiceRegistration Registration;
        private readonly Action<IAsyncDvcChannel> AcceptConnectionInternal;

        public BrokeredServiceRegistration(string serviceName, Action<IAsyncDvcChannel> acceptConnection)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName));
            }
            if (acceptConnection == null)
            {
                throw new ArgumentNullException(nameof(acceptConnection));
            }

            var broker = (IDvcChannelBroker)Com.ComInterop.CreateLocalServer(Guid.Parse(DvcBrokerConstants.BrokerClsid));
            this.Registration = broker.RegisterService(serviceName, new BrokeredDvcServiceProxy(this));
            this.AcceptConnectionInternal = acceptConnection;
        }

        // Thunk from broker
        private class BrokeredDvcServiceProxy : IBrokeredDvcService
        {
            private readonly BrokeredServiceRegistration Parent;

            public BrokeredDvcServiceProxy(BrokeredServiceRegistration parent)
            {
                this.Parent = parent;
            }

            // Called by COM
            public IBrokeredDvcChannelServiceInstance AcceptConnection(IBrokeredDvcChannelConnection client)
            {
                try
                {
                    var thunk = new ChannelThunk(client);
                    Parent.AcceptConnectionInternal(thunk);
                    return thunk;
                }
                catch (Exception ex)
                {
                    DynamicVirtualClientApplication.Log($"Exception accepting connection: {ex}");
                    return null;
                }
            }

            // Called by COM
            public void Ping()
            {
                // no-op
            }
        }

        // Thunk from IBrokeredDvcChannelServiceInstance to IAsyncDvcChannel
        // Thunk from IAsyncDvcChannel to IBrokeredDvcChannelConnection
        private class ChannelThunk : IAsyncDvcChannel, IBrokeredDvcChannelServiceInstance
        {
            private readonly IBrokeredDvcChannelConnection Client;
            private readonly MessageQueue ReadQueue;

            private bool isDisposed;
            private bool isDisconnected;
            public event EventHandler Disconnected;

            public ChannelThunk(IBrokeredDvcChannelConnection client)
            {
                this.Client = client ?? throw new ArgumentNullException(nameof(client));
                this.ReadQueue = new MessageQueue();
            }

            public void Dispose()
            {
                AssertAlive();
                isDisposed = true;

                ReadQueue.Dispose();
            }

            private void AssertAlive()
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(BrokeredServiceRegistration));
                }
            }

            // Sugar
            public byte[] ReadMessage(CancellationToken ct) => ReadMessageAsync(ct).GetResultOrException();
            public Task SendMessageAsync(byte[] data, int offset, int length)
                => Task.Run(() => SendMessage(data, offset, length));

            //static int i = 0;

            // Called by app code
            public async Task<byte[]> ReadMessageAsync(CancellationToken ct)
            {
                AssertAlive();
                if (isDisconnected)
                {
                    throw new DvcChannelDisconnectedException();
                }

                var res = await ReadQueue.ReadAsync(ct).ConfigureAwait(false);
                //System.IO.File.WriteAllBytes($"n{i++}_read.txt", res);
                return res;
            }

            // Called by app code
            public void SendMessage(byte[] data, int offset, int length)
            {
                AssertAlive();
                if (isDisconnected)
                {
                    throw new DvcChannelDisconnectedException();
                }

                if (offset != 0 || length != data.Length)
                {
                    var arr = new byte[length];
                    Buffer.BlockCopy(data, offset, arr, 0, length);
                    data = arr;
                }

                //System.IO.File.WriteAllBytes($"n{i++}_write.txt", data);
                Client.SendMessage(data);
            }

            // Called by COM
            void IBrokeredDvcChannelServiceInstance.HandleClientDisconnected()
            {
                if (isDisposed)
                {
                    return;
                }
                isDisconnected = true;

                Dispose();
                Disconnected?.Invoke(this, EventArgs.Empty);
            }

            // Called by COM
            void IBrokeredDvcChannelServiceInstance.HandleMessage(byte[] message)
            {
                if (isDisposed)
                {
                    return;
                }

                ReadQueue.WriteMessage(message);
            }
        }

        public void Dispose()
        {
            Registration.Unregister();
        }
    }
}
