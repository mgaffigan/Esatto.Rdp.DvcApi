using Esatto.Rdp.DvcApi.Broker;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcApi.WcfDvc
{
    class DvcClientDuplexSessionChannel : DvcDuplexSessionChannel
    {
        public DvcClientDuplexSessionChannel(
            MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager, int maxBufferSize,
            EndpointAddress remoteAddress, Uri via, ChannelManagerBase channelManager)
            : base(messageEncoderFactory, bufferManager, maxBufferSize, remoteAddress, AnonymousAddress, via, channelManager)
        {
        }

        protected override void OnOpen(TimeSpan timeout)
        {
            try
            {
                var ct = GetCancellationTokenForTimeout(timeout);
                base.InitializeSocket(BrokeredServiceClient.ConnectAsync(Via.PathAndQuery, ct).GetResultOrException());
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Connect");
            }

            base.OnOpen(timeout);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Tap.Run(callback, state, async () =>
            {
                try
                {
                    var ct = GetCancellationTokenForTimeout(timeout);
                    base.InitializeSocket(await BrokeredServiceClient.ConnectAsync(Via.PathAndQuery, ct));
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Connect");
                }
            });
        }

        protected override void OnEndOpen(IAsyncResult result) => Tap.Complete(result);
    }
}
