using System;
using System.Runtime.InteropServices;

namespace hdsdump {
    internal static class NativeMethods {
        public const int  INVALID_HANDLE_VALUE = -1;
        public const uint OPEN_EXISTING        = 3;
        public const uint GENERIC_READ         = 0x80000000;
        public const uint GENERIC_WRITE        = 0x40000000;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(
           String pipeName,
           uint dwDesiredAccess,
           uint dwShareMode,
           IntPtr lpSecurityAttributes,
           uint dwCreationDisposition,
           uint dwFlagsAndAttributes,
           IntPtr hTemplate);
    }
}
