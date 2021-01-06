using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static Esatto.Win32.Rdp.DvcApi.TSVirtualChannels.NativeMethods;

namespace Esatto.Win32.Rdp.DvcApi.SessionHostApi
{
    public sealed class DynamicVirtualServerChannel : IDisposable
    {
        private readonly WtsVitrualChannelSafeHandle Channel;
        private readonly Stream Stream;
        private bool isDisposed;

        private DynamicVirtualServerChannel(WtsVitrualChannelSafeHandle channel, Stream baseStream)
        {
            this.Channel = channel;
            this.Stream = baseStream;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(DynamicVirtualServerChannel));
            }
            isDisposed = true;

            this.Stream.Dispose();
            this.Channel.Dispose();
        }

        public static DynamicVirtualServerChannel Open(string channelName, WTS_CHANNEL_OPTION option = WTS_CHANNEL_OPTION.DYNAMIC)
        {
            ValidateChannelName(channelName);

            DynamicVirtualServerChannel result = null;

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
                    result = new DynamicVirtualServerChannel(sfh, new FileStream(pFile, FileAccess.ReadWrite, bufferSize: 32 * 1024 * 1024, isAsync: true));
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

        public async Task<byte[]> ReadPacketAsync()
        {
            var readBuffer = new byte[CHANNEL_PDU_LENGTH];
            var readBytes = await Stream.ReadAsync(readBuffer, 0, readBuffer.Length);
            if (readBytes < 1 && isDisposed)
            {
                throw new ObjectDisposedException(nameof(DynamicVirtualServerChannel));
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
                readBytes = await Stream.ReadAsync(readBuffer, 0, readBuffer.Length);
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

        public async Task WritePacketAsync(byte[] data, int offset, int count)
        {
            await Stream.WriteAsync(data, offset, count);
            await Stream.FlushAsync();
        }
    }
}
