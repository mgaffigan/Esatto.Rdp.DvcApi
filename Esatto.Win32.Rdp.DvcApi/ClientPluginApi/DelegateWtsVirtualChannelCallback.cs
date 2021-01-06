#define TRACE

using Esatto.Win32.Rdp.DvcApi.TSVirtualChannels;
using System;
using System.Runtime.InteropServices;

namespace Esatto.Win32.Rdp.DvcApi.ClientPluginApi
{
    sealed class DelegateWtsVirtualChannelCallback : IWTSVirtualChannelCallback
    {
        private IWTSVirtualChannel NativeChannel;
        private readonly IDynamicVirtualClientChannelFactory Factory;

        public IDynamicVirtualClientChannel ReadChannel { get; set; }

        public DelegateWtsVirtualChannelCallback(IWTSVirtualChannel pChannel, IDynamicVirtualClientChannelFactory factory)
        {
            this.NativeChannel = pChannel;
            this.Factory = factory;
        }

        public void OnDataReceived(int cbSize, IntPtr pBuffer)
        {
            var data = new byte[cbSize];
            Marshal.Copy(pBuffer, data, 0, cbSize);

            try
            {
                ReadChannel?.ReadMessage(data);
            }
            catch (Exception ex)
            {
                Log($"Uncaught exception in ReadMessage for '{Factory.ChannelName}': {ex}");
            }
        }

        internal unsafe void WriteMessage(byte[] data, int offset, int count)
        {
            if (offset < 0 || offset >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (count < 0 || count + offset > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            fixed (byte* pData = data)
            {
                IntPtr pStart = (IntPtr)(pData + offset);
                NativeChannel.Write((uint)count, pStart, IntPtr.Zero);
            }
        }

        public void CloseWriteChannel()
        {
            if (NativeChannel == null)
            {
                throw new ObjectDisposedException(nameof(NativeChannel));
            }

            NativeChannel.Close();
            NativeChannel = null;
        }

        public void OnClose()
        {
            var channel = ReadChannel;
            if (channel == null)
            {
                CloseWriteChannel();
            }
            else
            {
                channel.Close();
            }
        }
    }
}
