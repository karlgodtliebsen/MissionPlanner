using MissionPlanner.Core.Setup.Advanced.Warnings;

namespace MissionPlanner.Library.Windows;

/// <summary>Persists warnings with atomic replacement in the current Windows user's application data.</summary>
public sealed class WindowsWarningRuleStore : IWarningRuleStore
{
    private readonly string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MissionPlanner Next Gen", "advanced-warnings.json");

    /// <inheritdoc />
    public async ValueTask<string?> ReadAsync(CancellationToken token)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        if (new FileInfo(path).Length > WarningRuleRepository.MaximumDocumentLength * 4L)
        {
            throw new IOException("Warning storage exceeds the size limit.");
        }
        return await File.ReadAllTextAsync(path, token);
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(string document, CancellationToken token) => ReplaceAsync(path, document, token);

    /// <inheritdoc />
    public ValueTask QuarantineAsync(string document, CancellationToken token) => ReplaceAsync(path + ".recovery", document, token);

    private static async ValueTask ReplaceAsync(string destination, string document, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, document, token);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
