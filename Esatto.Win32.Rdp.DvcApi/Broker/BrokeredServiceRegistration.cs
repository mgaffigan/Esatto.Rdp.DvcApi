using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.Broker
{
    public class BrokeredServiceRegistration : IDisposable
    {
        private IBrokeredDvcServiceRegistration Registration;
        private readonly Action<IRawDvcChannel> AcceptConnectionInternal;

        public BrokeredServiceRegistration(string serviceName, Action<IRawDvcChannel> acceptConnection)
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

        private class BrokeredDvcServiceProxy : IBrokeredDvcService
        {
            private readonly BrokeredServiceRegistration Parent;

            public BrokeredDvcServiceProxy(BrokeredServiceRegistration parent)
            {
                this.Parent = parent;
            }

            public IBrokeredDvcChannelServiceInstance AcceptConnection(IBrokeredDvcChannelConnection client)
            {
                return Parent.AcceptConnection(client);
            }

            public void Ping()
            {
                // no-op
            }
        }

        private IBrokeredDvcChannelServiceInstance AcceptConnection(IBrokeredDvcChannelConnection client)
        {
            var thunk = new ChannelThunk(client);
            AcceptConnectionInternal(thunk);
            return thunk;
        }

        private class ChannelThunk : IRawDvcChannel, IBrokeredDvcChannelServiceInstance
        {
            private readonly IBrokeredDvcChannelConnection Client;

            public ChannelThunk(IBrokeredDvcChannelConnection client)
            {
                this.Client = client ?? throw new ArgumentNullException(nameof(client));
            }

            public event EventHandler<CloseReceivedEventArgs> SenderDisconnected;
            public event EventHandler<UnhandledExceptionEventArgs> Exception;
            public event EventHandler<MessageReceivedEventArgs> MessageReceived;

            public void SendMessage(byte[] data)
                => Client.SendMessage(data);

            void IBrokeredDvcChannelServiceInstance.HandleClientDisconnected()
                => SenderDisconnected?.Invoke(this, new CloseReceivedEventArgs());

            void IBrokeredDvcChannelServiceInstance.HandleMessage(byte[] message)
                => MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
        }

        public void Dispose()
        {
            Registration.Unregister();
        }
    }
}
