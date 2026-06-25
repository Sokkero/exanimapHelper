using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ExanimapHelper;

/// <summary>
/// Attaches to the Exanima process and reads float32 values from absolute
/// memory addresses via the Win32 ReadProcessMemory API.
///
/// Reading never throws: <see cref="TryReadFloat"/> returns false on any failure
/// (see PROJECT.md → Address format &amp; error handling, case 2).
/// </summary>
public sealed class MemoryReader : IDisposable
{
    // Name of the target process, without the ".exe" extension.
    public const string TargetProcessName = "Exanima";

    // Access rights required to read another process's memory.
    private const int PROCESS_VM_READ = 0x0010;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;

    private IntPtr _handle = IntPtr.Zero;

    /// <summary>True when a process handle is currently open.</summary>
    public bool IsAttached => _handle != IntPtr.Zero;

    /// <summary>PID of the attached process, or null when not attached.</summary>
    public int? ProcessId { get; private set; }

    /// <summary>
    /// Finds <c>Exanima.exe</c> by name and opens a read handle to it.
    /// Returns the resulting status; does not throw.
    /// </summary>
    public AttachStatus Attach()
    {
        Detach();

        Process[] matches = Process.GetProcessesByName(TargetProcessName);
        try
        {
            if (matches.Length == 0)
                return AttachStatus.ProcessNotFound;

            int pid = matches[0].Id;
            IntPtr handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, pid);
            if (handle == IntPtr.Zero)
                return AttachStatus.AccessDenied;

            _handle = handle;
            ProcessId = pid;
            return AttachStatus.Attached;
        }
        finally
        {
            foreach (Process p in matches)
                p.Dispose();
        }
    }

    /// <summary>Closes the open handle, if any.</summary>
    public void Detach()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
        ProcessId = null;
    }

    /// <summary>
    /// Reads a 32-bit float from <paramref name="address"/> in the attached
    /// process. Returns false (without throwing) if not attached or the read
    /// fails or is incomplete.
    /// </summary>
    public bool TryReadFloat(long address, out float value)
    {
        value = 0f;
        if (_handle == IntPtr.Zero)
            return false;

        byte[] buffer = new byte[sizeof(float)];
        bool ok = ReadProcessMemory(_handle, (IntPtr)address, buffer, buffer.Length, out int read);
        if (!ok || read != buffer.Length)
            return false;

        value = BitConverter.ToSingle(buffer, 0);
        return true;
    }

    /// <summary>
    /// Parses a plain absolute hexadecimal address (e.g. "1F4A2C80"), tolerating
    /// an optional "0x"/"0X" prefix and surrounding whitespace. Returns false for
    /// empty or non-hex input (error case 1). Does not accept module+offset form.
    /// </summary>
    public static bool TryParseAddress(string? text, out long address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string s = text.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];

        if (s.Length == 0)
            return false;

        return long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address);
    }

    public void Dispose() => Detach();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}

/// <summary>Result of attempting to attach to the target process.</summary>
public enum AttachStatus
{
    Attached,
    ProcessNotFound,
    AccessDenied,
}
