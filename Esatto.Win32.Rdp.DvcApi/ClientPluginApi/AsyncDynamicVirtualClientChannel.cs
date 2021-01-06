using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    public sealed class AsyncDynamicVirtualClientChannel
    {
        private RawDynamicVirtualClientChannel RawChannel;

        private readonly object syncRoot;
        private Queue<byte[]> PendingMessages;
        private TaskCompletionSource<byte[]> ReadTask;

        public event EventHandler<CloseReceivedEventArgs> SenderDisconnected;
        public event EventHandler<UnhandledExceptionEventArgs> Exception;

        private AsyncDynamicVirtualClientChannel(RawDynamicVirtualClientChannel rawChannel)
        {
            this.PendingMessages = new Queue<byte[]>();
            this.syncRoot = new object();
            this.RawChannel = rawChannel;

            this.RawChannel.MessageReceived += RawChannel_MessageReceived;
            this.RawChannel.Exception += RawChannel_Exception;
            this.RawChannel.SenderDisconnected += RawChannel_SenderDisconnected;
        }

        private void RawChannel_SenderDisconnected(object sender, CloseReceivedEventArgs e)
        {
            TaskCompletionSource<byte[]> handler;
            lock (syncRoot)
            {
                handler = ReadTask;
                ReadTask = null;
            }

            handler?.SetCanceled();
            SenderDisconnected?.Invoke(this, e);
        }

        private void RawChannel_Exception(object sender, UnhandledExceptionEventArgs e)
        {
            TaskCompletionSource<byte[]> handler;
            lock (syncRoot)
            {
                handler = ReadTask;
                ReadTask = null;
            }

            handler?.SetException((e.ExceptionObject as Exception) ?? new Exception("Unknown exception"));
            Exception?.Invoke(this, e);
        }

        private void RawChannel_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            TaskCompletionSource<byte[]> handler;
            lock (syncRoot)
            {
                handler = ReadTask;
                ReadTask = null;

                if (handler == null)
                {
                    PendingMessages.Enqueue(e.Message);
                }
            }

            handler.SetResult(e.Message);
        }

        public Task SendMessageAsync(byte[] message) => SendMessageAsync(message, 0, message.Length);
        public Task SendMessageAsync(byte[] message, int offset, int length)
        {
            return Task.Run(() =>
            {
                RawChannel.SendMessage(message, offset, length);
            });
        }

        public Task<byte[]> ReadMessageAsync() => ReadMessageAsync(default(CancellationToken));
        public Task<byte[]> ReadMessageAsync(CancellationToken ct)
        {
            lock (syncRoot)
            {
                if (ReadTask != null)
                {
                    throw new InvalidOperationException("An uncompleted read is already in process");
                }

                if (PendingMessages.Any())
                {
                    return Task.FromResult(PendingMessages.Dequeue());
                }

                var newTcs = new TaskCompletionSource<byte[]>();
                ReadTask = newTcs;
                ct.Register(() =>
                {
                    lock (syncRoot)
                    {
                        if (ReadTask == newTcs)
                        {
                            ReadTask.SetCanceled();
                            ReadTask = null;
                        }
                    }
                });
                return newTcs.Task;
            }
        }

        public static IDynamicVirtualClientChannelFactory Create(string channelName, Action<AsyncDynamicVirtualClientChannel> handleChannel)
        {
            return RawDynamicVirtualClientChannel.Create(channelName, rawChannel =>
            {
                handleChannel(new AsyncDynamicVirtualClientChannel(rawChannel));
            });
        }
    }
}
