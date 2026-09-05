using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Osd;
using ParameterItemViewModel = MissionPlanner.App.Models.ParameterItemViewModel;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one discovered OSD item and its placement controls.</summary>
public sealed partial class OsdItemViewModel : ObservableObject
{
    private readonly IParameterEditSession session;
    private readonly Func<OsdItemViewModel, int, int, string?> move;
    private bool loading;

    /// <summary>Initializes an OSD item projection.</summary>
    /// <param name="definition">The discovered item definition.</param>
    /// <param name="session">The shared editing session.</param>
    /// <param name="move">The validated position callback.</param>
    public OsdItemViewModel(
        OsdItemDefinition definition,
        IParameterEditSession session,
        Func<OsdItemViewModel, int, int, string?> move)
    {
        Definition = definition;
        this.session = session;
        this.move = move;
        AdditionalParameters = new ObservableCollection<ParameterItemViewModel>(
            definition.AdditionalParameterNames
                .Select(session.GetField)
                .Where(state => state is not null)
                .Select(state => new ParameterItemViewModel(session, state!)));
        Refresh();
    }

    /// <summary>Gets the discovered item definition.</summary>
    public OsdItemDefinition Definition
    {
        get;
    }

    /// <summary>Gets the firmware item key.</summary>
    public string Key => Definition.Key;

    /// <summary>Gets the metadata-derived item title.</summary>
    public string Title => Definition.Title;

    /// <summary>Gets the metadata-derived item description.</summary>
    public string Description => Definition.Description;

    /// <summary>Gets discovered item-specific option/unit/warning parameters.</summary>
    public ObservableCollection<ParameterItemViewModel> AdditionalParameters
    {
        get;
    }

    /// <summary>Gets or sets whether the item is enabled.</summary>
    [ObservableProperty]
    public partial bool IsEnabled
    {
        get;
        set;
    }

    /// <summary>Gets or sets the zero-based character column.</summary>
    [ObservableProperty]
    public partial int Column
    {
        get;
        set;
    }

    /// <summary>Gets or sets the zero-based character row.</summary>
    [ObservableProperty]
    public partial int Row
    {
        get;
        set;
    }

    /// <summary>Gets the latest coordinate or metadata error.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    public partial string? ValidationError
    {
        get;
        private set;
    }

    /// <summary>Gets whether placement is currently invalid.</summary>
    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationError);

    /// <summary>Refreshes values from the shared session.</summary>
    public void Refresh()
    {
        loading = true;
        IsEnabled = Definition.EnableParameterName is null ||
                    session.GetField(Definition.EnableParameterName)?.PendingValue > 0.5;
        Column = (int)Math.Round(session.GetField(Definition.ColumnParameterName)?.PendingValue ?? 0);
        Row = (int)Math.Round(session.GetField(Definition.RowParameterName)?.PendingValue ?? 0);
        foreach (var parameter in AdditionalParameters)
        {
            if (session.GetField(parameter.Name) is { } state)
            {
                parameter.SetField(state);
            }
        }

        loading = false;
    }

    internal void SetValidationError(string? error)
    {
        ValidationError = error;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (!loading && Definition.EnableParameterName is not null)
        {
            session.TrySetPending(Definition.EnableParameterName, value ? 1 : 0, out var error);
            ValidationError = error;
        }
    }

    partial void OnColumnChanged(int oldValue, int newValue)
    {
        if (!loading)
        {
            ValidationError = move(this, newValue, Row);
            if (ValidationError is not null)
            {
                loading = true;
                Column = oldValue;
                loading = false;
            }
        }
    }

    partial void OnRowChanged(int oldValue, int newValue)
    {
        if (!loading)
        {
            ValidationError = move(this, Column, newValue);
            if (ValidationError is not null)
            {
                loading = true;
                Row = oldValue;
                loading = false;
            }
        }
    }
}

