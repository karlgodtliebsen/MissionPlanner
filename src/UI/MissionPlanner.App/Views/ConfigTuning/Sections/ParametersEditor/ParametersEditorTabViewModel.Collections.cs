using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MissionPlanner.App.Models;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

public partial class ParametersEditorTabViewModel
{
    private readonly IPlannerSettingsService? plannerSettings;
    /// <summary>Gets filtered references to the shared live editors; filtering never discards pending values.</summary>
    public ObservableCollection<ParameterItemViewModel> CollectionParameters { get; } = [];
    /// <summary>Gets named live subsets, including the standard RC auxiliary-function subset.</summary>
    [ObservableProperty] public partial IReadOnlyList<string> CollectionNames { get; private set; } = ["All", "Favorites", "RC auxiliary"];
    /// <summary>Gets or sets the selected name-only collection.</summary>
    [ObservableProperty] public partial string SelectedCollection { get; set; } = "All";
    /// <summary>Gets firmware parameter family prefixes discovered from the current vehicle.</summary>
    [ObservableProperty] public partial IReadOnlyList<string> ParameterGroups { get; private set; } = ["All"];
    /// <summary>Gets or sets the prefix used for navigation.</summary>
    [ObservableProperty] public partial string SelectedParameterGroup { get; set; } = "All";
    /// <summary>Gets supported metadata visibility filters.</summary>
    public IReadOnlyList<string> ParameterLevels { get; } = ["All", "Standard", "Advanced", "Unknown"];
    /// <summary>Gets or sets the metadata user-level filter.</summary>
    [ObservableProperty] public partial string ParameterLevel { get; set; } = "All";
    /// <summary>Gets or sets a name for saving the entered live subset.</summary>
    [ObservableProperty] public partial string CollectionName { get; set; } = "Favorites";
    /// <summary>Gets or sets comma, space or newline separated parameter names.</summary>
    [ObservableProperty] public partial string CollectionMembers { get; set; } = string.Empty;

    partial void OnSelectedCollectionChanged(string value)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (value is null)
        {
            return;
        }
        if (plannerSettings?.Current.ParameterCollections.TryGetValue(value, out var members) == true)
        {
            CollectionName = value;
            CollectionMembers = string.Join(Environment.NewLine, members);
        }
        RefreshParameterCollections();
    }
    partial void OnSelectedParameterGroupChanged(string value) => RefreshParameterCollections();
    partial void OnParameterLevelChanged(string value) => RefreshParameterCollections();

    private void RefreshParameterCollections()
    {
        var saved = plannerSettings?.Current.ParameterCollections ?? new Dictionary<string, string[]>();
        CollectionNames = new[] { "All", "Favorites", "RC auxiliary" }.Concat(saved.Keys).Distinct().Order().ToArray();
        var groups = new[] { "All" }.Concat(Parameters.Select(parameter => parameter.Name.Split('_')[0]).Distinct().Order()).ToArray();
        if (!ParameterGroups.SequenceEqual(groups))
        {
            ParameterGroups = groups;
        }
        IEnumerable<ParameterItemViewModel> rows = Parameters;
        if (SelectedCollection == "RC auxiliary")
        {
            rows = rows.Where(parameter => System.Text.RegularExpressions.Regex.IsMatch(parameter.Name, "^RC[0-9]+_OPTION$"));
        }
        else if (SelectedCollection != "All")
        {
            var names = saved.GetValueOrDefault(SelectedCollection, []).ToHashSet(StringComparer.Ordinal);
            rows = rows.Where(parameter => names.Contains(parameter.Name));
        }
        if (SelectedParameterGroup != "All")
        {
            rows = rows.Where(parameter => parameter.Name == SelectedParameterGroup || parameter.Name.StartsWith(SelectedParameterGroup + "_", StringComparison.Ordinal));
        }
        if (ParameterLevel != "All")
        {
            rows = rows.Where(parameter => ParameterLevel == "Unknown" ? string.IsNullOrWhiteSpace(parameter.UserLevel)
                : string.Equals(parameter.UserLevel, ParameterLevel, StringComparison.OrdinalIgnoreCase));
        }
        var result = rows.ToArray();
        if (!CollectionParameters.SequenceEqual(result))
        {
            CollectionParameters.Clear();
            foreach (var item in result)
            {
                CollectionParameters.Add(item);
            }
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SaveCollectionAsync(CancellationToken token)
    {
        if (plannerSettings is null)
        {
            SetMessages(errorMessage: "Collection persistence is unavailable on this host.");
            return;
        }
        var name = CollectionName.Trim();
        if (name is "All" or "RC auxiliary" || string.IsNullOrWhiteSpace(name))
        {
            SetMessages(errorMessage: "Choose a collection name, such as Favorites or Bench setup.");
            return;
        }
        var names = CollectionMembers.Split([' ', ',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.ToUpperInvariant()).Distinct().Order().ToArray();
        var collections = plannerSettings.Current.ParameterCollections.ToDictionary(pair => pair.Key, pair => pair.Value);
        collections[name] = names;
        try
        {
            var result = await plannerSettings.SaveAsync(plannerSettings.Current with
            {
                ParameterCollections = collections
            }, token);
            if (!result.Success)
            {
                SetMessages(errorMessage: string.Join(Environment.NewLine, result.Errors.Select(error => error.Message)));
                return;
            }
            SelectedCollection = name;
            RefreshParameterCollections();
            SetMessages($"Saved {names.Length} parameter names. Unreported parameters stay in the collection and appear when supported; no values were written.");
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { SetMessages(exception); }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task DeleteCollectionAsync(CancellationToken token)
    {
        if (plannerSettings is null || !plannerSettings.Current.ParameterCollections.ContainsKey(SelectedCollection))
        {
            return;
        }
        var collections = plannerSettings.Current.ParameterCollections.ToDictionary(pair => pair.Key, pair => pair.Value);
        collections.Remove(SelectedCollection);
        try
        {
            var result = await plannerSettings.SaveAsync(plannerSettings.Current with
            {
                ParameterCollections = collections
            }, token);
            if (result.Success)
            {
                SelectedCollection = "All";
                RefreshParameterCollections();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { SetMessages(exception); }
    }
}
