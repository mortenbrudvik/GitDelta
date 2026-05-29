namespace GitDelta.Core.Git;

/// <summary>
/// Result of checking whether git is installed and meets the minimum required version.
/// </summary>
public sealed record GitAvailability(bool IsInstalled, string? Version, bool MeetsMinimum);
