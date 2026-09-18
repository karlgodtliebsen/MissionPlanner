namespace MissionPlanner.Library.Logging;

/// <summary>Shared bounded streaming import, independent of platform file pickers.</summary>
public sealed class LogFileOperations(ILogStorage storage)
{
    /// <summary>Imports without overwriting existing files, deleting partial imports on failure.</summary>
    public async Task<string> ImportAsync(LogStorageArea area, string fileName, Stream input,
        CancellationToken cancellationToken = default)
    {
        LogNames.Validate(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var suffix = 0; suffix < 10000; suffix++)
        {
            var name = suffix == 0 ? fileName : $"{stem}-{suffix}{extension}";
            Stream output;
            try
            {
                output = await storage.CreateAsync(area, name, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                if (!(await storage.ListAsync(area, cancellationToken).ConfigureAwait(false)).Any(item => item.Id == name))
                {
                    throw;
                }

                continue;
            }

            try
            {
                await using (output.ConfigureAwait(false))
                {
                    await input.CopyToAsync(output, 65536, cancellationToken).ConfigureAwait(false);
                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }

                return name;
            }
            catch
            {
                try
                {
                    await storage.DeleteAsync(area, name, CancellationToken.None).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    // Preserve the original import error if cleanup also fails.
                }

                throw;
            }
        }

        throw new IOException("Unable to allocate a unique imported log name.");
    }
}
