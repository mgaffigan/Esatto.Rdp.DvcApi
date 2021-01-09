﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace Esatto.Win32.Rdp.DvcApi
{
    public interface IAsyncDvcChannel : IDisposable
    {
        byte[] ReadMessage(CancellationToken ct = default(CancellationToken));
        Task<byte[]> ReadMessageAsync(CancellationToken ct = default(CancellationToken));

        void SendMessage(byte[] data, int offset, int length);
        Task SendMessageAsync(byte[] data, int offset, int length);

        event EventHandler Disconnected;
    }

    public static class AsyncDvcChannelExtensions
    {
        public static void SendMessage(this IAsyncDvcChannel @this, byte[] data)
            => @this.SendMessage(data, 0, data.Length);
        public static Task SendMessageAsync(this IAsyncDvcChannel @this, byte[] data)
            => @this.SendMessageAsync(data, 0, data.Length);

    }
}
