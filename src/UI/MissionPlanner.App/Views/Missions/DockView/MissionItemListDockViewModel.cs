using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MissionPlanner.App.Views.Missions.DockView;

public partial class MissionItemListDockViewModel : ObservableObject, IDisposable
{
    [ObservableProperty] public partial bool IsExpanded { get; set; }

    [ObservableProperty] public partial double CalculatedWidth { get; set; }
    [ObservableProperty] public partial string GuidingText { get; set; } = "<<";
    [ObservableProperty] public partial double ShrinkWidth { get; set; }
    [ObservableProperty] public partial double ExpandWidth { get; set; }

    /// <summary>
    /// Occurs when the width request changes.
    /// </summary>
    public event EventHandler<WidthEventArgs>? WidthRequestChanged;

    /// <inheritdoc />
    public MissionItemListDockViewModel()
    {
    }

    partial void OnShrinkWidthChanged(double value)
    {
        if (Math.Abs(ShrinkWidth - value) > 1)
        {
            ShrinkWidth = value;
        }

        var calcWidth = IsExpanded ? ExpandWidth : ShrinkWidth;
        if (Math.Abs(CalculatedWidth - calcWidth) > 1)
        {
            CalculatedWidth = calcWidth;
            WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth, IsExpanded));
        }
    }

    partial void OnExpandWidthChanged(double value)
    {
        if (Math.Abs(ExpandWidth - value) > 1)
        {
            ExpandWidth = value;
        }

        var calcWidth = IsExpanded ? ExpandWidth : ShrinkWidth;
        if (Math.Abs(CalculatedWidth - calcWidth) > 1)
        {
            CalculatedWidth = calcWidth;
            WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth, IsExpanded));
        }
    }

    [RelayCommand]
    private void Expand()
    {
        IsExpanded = !IsExpanded;
        CalculatedWidth = IsExpanded ? ExpandWidth : ShrinkWidth;
        GuidingText = IsExpanded ? ">>" : "<<";
        WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth, IsExpanded));
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
