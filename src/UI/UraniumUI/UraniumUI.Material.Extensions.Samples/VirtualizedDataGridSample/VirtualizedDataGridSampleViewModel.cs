using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;

namespace UraniumUI.Material.Extensions.Samples.VirtualizedDataGridSample;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class VirtualizedDataGridSampleViewModel : ObservableObject
{
    private readonly List<ItemViewModel> allParameterItems = [];

    /// <summary>Initializes the Full Parameters List tab.</summary>
    public VirtualizedDataGridSampleViewModel()
    {
        for (var i = 0; i < 2000; i++)
        {
            allParameterItems.Add(new ItemViewModel
            {
                AValue = i,
                Name = $"Name {i}",
                DisplayName = Ulid.NewUlid().ToString(),
                Value = i * 10,
                LiveValue = i * 5,
                StepSize = 1,
                SelectedValue = $"Selected {i}"
            });
        }

        Parameters.AddRange(allParameterItems);
    }

    ///// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ItemViewModel> Parameters { get; } = [];
}

public partial class ItemViewModel : ObservableObject
{
    [ObservableProperty] public partial double AValue { get; set; }

    [ObservableProperty] public partial string Name { get; set; } = null!;
    [ObservableProperty] public partial string? DisplayName { get; set; }
    [ObservableProperty] public partial double Value { get; set; }
    [ObservableProperty] public partial double LiveValue { get; set; }
    [ObservableProperty] public partial double StepSize { get; set; }
    [ObservableProperty] public partial string? SelectedValue { get; set; }
}
