namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.InstallFirmware;

/// <summary>Describes one curated firmware support destination.</summary>
public sealed record FirmwareSupportLink
{
    /// <summary>Initializes a validated support link.</summary>
    public FirmwareSupportLink(string title, string description, Uri uri, FirmwareSupportCategory category, bool isThirdParty = false)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("A support-link title is required.", nameof(title)) : title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? throw new ArgumentException("A support-link description is required.", nameof(description)) : description.Trim();
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Firmware support links must use absolute HTTPS URIs.", nameof(uri));
        }

        Uri = uri;
        Category = category;
        IsThirdParty = isThirdParty;
    }

    /// <summary>Gets the display title.</summary>
    public string Title { get; }

    /// <summary>Gets the concise destination description.</summary>
    public string Description { get; }

    /// <summary>Gets the HTTPS destination.</summary>
    public Uri Uri { get; }

    /// <summary>Gets the support category.</summary>
    public FirmwareSupportCategory Category { get; }

    /// <summary>Gets whether the resource is maintained by a third party.</summary>
    public bool IsThirdParty { get; }
}

