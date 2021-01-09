using Esatto.Win32.Rdp.DvcApi.ClientPluginApi;
using System;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.WcfDvc
{
    abstract class DvcDuplexSessionChannel : ChannelBase, IDuplexSessionChannel
    {
        int maxBufferSize;
        BufferManager bufferManager;
        IAsyncDvcChannel channel;
        object readLock = new object();
        object writeLock = new object();

        public EndpointAddress RemoteAddress { get; }

        public Uri Via { get; }

        protected MessageEncoder MessageEncoder { get; }

        internal static readonly EndpointAddress AnonymousAddress =
            new EndpointAddress("http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous");

        protected DvcDuplexSessionChannel(
            MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager, int maxBufferSize,
            EndpointAddress remoteAddress, EndpointAddress localAddress, Uri via, ChannelManagerBase channelManager)
            : base(channelManager)
        {

            this.RemoteAddress = remoteAddress;
            this.LocalAddress = localAddress;
            this.Via = via;
            this.Session = new TcpDuplexSession(this);
            this.MessageEncoder = messageEncoderFactory.CreateSessionEncoder();
            this.bufferManager = bufferManager;
            this.maxBufferSize = maxBufferSize;
        }

        protected void InitializeSocket(IAsyncDvcChannel socket)
        {
            if (this.channel != null)
            {
                throw new InvalidOperationException("Socket is already set");
            }

            this.channel = socket;
        }

        protected static Exception ConvertSocketException(SocketException socketException, string operation)
        {
            if (socketException.ErrorCode == 10049 // WSAEADDRNOTAVAIL 
                || socketException.ErrorCode == 10061 // WSAECONNREFUSED 
                || socketException.ErrorCode == 10050 // WSAENETDOWN 
                || socketException.ErrorCode == 10051 // WSAENETUNREACH 
                || socketException.ErrorCode == 10064 // WSAEHOSTDOWN 
                || socketException.ErrorCode == 10065) // WSAEHOSTUNREACH
            {
                return new EndpointNotFoundException(string.Format(operation + " error: {0} ({1})", socketException.Message, socketException.ErrorCode), socketException);
            }
            if (socketException.ErrorCode == 10060) // WSAETIMEDOUT
            {
                return new TimeoutException(operation + " timed out.", socketException);
            }
            else
            {
                return new CommunicationException(string.Format(operation + " error: {0} ({1})", socketException.Message, socketException.ErrorCode), socketException);
            }
        }

        #region Send

        public void Send(Message message) => this.Send(message, DefaultSendTimeout);

        public void Send(Message message, TimeSpan timeout)
        {
            base.ThrowIfDisposedOrNotOpen();
            lock (writeLock)
            {
                try
                {
                    var encodedBytes = EncodeMessage(message);
                    channel.SendMessage(encodedBytes.Array, encodedBytes.Offset, encodedBytes.Count);
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Receive");
                }
            }
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
            => BeginSend(message, DefaultSendTimeout, callback, state);

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            base.ThrowIfDisposedOrNotOpen();
            var encodedBytes = this.EncodeMessage(message);

            return Tap.Run(callback, state, () => Task.Run(() =>
            {
                try
                {
                    channel.SendMessage(encodedBytes.Array, encodedBytes.Offset, encodedBytes.Count);
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Receive");
                }
            }));
        }

        public void EndSend(IAsyncResult result) => Tap.Complete(result);

        #endregion

        #region Receive

        public Message Receive()
        {
            return this.Receive(DefaultReceiveTimeout);
        }

        public Message Receive(TimeSpan timeout)
        {
            base.ThrowIfDisposedOrNotOpen();
            lock (readLock)
            {
                try
                {
                    var ct = GetCancellationTokenForTimeout(timeout);
                    var encodedBytes = TakeOwnership(channel.ReadMessage(ct));
                    return DecodeMessage(encodedBytes);
                }
                catch (ObjectDisposedException) { /* no-op */ }
                catch (OperationCanceledException) { /* no-op */ }
                catch (DvcChannelDisconnectedException) { /* no-op */ }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Receive");
                }
            }

            // ODE, Cancellation, Disconnection -> end of stream
            return null;
        }

        private async Task<Message> ReceiveAsync(TimeSpan timeout)
        {
            try
            {
                var ct = GetCancellationTokenForTimeout(timeout);
                var encodedBytes = TakeOwnership(await channel.ReadMessageAsync(ct));
                return DecodeMessage(encodedBytes);
            }
            catch (ObjectDisposedException) { /* no-op */ }
            catch (OperationCanceledException) { /* no-op */ }
            catch (DvcChannelDisconnectedException) { /* no-op */ }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Receive");
            }

            // ODE, Cancellation, Disconnection -> end of stream
            return null;
        }

        protected static CancellationToken GetCancellationTokenForTimeout(TimeSpan timeout)
        {
            if (timeout > TimeSpan.FromDays(2))
            {
                return default(CancellationToken);
            }

            return new CancellationTokenSource(timeout).Token;
        }

        private ArraySegment<byte> TakeOwnership(byte[] data)
        {
            var buffer = bufferManager.TakeBuffer(data.Length);
            Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
            return new ArraySegment<byte>(buffer, 0, data.Length);
        }

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            return BeginReceive(DefaultReceiveTimeout, callback, state);
        }

        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            base.ThrowIfDisposedOrNotOpen();
            return Tap.Run(callback, state, () => ReceiveAsync(timeout));
        }

        public Message EndReceive(IAsyncResult result)
            => Tap.Complete<Message>(result);

        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            try
            {
                message = Receive(timeout);
                return true;
            }
            catch (TimeoutException)
            {
                message = null;
                return false;
            }
        }

        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            base.ThrowIfDisposedOrNotOpen();

            return Tap.Run(callback, state, () => ReceiveAsync(timeout));
        }

        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            try
            {
                message = Tap.Complete<Message>(result);
                return true;
            }
            catch (TimeoutException)
            {
                message = null;
                return false;
            }
        }

        #endregion

        // Address the Message and serialize it into a byte array.
        ArraySegment<byte> EncodeMessage(Message message)
        {
            try
            {
                this.RemoteAddress.ApplyTo(message);
                return MessageEncoder.WriteMessage(message, maxBufferSize, bufferManager);
            }
            finally
            {
                // we've consumed the message by serializing it, so clean up
                message.Close();
            }
        }

        Message DecodeMessage(ArraySegment<byte> data)
        {
            if (data.Array == null)
                return null;
            else
                return MessageEncoder.ReadMessage(data, bufferManager);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            CompletedAsyncResult.End(result);
        }

        protected override void OnOpen(TimeSpan timeout)
        {
        }

        protected override void OnAbort()
        {
            try
            {
                channel?.Dispose();
            }
            catch (ObjectDisposedException) { /* no-op */ }
            catch (OperationCanceledException) { /* no-op */ }
            catch (DvcChannelDisconnectedException) { /* no-op */ }
            channel = null;
        }

        protected override void OnClose(TimeSpan timeout) => OnAbort();

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            OnAbort();
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            CompletedAsyncResult.End(result);
        }

        public EndpointAddress LocalAddress { get; }

        public IDuplexSession Session { get; }

        class TcpDuplexSession : IDuplexSession
        {
            DvcDuplexSessionChannel channel;
            string id;

            public TcpDuplexSession(DvcDuplexSessionChannel channel)
            {
                this.channel = channel;
                this.id = Guid.NewGuid().ToString();
            }

            public void CloseOutputSession(TimeSpan timeout)
            {
                if (channel.State != CommunicationState.Closing)
                {
                    channel.ThrowIfDisposedOrNotOpen();
                }
            }

            public IAsyncResult BeginCloseOutputSession(TimeSpan timeout, AsyncCallback callback, object state)
            {
                CloseOutputSession(timeout);
                return new CompletedAsyncResult(callback, state);
            }

            public IAsyncResult BeginCloseOutputSession(AsyncCallback callback, object state)
            {
                return BeginCloseOutputSession(channel.DefaultCloseTimeout, callback, state);
            }

            public void EndCloseOutputSession(IAsyncResult result)
            {
                CompletedAsyncResult.End(result);
            }

            public void CloseOutputSession()
            {
                CloseOutputSession(channel.DefaultCloseTimeout);
            }

            public string Id => this.id;
        }

        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
            => throw new NotSupportedException("The method or operation is not implemented.");

        public bool EndWaitForMessage(IAsyncResult result)
            => throw new NotSupportedException("The method or operation is not implemented.");

        public bool WaitForMessage(TimeSpan timeout)
            => throw new NotSupportedException("The method or operation is not implemented.");
    }
}
