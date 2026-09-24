using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MissionPlanner.App.Presentation.Documents;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Deterministic document contracts, untrusted-text handling, and semantic refresh boundaries.</summary>
public sealed class CompassDocumentTests
{
    /// <summary>Generated structure remains fixed even with Markdown, HTML, and multiline dynamic values.</summary>
    [Fact]
    public void DynamicTextCannotInjectMarkup()
    {
        const string hostile = "* _ ` [link](file:///secret) ![img](https://example.test/image) # | \\ <img>\r\n# injected";
        var text = new UserDocumentBuilder().Heading(hostile).Paragraph(hostile).Bullet(hostile)
            .Note(hostile).Table("Name", "Value", [(hostile, hostile)]).Build().Markdown;
        var parsed = Markdown.Parse(text, new MarkdownPipelineBuilder().UsePipeTables().Build());
        Assert.Empty(parsed.Descendants<LinkInline>());
        Assert.Empty(parsed.Descendants<HtmlInline>());
        Assert.Empty(parsed.Descendants<EmphasisInline>());
        Assert.Single(parsed.Descendants<HeadingBlock>());
        Assert.DoesNotContain("\n# injected", text);
    }

    /// <summary>Headings, empty values, code delimiters and tables have stable source output.</summary>
    [Fact]
    public void BuilderGolden()
    {
        Assert.Equal("", UserDocumentBuilder.Escape(null));
        Assert.Equal("a b c", UserDocumentBuilder.Escape("a\r\nb\nc"));
        Assert.Equal("`` a`b ``", UserDocumentBuilder.Code("a`b"));
        Assert.Equal("—", UserDocumentBuilder.Code(null));
        Assert.Equal("## Status\n\n| Parameter | Value |\n| --- | --- |\n| ` COMPASS_ENABLE ` | ` 0 ` |\n",
            new UserDocumentBuilder().Heading("Status").Paragraph(null)
                .Table("Parameter", "Value", [("COMPASS_ENABLE", "0")], true).Build().Markdown);
    }

    /// <summary>Pipe-table code cells preserve punctuation without creating extra cells or links.</summary>
    [Theory]
    [InlineData("a|b")]
    [InlineData("a\\|b")]
    [InlineData("a`[link](https://example.test)")]
    public void CodeTableRoundTrips(string value)
    {
        var markdown = new UserDocumentBuilder().Table("Name", "Value", [("PARAM", value)], true).Build().Markdown;
        var parsed = Markdown.Parse(markdown, new MarkdownPipelineBuilder().UsePipeTables().Build());
        Assert.Equal(new[] { "PARAM", value }, parsed.Descendants<CodeInline>().Select(code => code.Content));
        Assert.Empty(parsed.Descendants<LinkInline>());
    }

    /// <summary>Loading and disconnected reports cannot expose stale live values.</summary>
    [Theory]
    [InlineData(false, false, "Disconnected. Reconnect to read the current flight-controller state.")]
    [InlineData(true, true, "Loading compass parameters… Current values are not yet available.")]
    public void UnavailableGolden(bool online, bool loading, string expected)
    {
        var document = new CompassSetupDocumentFactory().Create(Context() with { Online = online, Loading = loading });
        Assert.Equal("## Compass status\n\n" + UserDocumentBuilder.Escape(expected) + "\n", document.Markdown);
    }

    /// <summary>Major live variants explain the authoritative facts without promoting history or pending edits.</summary>
    [Theory]
    [InlineData(false, "Disabled", "Compass disabled; yaw does not require compass.")]
    [InlineData(false, "Disabled", "Yaw source requires compass.")]
    [InlineData(true, "Healthy", "Compass configuration is valid.")]
    [InlineData(true, "Not detected", "Enabled but no device detected.")]
    [InlineData(true, "Unhealthy", "Calibration required.")]
    public void StatusVariants(bool enabled, string health, string validation)
    {
        var context = Context();
        var state = context.State! with
        {
            Current = context.State!.Current with { Enabled = enabled },
            Health = health,
            Validation = validation
        };
        var text = new CompassSetupDocumentFactory().Create(context with { State = state, Desired = state.Current }).Markdown;
        Assert.Contains(enabled ? "The compass is enabled" : "The compass is disabled", text);
        Assert.Contains(UserDocumentBuilder.Escape("Health: " + health + "."), text);
        Assert.Contains(UserDocumentBuilder.Escape(validation), text);
        Assert.DoesNotContain("Pending changes", text);
        Assert.DoesNotContain("MISSING_PARAM", text);
    }

