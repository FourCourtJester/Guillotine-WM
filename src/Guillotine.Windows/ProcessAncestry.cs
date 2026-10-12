using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Guillotine.Windows.NativeMethods;

namespace Guillotine.Windows;

[SupportedOSPlatform("windows")]
internal static class ProcessAncestry
{
    /// <summary>
    /// The processes that started this one (shell, terminal, ...), nearest first. Their windows often
    /// show the command line in the title, so they must never be mistaken for the target window.
    /// </summary>
    public static HashSet<int> GetAncestors()
    {
        var parents = new Dictionary<int, int>();
        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
            return [];

        try
        {
            var entry = new PROCESSENTRY32W { dwSize = Marshal.SizeOf<PROCESSENTRY32W>() };
            for (bool ok = Process32First(snapshot, ref entry); ok; ok = Process32Next(snapshot, ref entry))
                parents[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
        }
        finally
        {
            CloseHandle(snapshot);
        }

        var ancestors = new HashSet<int>();
        int child = Environment.ProcessId;
        var childStart = StartTime(child);
        while (ancestors.Count < 16 && parents.TryGetValue(child, out int parent) && parent != 0 && ancestors.Add(parent))
        {
            // A parent that exited may have had its id reused by an unrelated process started later.
            var parentStart = StartTime(parent);
            if (parentStart is not null && childStart is not null && parentStart > childStart)
            {
                ancestors.Remove(parent);
                break;
            }
            child = parent;
            childStart = parentStart;
        }
        return ancestors;
    }

    private static DateTime? StartTime(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.StartTime;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}
