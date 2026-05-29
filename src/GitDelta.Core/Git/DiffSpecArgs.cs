namespace GitDelta.Core.Git;

public static class DiffSpecArgs
{
    /// <summary>
    /// Maps a <see cref="DiffSpec"/> to the ref arguments used in 'git diff'.
    /// For a merge commit's "vs parent", the first parent is referenced via "sha^"
    /// (equivalent to "sha^1"). For a root commit, the empty tree is the base.
    /// </summary>
    public static IReadOnlyList<string> ToDiffArgs(DiffSpec spec, bool isRootCommit) => spec switch
    {
        DiffSpec.WorkingTreeVsHead => ["HEAD"],
        DiffSpec.CommitVsParent c when isRootCommit => [GitConstants.EmptyTreeSha, c.Sha],
        DiffSpec.CommitVsParent c => [c.Sha + "^", c.Sha],
        DiffSpec.TwoCommits t => [t.BaseSha, t.TargetSha],
        _ => throw new ArgumentOutOfRangeException(nameof(spec), spec, "Unknown DiffSpec variant.")
    };
}
