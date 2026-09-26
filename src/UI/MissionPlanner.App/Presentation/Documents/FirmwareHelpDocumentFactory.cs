using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews.Models;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Builds the shared offline firmware help article for the Help hub and firmware page.</summary>
public static class FirmwareHelpDocumentFactory
{
    /// <summary>Creates the complete firmware guide from the maintained help sections.</summary>
    public static UserDocument Create()
    {
        var builder = new UserDocumentBuilder().Heading("Install Firmware")
            .Paragraph("Choose firmware, install it, and recover a controller using the guidance below. This guide is available offline; the external resources require an internet connection.");
        foreach (var section in FirmwareSupportContent.Sections)
            builder.Heading(section.Title, 3).Paragraph(section.Content);
        return builder.Build("Install Firmware");
    }
}
