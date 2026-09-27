using Avalonia.Controls;
using Avalonia.Interactivity;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>A movable receiver-history panel that inherits the radio page's data context.</summary>
public partial class RadioChannelHistoryView : UserControl
{
    public RadioChannelHistoryView() => InitializeComponent();
    private void ResetHistory(object? sender, RoutedEventArgs e) => HistoryPlot.Clear();
}
