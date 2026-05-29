using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace GitDelta.UI.Services;

/// <summary>
/// Launches the external editor configured in settings, falling back to the OS default
/// application. All launch failures are logged and reported via the return value instead of
/// being silently swallowed.
/// </summary>
public sealed partial class ExternalEditorLauncher : IExternalEditorLauncher
{
    private readonly IProcessStarter _processStarter;
    private readonly ILogger<ExternalEditorLauncher> _logger;

    public ExternalEditorLauncher(IProcessStarter processStarter, ILogger<ExternalEditorLauncher> logger)
    {
        _processStarter = processStarter;
        _logger = logger;
    }

    public bool TryOpen(string? editorCommandTemplate, string absolutePath)
    {
        if (!string.IsNullOrWhiteSpace(editorCommandTemplate)
            && TryLaunchCustom(editorCommandTemplate, absolutePath))
        {
            return true;
        }

        return TryLaunchOsDefault(absolutePath);
    }

    private bool TryLaunchCustom(string commandTemplate, string filePath)
    {
        // Parse the TEMPLATE (with {file} left as a literal bare token) so editor paths with
        // spaces survive, then substitute {file} POST-PARSE so a spaced FILE path stays a
        // single argv element.
        var templateArgv = SplitCommandLine(commandTemplate);
        if (!EditorCommandBuilder.TryBuild(templateArgv, filePath, out var fileName, out var arguments))
        {
            return false;
        }

        try
        {
            _processStarter.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
            });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            _logger.LogWarning(ex, "Custom editor command failed for {File}; falling back to OS default", filePath);
            return false;
        }
    }

    private bool TryLaunchOsDefault(string filePath)
    {
        try
        {
            _processStarter.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            _logger.LogWarning(ex, "Could not open {File} in the OS default application", filePath);
            return false;
        }
    }

    /// <summary>
    /// Splits a command line into argv respecting double-quote grouping, using the Win32
    /// <c>CommandLineToArgvW</c> shell parser (same rules as the OS).
    /// </summary>
    private static string[] SplitCommandLine(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        var argvPtr = CommandLineToArgvW(commandLine, out var argc);
        if (argvPtr == nint.Zero)
        {
            return [];
        }

        try
        {
            var args = new string[argc];
            for (var i = 0; i < argc; i++)
            {
                var strPtr = Marshal.ReadIntPtr(argvPtr, i * nint.Size);
                args[i] = Marshal.PtrToStringUni(strPtr) ?? string.Empty;
            }

            return args;
        }
        finally
        {
            LocalFree(argvPtr);
        }
    }

    [LibraryImport("shell32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CommandLineToArgvW(string lpCmdLine, out int pNumArgs);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint LocalFree(nint hMem);
}
