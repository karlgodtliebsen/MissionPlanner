using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MissionPlanner.Analysis.Frequency;
using MissionPlanner.Analysis.Vibration;
using MissionPlanner.Core.Analysis;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class FftIntegrationTests
{
    private readonly DataFlashImuSampleProvider provider = new(new DataFlashRecordReader());

    [Fact]
    public void SmallDataFlashManifestDrivesParserAndSpectrumRegression()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "TestData", "Frequency", "DataFlash");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "regular-128.json")));
        using var stream = File.OpenRead(Path.Combine(folder, manifest.RootElement.GetProperty("file").GetString()!));
        var log = provider.Read(stream, "fixture", false, TestContext.Current.CancellationToken);
        var source = Assert.Single(log.Series.Where(s => s.Axis == VibrationAxis.X));
        Assert.Equal(256, source.SampleCount);
        Assert.InRange(source.SampleRateHz, 1023.99, 1024.01);
        Assert.Equal(0, source.Instance);
        Assert.Equal("rad/s", source.Unit);
        var result = Workspace().Analyze(log, source, new(source.StartTimeSeconds, source.EndTimeSeconds, 256, 0, null),
            null, null, [], TestContext.Current.CancellationToken);
        var peak = Assert.Single(result.Peaks);
        Assert.InRange(Math.Abs(peak.FrequencyHz - manifest.RootElement.GetProperty("expected").GetProperty("fundamentalHz").GetDouble()), 0, result.Spectrum.ResolutionHz);
        Assert.InRange(peak.Amplitude, 0.999, 1.001);
    }

    [Fact]
    public void BinaryBatchUsesHeaderRateMultiplierAxisAndStartTime()
    {
        using var stream = BatchStream();
        var log = provider.Read(stream, "batch.bin", true, TestContext.Current.CancellationToken);
        Assert.Equal(3, log.Series.Length);
        var x = log.Series.Single(s => s.Axis == VibrationAxis.X);
        Assert.Equal(64, x.SampleCount);
        Assert.Equal(1024, x.SampleRateHz);
        Assert.Equal(1, x.StartTimeSeconds);
        Assert.Equal(42, x.BatchNumber);
        Assert.Equal(2, x.Instance);
        Assert.Equal(1, x.Samples[2], 10);
        Assert.Equal(1 + 63.0 / 1024, x.EndTimeSeconds, 10);
        var result = Workspace().Analyze(log, x, new(1, x.EndTimeSeconds, 32, 0, null), null, null,
            [new NotchFilter(128, 20, 40)], TestContext.Current.CancellationToken);
        Assert.Equal(3, result.Spectrogram.Frames.Length);
        Assert.Equal(128, Assert.Single(result.Peaks).FrequencyHz);
        Assert.InRange(result.ProposedSimulation[4].Gain, 0.009999, 0.010001);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void IncompleteOrOutOfOrderBatchIsNotAnalyzed(bool missingPacket, bool wrongSequence)
    {
        using var stream = BatchStream(missingPacket, wrongSequence);
        var log = provider.Read(stream, "bad-batch.bin", true, TestContext.Current.CancellationToken);
        Assert.Empty(log.Series);
        Assert.Contains(log.Diagnostics, d => d.Contains("rejected", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GapsAndDuplicateTimestampsSplitRegularSources()
    {
        var text = new StringBuilder("FMT, 150, 32, GYR, QBQfff, TimeUS, I, SampleUS, GyrX, GyrY, GyrZ\n");
        foreach (var timestamp in new[] { 0, 1000, 2000, 3000, 10000, 11000, 12000, 13000 })
        {
            text.AppendLine($"GYR,{timestamp},0,{timestamp},1,2,3");
        }
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
        var result = provider.Read(stream, "gap.log", false, TestContext.Current.CancellationToken);
        Assert.Equal(6, result.Series.Length);
        Assert.All(result.Series, s => Assert.Equal(4, s.SampleCount));
        Assert.Contains(result.Diagnostics, d => d.Contains("split"));
    }

    [Fact]
    public void TimestampDriftCannotBeSilentlyTreatedAsUniform()
    {
        var text = new StringBuilder("FMT, 150, 32, GYR, QBQfff, TimeUS, I, SampleUS, GyrX, GyrY, GyrZ\n");
        for (var i = 0; i < 400; i++)
        {
            var time = i * 1000 + (i < 200 ? 0 : (i - 200) * 10);
            text.AppendLine($"GYR,{time},0,{time},1,2,3");
        }
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
        var result = provider.Read(stream, "drift.log", false, TestContext.Current.CancellationToken);
        Assert.Empty(result.Series);
        Assert.Contains(result.Diagnostics, d => d.Contains("drift"));
    }

    [Fact]
    public void TruncatedFinalBatchIsRejectedAndTextWithoutFmtFailsExplicitly()
    {
        using var stream = BatchStream();
        var bytes = stream.ToArray();
        using var truncated = new MemoryStream(bytes[..^10]);
        var log = provider.Read(truncated, "truncated.bin", true, TestContext.Current.CancellationToken);
        Assert.Empty(log.Series);
        Assert.Contains(log.Diagnostics, d => d.Contains("Truncated"));
        using var text = new MemoryStream(Encoding.UTF8.GetBytes("GYR,0,0,0,1,2,3"));
        Assert.Throws<InvalidDataException>(() => provider.Read(text, "no-fmt.log", false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RealLogWithLateFormatsAndTruncatedTailPreservesCompleteBatches()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "RealFft", "df-isb-fft-sample.bin");
        using var stream = File.OpenRead(path);
        var log = provider.Read(stream, "df-isb-fft-sample.bin", true, TestContext.Current.CancellationToken);
        var batches = log.Series.Where(s => s.BatchNumber.HasValue).ToArray();
        Assert.Equal(384, batches.Length);
        Assert.All(batches, s => Assert.Equal(1024, s.SampleCount));
        Assert.Contains(log.Diagnostics, d => d.Contains("Truncated"));
        Assert.All(log.Series, s => Assert.True(s.SampleCount >= 4));
    }

    [Fact]
    public void OutputPwmRemainsDistinctFromActualRpm()
    {
        const string text = "FMT,150,15,RCOU,QHH,TimeUS,C1,C2\nRCOU,1000000,1500,1600\nFMT,151,20,ESC,QBff,TimeUS,Instance,RPM,RawRPM\nESC,1000000,2,6000,6100\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var result = provider.Read(stream, "motor.log", false, TestContext.Current.CancellationToken);
        Assert.Equal(2, result.MotorSamples.Count(m => !m.IsRpm));
        Assert.Equal(6000, Assert.Single(result.MotorSamples.Where(m => m.IsRpm)).Value);
    }

    [Fact]
    public void StaticParametersUseMetadataAndDynamicSettingsHaveNoStaticCoverageClaim()
    {
        var values = StaticValues();
        var definition = new ParameterMetadata("INS_HNTCH_FREQ", "Frequency", "Firmware supplied center description", "Hz",
            null, null, null, null, null, null, false, false);
        var snapshot = NotchParameterAnalysisService.Build("saved", values, new Dictionary<string, ParameterMetadata> { [definition.Name] = definition });
        Assert.Equal(2, snapshot.StaticFilters.Length);
        Assert.Equal(128, snapshot.StaticFilters[0].CenterHz);
        Assert.Equal(256, snapshot.StaticFilters[1].CenterHz);
        Assert.Equal(definition.Description, snapshot.Values.Single(p => p.Name == definition.Name).Description);
        values["INS_HNTCH_MODE"] = 3;
        var dynamic = NotchParameterAnalysisService.Build("saved", values, new Dictionary<string, ParameterMetadata>());
        Assert.Empty(dynamic.StaticFilters);
        Assert.Contains(dynamic.Limitations, l => l.Contains("dynamic"));
    }

    [Fact]
    public async Task SavedParameterImportUsesSelectedFirmwareMetadataWithoutWrites()
    {
        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetAllMetadataAsync(VehicleType.ArduCopter, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyDictionary<string, ParameterMetadata>>(new Dictionary<string, ParameterMetadata>()));
        var registry = Substitute.For<IVehicleParameterRegistry>();
        var service = new NotchParameterAnalysisService(Substitute.For<IActiveVehicleContext>(), registry, metadata);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join('\n', StaticValues().Select(p => $"{p.Key} = {p.Value.ToString(CultureInfo.InvariantCulture)} // saved"))));
        var snapshot = await service.ReadSavedAsync(stream, "saved.param", "ArduCopter", TestContext.Current.CancellationToken);
        Assert.Equal(2, snapshot.StaticFilters.Length);
        Assert.Empty(registry.ReceivedCalls());
        await metadata.Received(1).GetAllMetadataAsync(VehicleType.ArduCopter, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void WorkspaceRejectsCrossUnitBaselineAndInvalidIntervals()
    {
        using var stream = BatchStream();
        var log = provider.Read(stream, "batch", true, TestContext.Current.CancellationToken);
        var source = log.Series[0];
        var baseline = new FftBaseline("wrong units", "m/s²", new FftAnalyzer().Analyze(new double[32], 1024, new FftOptions { Size = 32 }));
        Assert.Throws<ArgumentException>(() => Workspace().Analyze(log, source, new(1, source.EndTimeSeconds, 32, 0, null),
            baseline, null, [], TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => Workspace().Analyze(log, source, new(0, 1, 32, 0, null), null, null, [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void CancellationPreventsParsing()
    {
        using var stream = BatchStream();
        Assert.Throws<OperationCanceledException>(() => provider.Read(stream, "batch", true, new CancellationToken(true)));
    }

    private static Dictionary<string, double> StaticValues() => new()
    {
        ["INS_HNTCH_ENABLE"] = 1, ["INS_HNTCH_MODE"] = 0, ["INS_HNTCH_OPTS"] = 0,
        ["INS_HNTCH_FREQ"] = 128, ["INS_HNTCH_BW"] = 20, ["INS_HNTCH_ATT"] = 40, ["INS_HNTCH_HMNCS"] = 3
    };

    [Fact]
    public void WorkspacePairsRpmOnlyAcrossShortTimeBrackets()
    {
        const double rate = 1024;
        var samples = Enumerable.Range(0, 4096).Select(i =>
        {
            var time = i / rate;
            return Math.Sin(2 * Math.PI * (80 * time + 20 * time * time));
        }).ToImmutableArray();
        var source = new ImuSampleSeries("sweep", 0, "Gyro", VibrationAxis.X, "rad/s", rate, 0, samples, null);
        var motors = Enumerable.Range(0, 17).Select(i =>
            new MotorLogSample(0, "ESC RPM", i * 0.25, (80 + 40 * i * 0.25) * 60, true)).ToImmutableArray();
        var log = new ImuLogData("sweep", [source], motors, []);
        var request = new FftAnalysisRequest(0, source.EndTimeSeconds, 256, 20, 300);
        var result = Workspace().Analyze(log, source, request, null, null, [], TestContext.Current.CancellationToken);
        var correlation = Assert.Single(result.Correlations);
        Assert.InRange(correlation.Correlation!.Value, 0.99, 1);
        Assert.InRange(correlation.RootMeanSquareErrorHz, 0, 4);
        Assert.All(correlation.Evidence, pair => Assert.InRange(pair.TimeSeconds, 0, 4));
        var gap = log with { MotorSamples = [motors[0], motors[^1]] };
        var gapped = Workspace().Analyze(gap, source, request, null, null, [], TestContext.Current.CancellationToken);
        Assert.Empty(gapped.Correlations);
    }

    private static FftWorkspaceService Workspace()
    {
        var fft = new FftAnalyzer();
        return new FftWorkspaceService(new SpectrumAverager(fft), new SpectrogramAnalyzer(fft), new PeakDetector(),
            new HarmonicDetector(), new MotorFrequencyCorrelator(), new ResonanceDetector(), new SpectrumComparer(), new NotchFilterSimulator());
    }

    private static MemoryStream BatchStream(bool missingPacket = false, bool wrongSequence = false)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        void Header(byte id)
        {
            writer.Write(new byte[] { 0xA3, 0x95, id });
        }
        void Fixed(string value, int length)
        {
            var bytes = new byte[length];
            Encoding.ASCII.GetBytes(value).CopyTo(bytes, 0);
            writer.Write(bytes);
        }
        void Fmt(byte id, byte length, string name, string types, string columns)
        {
            Header(128);
            writer.Write(id);
            writer.Write(length);
            Fixed(name, 4);
            Fixed(types, 16);
            Fixed(columns, 64);
        }
        Fmt(150, 31, "ISBH", "QHBBHHQf", "TimeUS,N,type,instance,mul,smp_cnt,SampleUS,smp_rate");
        Fmt(151, 207, "ISBD", "QHHaaa", "TimeUS,N,seqno,x,y,z");
        Header(150);
        writer.Write(1000000UL);
        writer.Write((ushort)42);
        writer.Write((byte)1);
        writer.Write((byte)2);
        writer.Write((ushort)1000);
        writer.Write((ushort)64);
        writer.Write(1000000UL);
        writer.Write(1024f);
        for (var packet = 0; packet < (missingPacket ? 1 : 2); packet++)
        {
            Header(151);
            writer.Write(1000000UL);
            writer.Write((ushort)42);
            writer.Write((ushort)(wrongSequence ? packet + 1 : packet));
            for (var axis = 0; axis < 3; axis++)
            {
                for (var i = 0; i < 32; i++)
                {
                    writer.Write((short)(axis == 0 ? Math.Round(1000 * Math.Sin(2 * Math.PI * 128 * (packet * 32 + i) / 1024)) : 0));
                }
            }
        }
        stream.Position = 0;
        return stream;
    }
}
