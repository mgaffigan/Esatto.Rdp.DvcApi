using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Esatto.Rdp.DvcApi.TSVirtualChannels
{
    internal static class NativeMethods
    {
        public const int
            CHANNEL_CHUNK_LENGTH = 1600,
            CHANNEL_BUFFER_SIZE = 65535,
            CHANNEL_PDU_HEADER_SIZE = 8,
            CHANNEL_PDU_LENGTH = CHANNEL_CHUNK_LENGTH + CHANNEL_PDU_HEADER_SIZE,
            WTS_CURRENT_SESSION = -1,
            DUPLICATE_SAME_ACCESS = 2;

        private const string
            Wtsapi32 = "wtsapi32.dll",
            KERNEL32 = "KERNEL32.dll";

        [DllImport(Wtsapi32, ExactSpelling = true, SetLastError = true)]
        public static extern WtsVitrualChannelSafeHandle WTSVirtualChannelOpenEx(
            int sessionId,
            // channel name is always ASCII
            [MarshalAs(UnmanagedType.LPStr)] string channelName,
            WTS_CHANNEL_OPTION options);

        [DllImport(Wtsapi32, ExactSpelling = true, SetLastError = true)]
        public static extern bool WTSVirtualChannelClose(IntPtr hChannel);

        [DllImport(Wtsapi32, ExactSpelling = true, SetLastError = true)]
        public static extern bool WTSVirtualChannelQuery(
            WtsVitrualChannelSafeHandle hChannelHandle,
            WTS_VIRTUAL_CLASS WtsVirtualClass,
            out WtsAllocSafeHandle ppBuffer,
            out int cBytesReturned
        );

        [DllImport(Wtsapi32, ExactSpelling = true)]
        public static extern void WTSFreeMemory(IntPtr pBuf);

        [DllImport(KERNEL32, CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DuplicateHandle(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle, 
            IntPtr hTargetProcessHandle, 
            out SafeFileHandle lpTargetHandle,
            uint dwDesiredAccess, 
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, 
            uint dwOptions
        );

        [DllImport(KERNEL32, CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetCurrentProcess();

        public static void ValidateChannelName(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName)
                            || channelName.Length >= 8
                            || channelName.Any(c => c > 127))
            {
                throw new FormatException($"'{channelName}' is not a valid channel name.  Channel names must be no more than 7 characters and must be ASCII");
            }
        }
    }
}
