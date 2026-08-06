using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Navigation;

namespace MissionPlanner.App.Views.Missions;

/// <summary>
/// Represents the view for the mission item list dock.
/// </summary>
public partial class MissionItemListDockView : ExtendedContentView<MissionItemListDockViewModel>
{
    /// <summary>
    /// Occurs when the width request changes.
    /// </summary>
    public event EventHandler<WidthEventArgs>? WidthRequestChanged;

    private double ShrinkWidth { get; set; } = 50;
    private double ExpandWidth { get; set; } = 700;

    /// <summary>
    /// Initializes a new instance of the <see cref="MissionItemListDockView"/> class.
    /// </summary>
    public MissionItemListDockView()
    {
        InitializeComponent();
        ViewModel!.ShrinkWidth = ShrinkWidth;
        ViewModel!.ExpandWidth = ExpandWidth;
        ViewModel!.WidthRequestChanged += ViewModel_WidthRequestChanged;
    }

    private void ViewModel_WidthRequestChanged(object? sender, WidthEventArgs e)
    {
        WidthRequestChanged?.Invoke(this, e);
    }
}

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
        // WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth));
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
            WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth));
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
            WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth));
        }
    }

    [RelayCommand]
    private void Expand()
    {
        IsExpanded = !IsExpanded;
        CalculatedWidth = IsExpanded ? ExpandWidth : ShrinkWidth;
        GuidingText = IsExpanded ? ">>" : "<<";
        WidthRequestChanged?.Invoke(this, new WidthEventArgs(CalculatedWidth));
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

public class WidthEventArgs : EventArgs
{
    public double Width { get; }

    public WidthEventArgs(double width)
    {
        Width = width;
    }
}
