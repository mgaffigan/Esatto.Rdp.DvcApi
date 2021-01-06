using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Contract = Esatto.Win32.Com.Contract;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    public sealed class RawDynamicVirtualClientChannel : IDynamicVirtualClientChannel, IDisposable
    {
        public string ChannelName { get; }
        private readonly DynamicVirtualChannelWriteCallback writeCallback;
        private readonly Action closeCallback;
        private readonly SynchronizationContext SyncCtx;
        private bool isDisposed;

        public event EventHandler<CloseReceivedEventArgs> SenderDisconnected;
        public event EventHandler<UnhandledExceptionEventArgs> Exception;

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        private RawDynamicVirtualClientChannel(string channelName, DynamicVirtualChannelWriteCallback writeCallback, Action closeCallback, SynchronizationContext syncCtx)
        {
            this.ChannelName = channelName;
            this.writeCallback = writeCallback;
            this.closeCallback = closeCallback;
            this.SyncCtx = syncCtx;
        }

        public static IDynamicVirtualClientChannelFactory Create(string channelName, Action<RawDynamicVirtualClientChannel> handleNewChannel)
            => Create(channelName, handleNewChannel, null);
        public static IDynamicVirtualClientChannelFactory Create(string channelName, Action<RawDynamicVirtualClientChannel> handleNewChannel, SynchronizationContext syncCtx)
        {
            TSVirtualChannels.NativeMethods.ValidateChannelName(channelName);
            Contract.Requires(handleNewChannel != null);

            return new RawDynamicVirtualClientChannelFactory(channelName, handleNewChannel, syncCtx);
        }

        private void AssertAlive()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(ChannelName);
            }
        }

        private void RunOnSyncCtx(Action func)
        {
            try
            {
                if (SyncCtx == null)
                {
                    func();
                }
                else
                {
                    SyncCtx.Post(_1 => func(), null);
                }
            }
            catch (Exception ex)
            {
                this.Exception?.Invoke(this, new UnhandledExceptionEventArgs(ex, false));
            }
        }

        public void SendMessage(byte[] data)
        {
            SendMessage(data, 0, data.Length);
        }

        public void SendMessage(byte[] data, int offset, int length)
        {
            AssertAlive();

            this.writeCallback(data, offset, length);
        }

        void IDisposable.Dispose() => Close();

        public void Close()
        {
            AssertAlive();

            isDisposed = true;
            closeCallback();
        }

        void IDynamicVirtualClientChannel.Close()
        {
            RunOnSyncCtx(() =>
            {
                var ea = new CloseReceivedEventArgs();
                SenderDisconnected?.Invoke(this, ea);
                if (!ea.Handled)
                {
                    Close();
                }
            });
        }

        void IDynamicVirtualClientChannel.ReadMessage(byte[] data)
        {
            RunOnSyncCtx(() => MessageReceived?.Invoke(this, new MessageReceivedEventArgs(data)));
        }

        private sealed class RawDynamicVirtualClientChannelFactory : IDynamicVirtualClientChannelFactory
        {
            public string ChannelName { get; }

            private readonly Action<RawDynamicVirtualClientChannel> HandleNewChannel;
            private readonly SynchronizationContext SyncCtx;

            public RawDynamicVirtualClientChannelFactory(string channelName, Action<RawDynamicVirtualClientChannel> handleNewChannel, SynchronizationContext syncCtx)
            {
                this.ChannelName = channelName;
                this.HandleNewChannel = handleNewChannel;
                this.SyncCtx = syncCtx;
            }

            public IDynamicVirtualClientChannel CreateChannel(DynamicVirtualChannelWriteCallback writeCallback, Action closeCallback)
            {
                var newChannel = new RawDynamicVirtualClientChannel(ChannelName, writeCallback, closeCallback, SyncCtx);
                HandleNewChannel(newChannel);
                return newChannel;
            }
        }
    }

    public sealed class CloseReceivedEventArgs : EventArgs
    {
        public bool Handled { get; set; }
    }

    public sealed class MessageReceivedEventArgs : EventArgs
    {
        public byte[] Message { get; }

        public MessageReceivedEventArgs(byte[] message)
        {
            this.Message = message;
        }
    }
}
