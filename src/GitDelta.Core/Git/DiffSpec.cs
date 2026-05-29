namespace GitDelta.Core.Git;

public abstract record DiffSpec
{
    private DiffSpec() { }

    public sealed record WorkingTreeVsHead() : DiffSpec;

    public sealed record CommitVsParent(string Sha) : DiffSpec;

    public sealed record TwoCommits(string BaseSha, string TargetSha) : DiffSpec;
}
