using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// Windows PowerShell compiles this small Development-only host with the system framework.
// The job handle is private. Losing the host therefore closes every descendant, including
// Next's listener, even if npm/the terminal disappears without executing a JS exit handler.
public static class WindowsDevJob
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits {
        public long ProcessTime, JobTime;
        public uint Flags;
        public UIntPtr MinWorkingSet, MaxWorkingSet;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits {
        public BasicLimits Basic;
        public ulong Io1, Io2, Io3, Io4, Io5, Io6;
        public UIntPtr ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Accounting {
        public long User, Kernel, PeriodUser, PeriodKernel;
        public uint Faults, Total, Active, Terminated;
    }
    private delegate bool ControlHandler(uint kind);
    private static readonly ControlHandler IgnoreBroadcast = delegate { return true; };
    [DllImport("kernel32", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr CreateJobObject(IntPtr attributes, string name);
    [DllImport("kernel32", SetLastError=true)] private static extern bool SetInformationJobObject(IntPtr job, int type, ref ExtendedLimits info, int length);
    [DllImport("kernel32", SetLastError=true)] private static extern bool QueryInformationJobObject(IntPtr job, int type, out Accounting info, int length, IntPtr returned);
    [DllImport("kernel32", SetLastError=true)] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32", SetLastError=true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32", SetLastError=true)] private static extern bool FreeConsole();
    [DllImport("kernel32", SetLastError=true)] private static extern bool AllocConsole();
    [DllImport("kernel32")] private static extern IntPtr GetConsoleWindow();
    [DllImport("user32")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("kernel32", SetLastError=true)] private static extern bool SetConsoleCtrlHandler(ControlHandler handler, bool add);
    [DllImport("kernel32", SetLastError=true)] private static extern bool GenerateConsoleCtrlEvent(uint signal, uint group);
    private static void Check(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
    private static uint Active(IntPtr job) {
        Accounting info;
        Check(QueryInformationJobObject(job, 1, out info, Marshal.SizeOf(typeof(Accounting)), IntPtr.Zero));
        return info.Active;
    }

    public static int Run(string node, string script, string session, string jobName, string eventName)
    {
        // Capture the launcher's pipes before creating a console private to this job.
        Stream output=Console.OpenStandardOutput(), error=Console.OpenStandardError();
        var input = new StreamReader(Console.OpenStandardInput());
        var closed = Task.Run(() => input.ReadLine());
        var job = CreateJobObject(IntPtr.Zero, jobName);
        int creationError = Marshal.GetLastWin32Error();
        Check(job != IntPtr.Zero);
        try {
            if (creationError == 183) throw new InvalidOperationException("[DEV_SESSION_ACTIVE] Another Development session is running or stopping.");
            var limits = new ExtendedLimits(); limits.Basic.Flags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            Check(SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf(typeof(ExtendedLimits))));
            FreeConsole();
            Check(AllocConsole());
            ShowWindow(GetConsoleWindow(), 0);
            Check(SetConsoleCtrlHandler(IgnoreBroadcast,true));
            using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, eventName))
            using (var worker = new Process()) {
                worker.StartInfo = new ProcessStartInfo(node, "\"" + script + "\" " + session) {
                    UseShellExecute=false, RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true
                };
                worker.Start();
                // Until it receives this handshake the worker cannot start any children.
                // If assignment fails, closing stdin releases the waiting worker.
                try { Check(AssignProcessToJobObject(job,worker.Handle)); }
                catch { worker.StandardInput.Close(); throw; }
                var stdout=worker.StandardOutput.BaseStream.CopyToAsync(output);
                var stderr=worker.StandardError.BaseStream.CopyToAsync(error);
                worker.StandardInput.WriteLine("owned"); worker.StandardInput.Flush();
                while (!worker.HasExited && !closed.IsCompleted && !stop.WaitOne(100)) { }
                bool requested = stop.WaitOne(0) || closed.IsCompleted;
                if (Active(job)>0) {
                    // CTRL_C_EVENT group zero is safe ONLY in our freshly allocated private console.
                    GenerateConsoleCtrlEvent(0,0);
                    var timer = Stopwatch.StartNew();
                    while (Active(job)>0 && timer.ElapsedMilliseconds<6500) Thread.Sleep(80);
                    if (Active(job)>0) {
                        var message = System.Text.Encoding.UTF8.GetBytes("[DEV_CLEANUP] Grace period ended; closing the verified session job.\n");
                        error.Write(message,0,message.Length);
                    }
                }
                int code = requested ? 0 : worker.ExitCode;
                CloseHandle(job); job=IntPtr.Zero;
                Task.WaitAll(new Task[]{stdout,stderr},3000);
                return code;
            }
        } finally { if (job!=IntPtr.Zero) CloseHandle(job); }
    }
}
