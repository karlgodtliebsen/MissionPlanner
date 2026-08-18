using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.MavLink.Parameters;
using UraniumUI.Material.Dialogs;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// View for editing vehicle parameters in a text format. It allows users to input parameter values and updates the corresponding parameters in a provided list.
/// </summary>
public partial class ParametersEditorView : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParametersEditorView"/> class.
    /// </summary>
    /// <param name="viewModel">The view model to bind to the view.</param>
    public ParametersEditorView(ParametersEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

/// <summary>
/// ViewModel for editing vehicle parameters in a text format. It allows users to input parameter values and updates the corresponding parameters in a provided list.
/// </summary>
/// <param name="dialogService"></param>
public partial class ParametersEditorViewModel(IExtendedDialogService dialogService) : ObservableObject
{
    /// <summary>
    /// Gets or sets the text input by the user, which contains parameter values in a specific format. This property is bound to the view and is used to update the parameters in the provided list.
    /// </summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;


    [RelayCommand]
    private Task CloseAsync(CancellationToken cancellationToken)
    {
        return dialogService.CloseAsync(true, cancellationToken);
    }

    /// <summary>
    /// Updates the parameters in the provided list based on the current Text property.
    /// </summary>
    /// <param name="fullParametersList">The list of vehicle parameters to update.</param>
    /// <returns>The number of parameters that were updated.</returns>
    public List<VehicleParameter> UpdateParameters(List<VehicleParameter> fullParametersList)
    {
        if (string.IsNullOrEmpty(Text))
        {
            return [];
        }

        var result = new List<VehicleParameter>();

        //format FRAME_CLASS=1//Quad
        var lines = Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var parts = line.Split(["=", "//"], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var name = parts[0];
            var parameter = fullParametersList.FirstOrDefault(p => p.Name == name);
            if (parameter is not null)
            {
                fullParametersList.Remove(parameter);

                var p = parts[1];
                if (string.IsNullOrEmpty(p))
                {
                    continue;
                }

                if (!float.TryParse(p, out var v))
                {
                    continue;
                }

                var param = parameter with { Value = v };
                fullParametersList.Add(param);
                result.Add(param);
            }
        }

        return result;
    }
}