    /// <summary>Calibration states produce explicit workflow explanations.</summary>
    [Theory]
    [InlineData(CompassCalibrationWorkflowState.Preparing, "Preparing calibration")]
    [InlineData(CompassCalibrationWorkflowState.Running, "Calibration is in progress")]
    [InlineData(CompassCalibrationWorkflowState.PendingAcceptance, "Accept results to save them")]
    [InlineData(CompassCalibrationWorkflowState.Success, "results were accepted")]
    [InlineData(CompassCalibrationWorkflowState.Failed, "Calibration failed")]
    [InlineData(CompassCalibrationWorkflowState.Cancelled, "Calibration was cancelled")]
    [InlineData(CompassCalibrationWorkflowState.Disconnected, "interrupted by disconnection")]
    public void CalibrationVariants(CompassCalibrationWorkflowState calibration, string expected)
    {
        Assert.Contains(expected, new CompassSetupDocumentFactory().Create(Context() with { Calibration = calibration }).Markdown);
    }

    /// <summary>Pending desired values never replace the current parameter evidence.</summary>
    [Fact]
    public void PendingCurrentAndArmingEvidence()
    {
        var context = Context();
        var factory = new CompassSetupDocumentFactory();
        var pending = context with { Desired = context.State!.Current with { Enabled = true }, RequiresReboot = true };
        var text = factory.Create(pending).Markdown;
        Assert.Contains("The compass is disabled", text);
        Assert.Contains("These values are not active", text);
        Assert.Contains("Off → On", text);
        Assert.Contains("| ` COMPASS_ENABLE ` | ` 0 ` |", text);
        Assert.Contains("Reboot required", text);
        var blocker = context.State with { ArmingImpact = "Current compass pre-arm issue: Compass inconsistent" };
        Assert.Contains("Compass inconsistent", factory.Create(context with { State = blocker }).Markdown);
        var historical = context.State with { Diagnostics = ["Historical: Compass inconsistent"] };
        Assert.DoesNotContain("Compass inconsistent", factory.Create(context with { State = historical }).Markdown);
        Assert.Contains("unsupported", factory.Create(context with { State = context.State with { IsSupported = false } }).Markdown);
    }

    /// <summary>Observation timestamps and raw diagnostics do not trigger a new status document.</summary>
    [Fact]
    public void SemanticEqualityIgnoresUnrelatedRefreshes()
    {
        var context = Context();
        Assert.True(context.HasSameContent(context with
        {
            State = context.State! with { ObservedAt = DateTimeOffset.UtcNow, Diagnostics = ["New health timestamp"] }
        }));
        Assert.False(context.HasSameContent(context with { State = context.State! with { Health = "Unhealthy" } }));
        Assert.False(context.HasSameContent(context with { Online = false }));
        Assert.False(context.HasSameContent(context with { Calibration = CompassCalibrationWorkflowState.Running }));
    }

    private static CompassDocumentContext Context()
    {
        var configuration = new CompassConfiguration { Enabled = false, YawSource = CompassYawSource.None };
        var state = new CompassSetupState(new VehicleId(1, 1), configuration,
        [
            new(CompassSetting.Enabled, "Enable compass", "", "COMPASS_ENABLE", 0, null,
                [new(0, "Off"), new(1, "On")], true, false, null),
            new(CompassSetting.YawSource, "Yaw source", "", "EK3_SRC1_YAW", 0, null,
                [new(0, "None"), new(1, "Compass")], true, false, null),
            new(CompassSetting.SecondaryOrientation, "Missing", "", "MISSING_PARAM", null, null,
                [], false, false, null)
        ], [], "Disabled", "Valid", "No current compass arming issue", true, null, DateTimeOffset.UnixEpoch, []);
        return new(state, configuration, true, false, "Valid", CompassCalibrationWorkflowState.NotStarted, "", "", null, false);
    }

