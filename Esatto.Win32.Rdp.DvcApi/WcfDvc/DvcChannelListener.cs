using Esatto.Rdp.DvcApi.Broker;
using Esatto.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcApi.WcfDvc
{
    class DvcChannelListener : ChannelListenerBase<IDuplexSessionChannel>
    {
        private readonly int maxBufferSize;
        BufferManager bufferManager;
        MessageEncoderFactory encoderFactory;
        BrokeredServiceRegistration serviceRegistration;
        BlockingCollection<IAsyncDvcChannel> acceptQueue;
        public override Uri Uri { get; }

        public DvcChannelListener(DvcBindingElement bindingElement, BindingContext context)
            : base(context.Binding)
        {
            // populate members from binding element
            this.maxBufferSize = (int)bindingElement.MaxReceivedMessageSize;
            this.bufferManager = BufferManager.CreateBufferManager(bindingElement.MaxBufferPoolSize, maxBufferSize);
            this.acceptQueue = new BlockingCollection<IAsyncDvcChannel>();

            var messageEncoderBindingElement = context.BindingParameters.OfType<MessageEncodingBindingElement>().SingleOrDefault();
            if (messageEncoderBindingElement != null)
            {
                this.encoderFactory = messageEncoderBindingElement.CreateMessageEncoderFactory();
            }
            else
            {
                this.encoderFactory = new MtomMessageEncodingBindingElement().CreateMessageEncoderFactory();
            }

            this.Uri = new Uri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
        }

        #region Open / Close

        protected override void OnOpen(TimeSpan timeout)
        {
            this.serviceRegistration = new BrokeredServiceRegistration(this.Uri.PathAndQuery, o => acceptQueue.Add(o));
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            OnOpen(timeout);
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result) => CompletedAsyncResult.End(result);

        private void CloseListenSocket(TimeSpan timeout)
        {
            this.serviceRegistration?.Dispose();
            this.serviceRegistration = null;
        }

        protected override void OnAbort() => CloseListenSocket(TimeSpan.Zero);

        protected override void OnClose(TimeSpan timeout) => CloseListenSocket(timeout);

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            CloseListenSocket(timeout);
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndClose(IAsyncResult result) => CompletedAsyncResult.End(result);

        #endregion

        #region Accept

        protected override IDuplexSessionChannel OnAcceptChannel(TimeSpan timeout)
        {
            var dataSocket = acceptQueue.Take();
            return new ServerDuplexSessionChannel(this.encoderFactory, this.bufferManager, this.maxBufferSize, dataSocket, new EndpointAddress(Uri), this);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Tap.Run(callback, state, () => Task.Run(() =>
            {
                try
                {
                    var dataSocket = acceptQueue.Take();
                    return (IDuplexSessionChannel)new ServerDuplexSessionChannel(this.encoderFactory, this.bufferManager, 
                        this.maxBufferSize, dataSocket, new EndpointAddress(this.Uri), this);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    return null;
                }
            }));
        }

        protected override IDuplexSessionChannel OnEndAcceptChannel(IAsyncResult result) => Tap.Complete<IDuplexSessionChannel>(result);

        #endregion

        #region WaitForChannel

        protected override bool OnWaitForChannel(TimeSpan timeout)
            => throw new NotSupportedException();

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
            => throw new NotSupportedException();

        protected override bool OnEndWaitForChannel(IAsyncResult result)
            => throw new NotSupportedException();

        #endregion

        class ServerDuplexSessionChannel : DvcDuplexSessionChannel
        {
            public ServerDuplexSessionChannel(MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager, int maxBufferSize,
                IAsyncDvcChannel socket, EndpointAddress localAddress, ChannelManagerBase channelManager)
                : base(messageEncoderFactory, bufferManager, maxBufferSize, AnonymousAddress, localAddress,
                AnonymousAddress.Uri, channelManager)
            {
                base.InitializeSocket(socket);
            }
        }
    }
}
