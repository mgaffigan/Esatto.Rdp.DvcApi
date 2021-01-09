using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcApi.Broker
{
    // Runs on Session Host
    public static class BrokeredServiceClient
    {
        public static async Task<IAsyncDvcChannel> ConnectAsync(string serviceName, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(serviceName))
            {
                throw new ArgumentNullException(nameof(serviceName));
            }

            var channel = await Task.Run(() => DvcServerChannel.Open(BrokerConstants.BrokerChannelName)).ConfigureAwait(false);
            try
            {
                var utf8ServiceName = Encoding.UTF8.GetBytes(serviceName);
                await channel.SendMessageAsync(utf8ServiceName, 0, utf8ServiceName.Length).ConfigureAwait(false);
                
                var brokerResponseUtf8 = await channel.ReadMessageAsync(ct).ConfigureAwait(false);
                var brokerResponse = Encoding.UTF8.GetString(brokerResponseUtf8);
                if (brokerResponse != BrokerConstants.AcceptedMessage)
                {
                    throw new ChannelNotAvailableException($"Broker refused connection: {brokerResponse}");
                }

                return channel;
            }
            catch
            {
                channel.Dispose();
                throw;
            }
        }
    }
}
