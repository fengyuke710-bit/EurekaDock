using System;
using System.Runtime.InteropServices;

namespace EurekaDock;

internal static class BackgroundMemory {
 [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
 [DllImport("kernel32.dll",SetLastError=true)]
 [return:MarshalAs(UnmanagedType.Bool)] static extern bool K32EmptyWorkingSet(IntPtr process);
 // Called once after caches and render workers are released during sustained
 // background sleep. Windows reloads resident pages on demand upon wake.
 internal static bool ReleaseIdlePages()=>K32EmptyWorkingSet(GetCurrentProcess());
}
