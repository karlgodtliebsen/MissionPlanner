using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

/// <summary>Provides the searchable full parameter list through the shared safe editing session.</summary>
public partial class VirtualizedDataGridSampleViewModel : ObservableObject
{
    [ObservableProperty] public partial int NumberOfRows { get; set; } = 100;

    ///// <summary>Gets the currently visible parameter rows.</summary>
    public ObservableRangeCollection<ItemViewModel> Parameters { get; } = [];

    public VirtualizedDataGridSampleViewModel()
    {
        PopulateRows(NumberOfRows);
    }

    /// <summary>Initializes the Full Parameters List tab.</summary>
    partial void OnNumberOfRowsChanged(int value)
    {
        PopulateRows(value);
    }

    private void PopulateRows(int value)
    {
        Parameters.Clear();
        List<ItemViewModel> allParameterItems = [];
        for (var i = 0; i < value; i++)
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
