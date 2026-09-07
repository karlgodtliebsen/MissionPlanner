namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Contains one context-sensitive help result.</summary>
public sealed record FirmwareContextHelp(string Title, string Content, FirmwareSupportCategory? LinkCategory = null);

