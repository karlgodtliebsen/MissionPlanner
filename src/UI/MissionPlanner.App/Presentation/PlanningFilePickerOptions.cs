using Avalonia.Platform.Storage;

namespace MissionPlanner.App.Presentation;

/// <summary>Consistent filename extensions and file-type filters for desktop and browser pickers.</summary>
public static class PlanningFilePickerOptions
{
    /// <summary>Creates save options for the format actually written by the caller.</summary>
    public static FilePickerSaveOptions ForSave(string fileName)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        return new FilePickerSaveOptions
        {
            Title = "Save file",
            SuggestedFileName = fileName,
            DefaultExtension = string.IsNullOrEmpty(extension) ? null : extension,
            FileTypeChoices = string.IsNullOrEmpty(extension) ? null : [FileType($"*.{extension}")]
        };
    }

    /// <summary>Creates open options with supported extensions visible in the filter labels.</summary>
    public static FilePickerOpenOptions ForOpen(string title, IReadOnlyList<string>? patterns)
    {
        IReadOnlyList<FilePickerFileType>? filters = null;
        if (patterns is { Count: > 0 })
        {
            var distinct = patterns.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            filters = distinct.Length == 1
                ? [FileType(distinct[0])]
                : [new FilePickerFileType($"Supported files ({string.Join(", ", distinct)})") { Patterns = distinct },
                    .. distinct.Select(FileType)];
        }
        return new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = filters
        };
    }

    private static FilePickerFileType FileType(string pattern)
    {
        var extension = Path.GetExtension(pattern).TrimStart('.').ToLowerInvariant();
        return new FilePickerFileType($"{extension.ToUpperInvariant()} files ({pattern})")
        {
            Patterns = [pattern],
            MimeTypes = extension switch
            {
                "csv" => ["text/csv"],
                "json" => ["application/json"],
                "txt" => ["text/plain"],
                _ => null
            }
        };
    }
}
