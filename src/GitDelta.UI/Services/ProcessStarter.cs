using System.Diagnostics;

namespace GitDelta.UI.Services;

/// <summary>Production <see cref="IProcessStarter"/> backed by <see cref="Process.Start(ProcessStartInfo)"/>.</summary>
public sealed class ProcessStarter : IProcessStarter
{
    public void Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}
