using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GitDelta.UI.Cli;

/// <summary>
/// Win32 console plumbing for a WinExe that must emit text to the launching terminal
/// for --help/--version while preserving redirection and pipes.
/// </summary>
internal static partial class NativeConsole
{
    private const int ATTACH_PARENT_PROCESS = -1;

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    private const uint FILE_TYPE_UNKNOWN = 0x0000;
    private const uint FILE_TYPE_CHAR = 0x0002;

    private const int STD_OUTPUT_HANDLE = -11;

    /// <summary>
    /// True only when we called <c>AttachConsole</c> against a real console and
    /// must balance it with <c>FreeConsole</c> on <see cref="Detach"/>. Stays
    /// false in the redirected/pipe case (we never attach there).
    /// </summary>
    private static bool _attachedToRealConsole;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FreeConsole();

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint GetFileType(nint hFile);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        nint lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        nint hTemplateFile);

    /// <summary>
    /// Attaches to the parent console. Returns true if a console is now available
    /// for writing (whether via an attached terminal or via redirection/pipe).
    /// </summary>
    public static bool TryAttachToParentConsole()
    {
        // If stdout is already redirected (file/pipe), the existing stream is valid
        // and the OS routes it correctly with no attach. We must NOT call
        // AttachConsole here — doing so would later require a FreeConsole that can
        // dislocate the parent shell's prompt — nor reopen CONOUT$, which would
        // break redirection. The redirected handle already routes correctly.
        if (IsStdOutRedirected())
        {
            return true;
        }

        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            // No parent console (e.g. launched from Explorer): nothing to write to.
            return false;
        }

        if (!ReopenStdOutToConsole())
        {
            // Attached but could not reopen CONOUT$: balance the attach so the
            // process does not exit still-attached to the parent console.
            FreeConsole();
            return false;
        }

        _attachedToRealConsole = true;
        return true;
    }

    /// <summary>
    /// Detaches from the parent console, but only if we actually attached to a
    /// real console (never in the redirected/pipe case, where we never attached).
    /// </summary>
    public static void Detach()
    {
        if (_attachedToRealConsole)
        {
            FreeConsole();
            _attachedToRealConsole = false;
        }
    }

    private static bool IsStdOutRedirected()
    {
        var handle = GetStdHandle(STD_OUTPUT_HANDLE);
        if (handle == nint.Zero || handle == new nint(-1))
        {
            return false;
        }

        var type = GetFileType(handle);
        // A char device is a console; disk/pipe means redirection is in effect.
        return type is not FILE_TYPE_CHAR and not FILE_TYPE_UNKNOWN;
    }

    private static bool ReopenStdOutToConsole()
    {
        // Match the .NET runtime's ConsolePal pattern: GENERIC_READ | GENERIC_WRITE
        // with FILE_SHARE_READ | FILE_SHARE_WRITE. conhost holds CONOUT$ open with
        // read access, so without FILE_SHARE_READ this can fail with
        // ERROR_SHARING_VIOLATION → invalid handle → no terminal output.
        var handle = CreateFile(
            "CONOUT$",
            GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            nint.Zero,
            OPEN_EXISTING,
            0,
            nint.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        var stream = new FileStream(handle, FileAccess.Write);
        var writer = new StreamWriter(stream, Console.OutputEncoding) { AutoFlush = true };
        Console.SetOut(writer);
        return true;
    }
}
