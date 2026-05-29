namespace GitDelta.Core.Git;

public static class GitConstants
{
    /// <summary>
    /// The well-known SHA-1 of the empty tree object, used to diff a root commit
    /// (a commit with no parents) against "nothing".
    /// </summary>
    public const string EmptyTreeSha = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";
}
