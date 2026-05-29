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

    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;

    private const uint FILE_TYPE_UNKNOWN = 0x0000;
    private const uint FILE_TYPE_CHAR = 0x0002;

    private const int STD_OUTPUT_HANDLE = -11;

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
        // If stdout is already redirected (file/pipe), the existing stream is valid —
        // we must NOT replace it, or redirection breaks. Detect that first.
        if (IsStdOutRedirected())
        {
            // Still attach so Console.Out flushes to the right place when not redirected,
            // but the redirected handle already routes correctly; nothing more to do.
            AttachConsole(ATTACH_PARENT_PROCESS);
            return true;
        }

        if (!AttachConsole(ATTACH_PARENT_PROCESS))
        {
            // No parent console (e.g. launched from Explorer): nothing to write to.
            return false;
        }

        return ReopenStdOutToConsole();
    }

    public static void Detach() => FreeConsole();

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
        var handle = CreateFile(
            "CONOUT$",
            GENERIC_WRITE,
            FILE_SHARE_WRITE,
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
