using Esatto.Rdp.DvcApi.ClientPluginApi;
using Esatto.Rdp.DvcApi.TSVirtualChannels;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Esatto.Rdp.DvcApi.TSVirtualChannels.NativeMethods;

namespace Esatto.Rdp.DvcApi
{
    public sealed class DvcServerChannel : IDisposable, IAsyncDvcChannel
    {
        private readonly WtsVitrualChannelSafeHandle Channel;
        private readonly Stream Stream;
        private bool isDisposed;

        public event EventHandler Disconnected;

        private DvcServerChannel(WtsVitrualChannelSafeHandle channel, Stream baseStream)
        {
            this.Channel = channel;
            this.Stream = baseStream;
        }

        public void Dispose()
        {
            AssertAlive();
            isDisposed = true;

            this.Stream.Dispose();
            this.Channel.Dispose();

            Disconnected?.Invoke(this, EventArgs.Empty);
        }

        private void AssertAlive()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(DvcServerChannel));
            }
        }

        public static IAsyncDvcChannel Open(string channelName, WTS_CHANNEL_OPTION option = WTS_CHANNEL_OPTION.DYNAMIC)
        {
            ValidateChannelName(channelName);

            DvcServerChannel result = null;

            // Open sfh in a CER since it can't be in a dispose block since lifetime is transferred to DynamicVirtualServerChannel
            RuntimeHelpers.PrepareConstrainedRegions();
            try { /* CER */ }
            finally
            {
                SafeFileHandle pFile = null;
                var sfh = WTSVirtualChannelOpenEx(WTS_CURRENT_SESSION, channelName, option);
                try
                {
                    WtsAllocSafeHandle pBuffer = null;
                    try
                    {
                        int cbReturned;
                        if (!WTSVirtualChannelQuery(sfh, WTS_VIRTUAL_CLASS.FileHandle, out pBuffer, out cbReturned)
                            || cbReturned < IntPtr.Size)
                        {
                            throw new Win32Exception();
                        }
                        pFile = new SafeFileHandle(Marshal.ReadIntPtr(pBuffer.DangerousGetHandle()), false);
                    }
                    finally
                    {
                        pBuffer?.Dispose();
                    }

                    if (pFile.IsInvalid)
                    {
                        throw new InvalidOperationException("WTSVirtualChannelQuery WTS_VIRTUAL_CLASS.FileHandle returned invalid handle");
                    }

                    // create
                    result = new DvcServerChannel(sfh, new FileStream(pFile, FileAccess.ReadWrite, bufferSize: 32 * 1024 * 1024, isAsync: true));
                }
                finally
                {
                    if (result == null)
                    {
                        sfh.Dispose();
                    }
                }
            }
            return result;
        }

        public async Task<byte[]> ReadMessageAsync(CancellationToken ct)
        {
            AssertAlive();

            try
            {
                var readBuffer = new byte[CHANNEL_PDU_LENGTH];
                var readBytes = await Stream.ReadAsync(readBuffer, 0, readBuffer.Length, ct).ConfigureAwait(false);
                if (readBytes < 1)
                {
                    throw new DvcChannelDisconnectedException();
                }
                if (readBytes < CHANNEL_PDU_HEADER_SIZE)
                {
                    throw new ProtocolViolationException($"Read returned buffer that was too short ({readBytes} bytes)");
                }
                var pdu = CHANNEL_PDU_HEADER.FromBuffer(readBuffer, 0);
                if (!pdu.flags.HasFlag(CHANNEL_FLAG.First))
                {
                    throw new ProtocolViolationException($"PDU received with flags {pdu.flags} when FIRST was expected");
                }

                var totalLength = pdu.length;
                var msResult = new MemoryStream(pdu.length);
                msResult.Write(readBuffer, CHANNEL_PDU_HEADER_SIZE, readBytes - CHANNEL_PDU_HEADER_SIZE);

                // 2..n
                while (msResult.Position < totalLength)
                {
                    // Allowing cancellation here would result in a protocol violation
                    readBytes = await Stream.ReadAsync(readBuffer, 0, readBuffer.Length).ConfigureAwait(false);
                    if (readBytes < CHANNEL_PDU_HEADER_SIZE)
                    {
                        throw new ProtocolViolationException($"Read returned buffer that was too short ({readBytes} bytes)");
                    }
                    pdu = CHANNEL_PDU_HEADER.FromBuffer(readBuffer, 0);
                    if (pdu.flags.HasFlag(CHANNEL_FLAG.First))
                    {
                        throw new ProtocolViolationException($"PDU received with flags {pdu.flags} when MIDDLE/LAST was expected");
                    }

                    msResult.Write(readBuffer, CHANNEL_PDU_HEADER_SIZE, readBytes - CHANNEL_PDU_HEADER_SIZE);

                    if (pdu.flags.HasFlag(CHANNEL_FLAG.Last))
                    {
                        break;
                    }
                }
                if (msResult.Position != totalLength)
                {
                    throw new ProtocolViolationException($"PDU declared length {totalLength} but {msResult.Position} bytes were received");
                }

                // ret
                return msResult.ToArray();
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x800700e9) /* HR ERROR_PIPE_NOT_CONNECTED */)
            {
                throw new DvcChannelDisconnectedException();
            }
        }

        public byte[] ReadMessage(CancellationToken ct) => ReadMessageAsync(ct).GetResultOrException();

        public async Task SendMessageAsync(byte[] data, int offset, int count)
        {
            AssertAlive();

            try
            {
                await Stream.WriteAsync(data, offset, count);
                await Stream.FlushAsync();
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x800700e9) /* HR ERROR_PIPE_NOT_CONNECTED */)
            {
                throw new DvcChannelDisconnectedException();
            }
        }

        public void SendMessage(byte[] data, int offset, int count)
        {
            AssertAlive();

            try
            {
                Stream.Write(data, offset, count);
                Stream.Flush();
            }
            catch (IOException ex) when (ex.HResult == unchecked((int)0x800700e9) /* HR ERROR_PIPE_NOT_CONNECTED */)
            {
                throw new DvcChannelDisconnectedException();
            }
        }
    }
}
