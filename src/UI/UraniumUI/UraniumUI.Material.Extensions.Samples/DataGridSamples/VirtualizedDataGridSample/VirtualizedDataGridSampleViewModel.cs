using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class VirtualizedDataGridSampleViewModel : ObservableObject
{
    private readonly List<ItemViewModel> allParameterItems = [];
    [ObservableProperty] public partial int NumberOfRows { get; set; } = 100;

    ///// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ItemViewModel> Parameters { get; } = [];

    /// <summary>Initializes the Full Parameters List tab.</summary>
    public VirtualizedDataGridSampleViewModel()
    {
        for (var i = 0; i < NumberOfRows; i++)
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
}
