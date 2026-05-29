using System.ComponentModel;
using System.Diagnostics;
using GitDelta.UI.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace GitDelta.UI.UnitTests.Services;

public class ExternalEditorLauncherTests
{
    private readonly IProcessStarter _starter = Substitute.For<IProcessStarter>();
    private ExternalEditorLauncher Create() =>
        new(_starter, NullLogger<ExternalEditorLauncher>.Instance);

    [Fact]
    public void TryOpen_WithCustomCommand_LaunchesItWithoutShellExecute_ReturnsTrue()
    {
        var sut = Create();

        var result = sut.TryOpen(@"code -g {file}", @"C:\repo\src\a.cs");

        result.ShouldBeTrue();
        _starter.Received(1).Start(Arg.Is<ProcessStartInfo>(p =>
            p.UseShellExecute == false && p.FileName == "code"));
    }

    [Fact]
    public void TryOpen_NoCustomCommand_UsesOsDefaultShellExecute_ReturnsTrue()
    {
        var sut = Create();

        var result = sut.TryOpen(null, @"C:\repo\src\a.cs");

        result.ShouldBeTrue();
        _starter.Received(1).Start(Arg.Is<ProcessStartInfo>(p => p.UseShellExecute));
    }

    [Fact]
    public void TryOpen_CustomCommandFails_FallsBackToOsDefault_ReturnsTrue()
    {
        // The custom (UseShellExecute=false) launch throws; the OS-default fallback succeeds.
        _starter.When(s => s.Start(Arg.Is<ProcessStartInfo>(p => !p.UseShellExecute)))
                .Throw(new Win32Exception("not found"));
        var sut = Create();

        var result = sut.TryOpen(@"missing-editor {file}", @"C:\repo\a.cs");

        result.ShouldBeTrue();
        _starter.Received(1).Start(Arg.Is<ProcessStartInfo>(p => p.UseShellExecute));
    }

    [Fact]
    public void TryOpen_AllAttemptsFail_ReturnsFalse()
    {
        _starter.When(s => s.Start(Arg.Any<ProcessStartInfo>()))
                .Throw(new Win32Exception("nope"));
        var sut = Create();

        var result = sut.TryOpen(@"missing-editor {file}", @"C:\repo\a.cs");

        result.ShouldBeFalse();
    }

    [Fact]
    public void TryOpen_EmptyTemplate_SkipsCustom_UsesOsDefault()
    {
        var sut = Create();

        var result = sut.TryOpen("   ", @"C:\repo\a.cs");

        result.ShouldBeTrue();
        _starter.Received(1).Start(Arg.Is<ProcessStartInfo>(p => p.UseShellExecute));
        _starter.DidNotReceive().Start(Arg.Is<ProcessStartInfo>(p => !p.UseShellExecute));
    }
}
