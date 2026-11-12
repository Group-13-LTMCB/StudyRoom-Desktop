using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StudyRoom.Core.Interfaces;

namespace StudyRoom.Infrastructure.OS;

[SupportedOSPlatform("windows")]
public sealed class MemoryOptimizer : IMemoryOptimizer
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    public void OptimizeMemory()
    {
        PerformOptimization();
    }

    public static void PerformOptimization()
    {
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                EmptyWorkingSet(currentProcess.Handle);
            }
            catch
            {
                // Fallback gracefully if permissions or OS API fails
            }
        }
    }
}