    /// <summary>Full Markdown golden files make user-facing variants explicitly reviewable.</summary>
    [Theory]
    [InlineData("loading")]
    [InlineData("disconnected")]
    [InlineData("unsupported")]
    [InlineData("disabled")]
    [InlineData("conflict")]
    [InlineData("healthy")]
    [InlineData("no-device")]
    [InlineData("calibration-required")]
    [InlineData("calibrating")]
    [InlineData("calibrated")]
    [InlineData("calibration-failed")]
    [InlineData("blocker")]
    [InlineData("historical")]
    [InlineData("pending")]
    [InlineData("escaping")]
    public void ReportGolden(string variant)
    {
        var context = Context();
        var state = context.State!;
        context = variant switch
        {
            "loading" => context with { Loading = true },
            "disconnected" => context with { Online = false },
            "unsupported" => context with { State = state with { IsSupported = false, UnsupportedReason = "Requires supported ArduPilot firmware." } },
            "conflict" => context with { State = state with { Current = state.Current with { YawSource = CompassYawSource.Compass }, Validation = "Yaw source requires compass, but compass is disabled." } },
            "healthy" => context with { State = state with { Current = state.Current with { Enabled = true }, Health = "Healthy", DetectedDevices = ["Compass 1: Device ID 123456"] } },
            "no-device" => context with { State = state with { Current = state.Current with { Enabled = true }, Health = "Not detected", Validation = "Compass is enabled, but no primary compass device is detected.", DetectedDevices = ["Compass 1: Not detected"] } },
            "calibration-required" => context with { State = state with { Current = state.Current with { Enabled = true }, Health = "Unhealthy", Validation = "Compass calibration required." } },
            "calibrating" => context with { Calibration = CompassCalibrationWorkflowState.Running, Instruction = "Rotate the vehicle.", Progress = "Compass 1: 50%" },
            "calibrated" => context with { Calibration = CompassCalibrationWorkflowState.Success, Quality = "Accepted" },
            "calibration-failed" => context with { Calibration = CompassCalibrationWorkflowState.Failed, Instruction = "Compass 1: Failed" },
            "blocker" => context with { State = state with { ArmingImpact = "Current compass pre-arm issue: Compass inconsistent" } },
            "historical" => context with { State = state with { Diagnostics = ["Historical: Compass inconsistent"] } },
            "pending" => context with { Desired = state.Current with { Enabled = true }, RequiresReboot = true },
            "escaping" => context with { State = state with { DetectedDevices = ["*[label](file:///secret) | <img>\n# device"] } },
            _ => context
        };
        if (variant != "pending")
        {
            context = context with { Desired = context.State!.Current };
        }
        if (variant is "calibrating" or "calibrated" or "calibration-failed")
        {
            var configuration = context.State!.Current with { Enabled = true };
            context = context with { State = context.State with { Current = configuration, Health = "Unknown" }, Desired = configuration };
        }
        context = context with { State = context.State! with
            {
                Settings = context.State!.Settings.Select(setting => setting with { Current = context.State.Current.Get(setting.Setting) }).ToArray()
            } };
        var actual = new CompassSetupDocumentFactory().Create(context).Markdown;
        var path = Path.Combine(RepositoryRoot(), "src", "Tests", "MissionPlanner.AvaloniaUI.Tests", "Snapshots", "Compass", variant + ".md");
        Assert.Equal(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal), actual);
    }

    /// <summary>Guards the backend dependency boundary and keeps renderer APIs out of page ViewModels.</summary>
    [Fact]
    public void PresentationDependenciesStayOutOfCore()
    {
        var backend = typeof(CompassSetupState).Assembly;
        Assert.DoesNotContain(backend.GetReferencedAssemblies(), reference =>
            reference.Name is "MissionPlanner.App" or "LiveMarkdown.Avalonia" or "Markdig");
        Assert.DoesNotContain(backend.GetTypes(), type => type.Name == nameof(UserDocument));
        var root = RepositoryRoot();
        var viewModels = Directory.EnumerateFiles(Path.Combine(root, "src", "UI", "MissionPlanner.App"), "*ViewModel*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar));
        Assert.All(viewModels, path => Assert.DoesNotContain("LiveMarkdown", File.ReadAllText(path)));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
