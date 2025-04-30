using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class ProcessExtensions
{
    public static bool IsDirectlyHostedByExplorer(this Process process) => process.Parent()?.ProcessName == "explorer";

    public static Process Parent(this Process process)
    {
        try
        {
            return Process.GetProcessById(GetParentPid(process.Id));
        }
        catch
        {
            return null;
        }
    }

    public static IntPtr FindVisibleExternalTerminal(this Process process)
    {
        string className = "CASCADIA_HOSTING_WINDOW_CLASS";
        string windowTitle = process.MainModule.FileName;  // or null to match any title

        IntPtr hWnd = FindWindow(className, windowTitle);

        bool isVisible = IsWindowVisible(hWnd);

        return isVisible ? hWnd : IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    // Native API approach: fast and no WMI required
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_BASIC_INFORMATION
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2_0;
        public IntPtr Reserved2_1;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [DllImport("ntdll.dll")]
    static extern int NtQueryInformationProcess(
        IntPtr processHandle,
        int processInformationClass,
        ref PROCESS_BASIC_INFORMATION processInformation,
        int processInformationLength,
        out int returnLength
                                               );

    static int GetParentPid(int pid)
    {
        var process = Process.GetProcessById(pid);
        var pbi = new PROCESS_BASIC_INFORMATION();
        NtQueryInformationProcess(process.Handle, 0, ref pbi, Marshal.SizeOf(pbi), out _);
        return pbi.InheritedFromUniqueProcessId.ToInt32();
    }
}