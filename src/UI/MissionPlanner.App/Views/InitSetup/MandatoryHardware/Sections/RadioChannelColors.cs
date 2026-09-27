namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Consistent receiver colours for the live meters and history traces.</summary>
internal static class RadioChannelColors
{
    public static string For(string? function, int number) => function?.ToUpperInvariant() switch
    {
        "ROLL" => "#E84468",
        "PITCH" => "#46BF58",
        "YAW" => "#4C88DA",
        "THROTTLE" => "#ECA525",
        _ => (number % 4) switch { 0 => "#AF77C7", 1 => "#24A99A", 2 => "#72AA65", _ => "#C1AC49" }
    };
}
