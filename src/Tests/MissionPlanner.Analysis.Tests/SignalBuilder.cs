namespace MissionPlanner.Analysis.Tests;

internal sealed class SignalBuilder
{
    private double sampleRate = 1000;
    private double duration = 1.024;
    private double dc;
    private double noiseAmplitude;
    private int seed = 73;
    private readonly List<(double Frequency, double Amplitude)> tones = [];

    internal SignalBuilder WithSampleRate(double value)
    {
        sampleRate = value;
        return this;
    }

    internal SignalBuilder WithDuration(double seconds)
    {
        duration = seconds;
        return this;
    }

    internal SignalBuilder AddSine(double frequency, double amplitude = 1)
    {
        tones.Add((frequency, amplitude));
        return this;
    }

    internal SignalBuilder WithDc(double offset)
    {
        dc = offset;
        return this;
    }

    internal SignalBuilder AddNoise(double amplitude, int randomSeed = 73)
    {
        noiseAmplitude = amplitude;
        seed = randomSeed;
        return this;
    }

    internal double[] Build()
    {
        var random = new Random(seed);
        return Enumerable.Range(0, (int)Math.Round(duration * sampleRate))
            .Select(i => dc + tones.Sum(t => t.Amplitude * Math.Sin(2 * Math.PI * t.Frequency * i / sampleRate))
                + noiseAmplitude * (2 * random.NextDouble() - 1)).ToArray();
    }

    internal static double[] Sweep(int count, double sampleRate, double startHz, double endHz)
    {
        var slope = (endHz - startHz) / ((count - 1) / sampleRate);
        return Enumerable.Range(0, count).Select(i =>
        {
            var time = i / sampleRate;
            return Math.Sin(2 * Math.PI * (startHz * time + 0.5 * slope * time * time));
        }).ToArray();
    }
}
