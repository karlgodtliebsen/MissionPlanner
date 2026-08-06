using FluentAssertions;
using MissionPlanner.Core.FlightData.Scripting;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies constrained script parsing and whole-document validation.</summary>
public sealed class VehicleScriptTests
{
    private readonly IVehicleScriptParser parser = new VehicleScriptParser();
    private readonly IVehicleScriptValidator validator = new VehicleScriptValidator(new VehicleScriptActionRegistry());

    /// <summary>A version-one allow-listed script is accepted.</summary>
    [Fact]
    public void AcceptsVersionOneAllowListedScript()
    {
        var document = parser.Parse("""{"version":1,"name":"test","steps":[{"action":"delay","arguments":{"milliseconds":"10"},"timeoutSeconds":2}]}""");
        validator.Validate(document).IsValid.Should().BeTrue();
    }

    /// <summary>Arbitrary execution actions are rejected before any step runs.</summary>
    [Theory]
    [InlineData("process")]
    [InlineData("file")]
    [InlineData("mavlinkCommand")]
    [InlineData("csharp")]
    public void RejectsForbiddenActions(string action)
    {
        var document = parser.Parse($$"""{"version":1,"name":"unsafe","steps":[{"action":"{{action}}","arguments":{},"timeoutSeconds":2}]}""");
        validator.Validate(document).IsValid.Should().BeFalse();
    }

    /// <summary>Unbounded waits are rejected.</summary>
    [Fact]
    public void RejectsUnboundedTimeoutAndDelay()
    {
        var document = parser.Parse("""{"version":1,"name":"long","steps":[{"action":"delay","arguments":{"milliseconds":"999999"},"timeoutSeconds":999}]}""");
        var result = validator.Validate(document);
        result.Errors.Should().HaveCount(2);
    }
}
