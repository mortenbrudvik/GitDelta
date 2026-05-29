using System.Diagnostics;

namespace GitDelta.UI.Services;

/// <summary>
/// Thin seam over <see cref="Process.Start(ProcessStartInfo)"/> so process-launching code
/// (e.g. the external editor) can be unit-tested without spawning real processes. Throws on
/// failure, exactly like <see cref="Process.Start(ProcessStartInfo)"/>.
/// </summary>
public interface IProcessStarter
{
    void Start(ProcessStartInfo startInfo);
}
