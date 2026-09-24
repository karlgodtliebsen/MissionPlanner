using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MissionPlanner.App.Presentation.Documents;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class MotorOutputDocumentTests
{
    [Fact]
    public void PreservesEveryDiagnosticAndGuidanceLineWithCopyableAssignments()
    {
        var configuration = new[]
        {
            "FRAME_CLASS = 1", "MOT_SPIN_ARM = 0.1", "BRD_SAFETY_DEFLT = 0",
            "SERVO1_FUNCTION = 33", "Q_FRAME_TYPE = unavailable",
            "Motor interlock: RC7_OPTION = 32; switch state unavailable.",
            "Selected protocol: Normal PWM; effective hardware support/reboot state not verified.",
            "Test A — Motor 1 — CCW | front/right | output 1 (Resolved) | Normal PWM",
            "Timer/output groups: unavailable; consult the board output-group documentation.",
        };
        var guidance = new[]
        {
            "Physical rotation has not been confirmed. Command acceptance alone does not prove movement.",
            "If the vehicle arms but motors remain stopped, inspect MOT_SPIN_ARM, spool state, interlock and safety state.",
            "If Motor Test fails on all motors, inspect protocol, output mapping and ESC power before changing parameters.",
            "Latest command failure: [untrusted](https://example.test).",
        };
        var markdown = MotorOutputDocumentFactory.Create(string.Join('\n', configuration), string.Join('\n', guidance)).Markdown;
        Assert.Contains("FRAME_CLASS = 1\nMOT_SPIN_ARM = 0.1\nBRD_SAFETY_DEFLT = 0\nSERVO1_FUNCTION = 33\n// Q_FRAME_TYPE = unavailable", markdown);
        foreach (var line in configuration.Skip(5).Concat(guidance))
        {
            Assert.Contains(UserDocumentBuilder.Escape(line), markdown);
        }
        Assert.Empty(Markdown.Parse(markdown).Descendants<LinkInline>());
    }
}
