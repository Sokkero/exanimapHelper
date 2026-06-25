using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ExanimapHelper;

/// <summary>
/// Attaches to the Exanima process and reads float32 values from its memory via the
/// Win32 ReadProcessMemory API. Addresses may be absolute or module-relative
/// (e.g. "Exanima.exe+48DDD0"); the latter is resolved against the module's runtime
/// base captured on attach, which is the only stable form across launches (ASLR).
///
/// Reading never throws: <see cref="TryReadFloat"/> returns false on any failure
/// (see PROJECT.md → Address format &amp; error handling, case 2).
/// </summary>
public sealed class MemoryReader : IDisposable
{
    // Name of the target process, without the ".exe" extension.
    public const string TargetProcessName = "Exanima";

    // File name of the target's main module — the only module a module-relative
    // address may name, validated at parse time.
    public const string MainModuleFileName = TargetProcessName + ".exe";

    // Access rights required to read another process's memory.
    private const int PROCESS_VM_READ = 0x0010;
    private const int PROCESS_QUERY_INFORMATION = 0x0400;

    private IntPtr _handle = IntPtr.Zero;

    // Base address of the target's main module, captured on Attach so module-relative
    // addresses can be resolved against the runtime base, which moves every launch
    // under ASLR. Zero until attached.
    private IntPtr _mainModuleBase = IntPtr.Zero;

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
            CaptureMainModule(matches[0]);
            return AttachStatus.Attached;
        }
        finally
        {
            foreach (Process p in matches)
                p.Dispose();
        }
    }

    // Records the main module's base for later module-relative resolution. Module
    // metadata can be unavailable (access or bitness mismatch); in that case absolute
    // addresses still work but module-relative ones will fail to resolve.
    private void CaptureMainModule(Process process)
    {
        try
        {
            _mainModuleBase = process.MainModule?.BaseAddress ?? IntPtr.Zero;
        }
        catch
        {
            _mainModuleBase = IntPtr.Zero;
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
        _mainModuleBase = IntPtr.Zero;
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

        Span<byte> buffer = stackalloc byte[sizeof(float)];
        bool ok = ReadProcessMemory(
            _handle, (IntPtr)address, ref MemoryMarshal.GetReference(buffer), buffer.Length, out int read);
        if (!ok || read != buffer.Length)
            return false;

        value = BitConverter.ToSingle(buffer);
        return true;
    }

    /// <summary>
    /// Parses an address in either form: a plain absolute hex value (e.g. "1F4A2C80")
    /// or module-relative "Exanima.exe+hexoffset" (e.g. "Exanima.exe+48DDD0"). Both
    /// tolerate an optional "0x"/"0X" prefix and surrounding whitespace. The module name
    /// must be the target's main module (<see cref="MainModuleFileName"/>); any other is
    /// rejected here. Returns false for empty or malformed input (error case 1).
    /// Resolution of the module base happens later via <see cref="TryResolve"/>, since
    /// the base is only known once attached.
    /// </summary>
    public static bool TryParseAddress(string? text, out AddressSpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string s = text.Trim();

        int plus = s.IndexOf('+');
        if (plus >= 0)
        {
            string module = s[..plus].Trim();
            if (!string.Equals(module, MainModuleFileName, StringComparison.OrdinalIgnoreCase)
                || !TryParseHex(s[(plus + 1)..], out long offset))
                return false;
            spec = new AddressSpec(true, offset);
            return true;
        }

        if (!TryParseHex(s, out long absolute))
            return false;
        spec = new AddressSpec(false, absolute);
        return true;
    }

    // Parses a hex literal, tolerating a "0x" prefix and whitespace. Unsigned parse so
    // a full 64-bit value (high bit set) round-trips by bit pattern instead of failing.
    private static bool TryParseHex(string text, out long value)
    {
        value = 0;
        string s = text.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        if (s.Length == 0)
            return false;
        if (!ulong.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out ulong parsed))
            return false;
        value = unchecked((long)parsed);
        return true;
    }

    /// <summary>
    /// Resolves a parsed address to an absolute one. Absolute specs pass through;
    /// module-relative specs add the main module's runtime base captured on attach.
    /// Returns false when not attached or the module metadata was unavailable.
    /// </summary>
    public bool TryResolve(AddressSpec spec, out long address)
    {
        if (!spec.IsModuleRelative)
        {
            address = spec.Value;
            return true;
        }

        address = 0;
        if (_mainModuleBase == IntPtr.Zero)
            return false;

        address = _mainModuleBase.ToInt64() + spec.Value;
        return true;
    }

    public void Dispose() => Detach();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress, ref byte lpBuffer, int dwSize, out int lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);
}

/// <summary>
/// A parsed address: when <see cref="IsModuleRelative"/> is false, <see cref="Value"/>
/// is an absolute address; when true, it is an offset added to the main module's runtime
/// base at resolution time.
/// </summary>
public readonly record struct AddressSpec(bool IsModuleRelative, long Value);

/// <summary>Result of attempting to attach to the target process.</summary>
public enum AttachStatus
{
    Attached,
    ProcessNotFound,
    AccessDenied,
}
