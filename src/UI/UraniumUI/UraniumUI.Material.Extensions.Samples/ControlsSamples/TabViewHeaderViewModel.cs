using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class TabViewHeaderViewModel : ObservableObject, IDisposable
{
    public TabHeaderModel[] TabHeaders { get; set; } =
    [
        new TabHeaderModel { Title = "Tab 1", Content = "Header Content for Tab 1" },
        new TabHeaderModel { Title = "Tab 2", Content = "Header Content for Tab 2" },
        new TabHeaderModel { Title = "Tab 3", Content = "Header Content for Tab 3" }
    ];


    /// <inheritdoc />
    public void Dispose()
    {
    }
}
