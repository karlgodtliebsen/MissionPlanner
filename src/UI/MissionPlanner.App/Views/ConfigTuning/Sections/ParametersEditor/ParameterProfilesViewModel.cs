using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

/// <summary>Manages reusable local profiles and stages reviewed values into the captured edit session.</summary>
public partial class ParameterProfilesViewModel(
    IParameterProfileRepository repository,
    IParameterProfileService workflow,
    IUserConfirmationService confirmation,
    IFileOpenService files,
    IFileSaveService exports,
    IParameterEditSession session,
    IUiDispatcher dispatcher,
    IDomainEventHub events,
    ILogger<ParameterProfilesViewModel> logger) : DialogViewModelBase(logger, dispatcher, events)
{
    /// <summary>Stored profiles, ordered by name.</summary>
    public ObservableCollection<ParameterProfile> Profiles { get; } = [];

    /// <summary>Comparison rows belonging to the selected profile.</summary>
    public ObservableCollection<ParameterComparisonItemViewModel> Rows { get; } = [];

    /// <summary>Available creation scopes.</summary>
    public IReadOnlyList<string> Scopes { get; } = ["All parameters", "Modified parameters", "Selected names"];

    /// <summary>Selected profile; selection never stages or writes values.</summary>
    [ObservableProperty]
    public partial ParameterProfile? SelectedProfile
    {
        get; set;
    }

    /// <summary>Name for creation or updating the selected profile's details.</summary>
    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    /// <summary>Reusable profile description.</summary>
    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    /// <summary>Comma-separated tags.</summary>
    [ObservableProperty]
    public partial string Tags { get; set; } = string.Empty;

    /// <summary>Scope of values to capture, including pending edits.</summary>
    [ObservableProperty]
    public partial string Scope { get; set; } = "Modified parameters";

    /// <summary>Comma, space or newline-separated parameter names for subset creation.</summary>
    [ObservableProperty]
    public partial string SelectedNames { get; set; } = string.Empty;

    /// <summary>Compatibility and source information.</summary>
    [ObservableProperty]
    public partial string ReviewText { get; private set; } = "Select a saved profile to compare it with the current vehicle.";

    /// <summary>Operation feedback, including rejected values and storage failures.</summary>
    [ObservableProperty]
    public partial string Feedback { get; private set; } = "Profiles are local snapshots. Staging does not write to the flight controller.";

    /// <summary>Prevents overlapping operations and edits while awaiting confirmation.</summary>
    [ObservableProperty]
    public partial bool Working
    {
        get; private set;
    }

    partial void OnSelectedProfileChanged(ParameterProfile? value)
    {
        ProfileName = value?.Name ?? string.Empty;
        Description = value?.Description ?? string.Empty;
        Tags = string.Join(", ", value?.Tags ?? []);
        Review();
    }

    /// <summary>Loads profiles without modifying parameters.</summary>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return RunAsync(async () =>
    {
        await ReloadAsync(null, cancellationToken);
        Feedback = Profiles.Count == 0 ? "No profiles yet. Enter a name and choose a scope to create one." : "Select a profile to review its values.";
    });
    }

    private async Task ReloadAsync(Guid? selectedId, CancellationToken token)
    {
        var saved = await repository.GetAllAsync(token);
        Profiles.Clear();
        foreach (var profile in saved)
        {
            Profiles.Add(profile);
        }
        SelectedProfile = Profiles.FirstOrDefault(profile => profile.Id == selectedId);
    }

    private bool ValidSession()
    {
        if (session.IsValid)
        {
            return true;
        }
        Feedback = string.IsNullOrWhiteSpace(session.InvalidReason)
            ? "The vehicle session changed. Close profiles and reopen them from the current Parameters Editor."
            : session.InvalidReason;
        return false;
    }

    [RelayCommand]
    private void Review()
    {
        Rows.Clear();
        if (SelectedProfile is not { } profile)
        {
            ReviewText = "Select a saved profile to compare it with the current vehicle.";
            return;
        }
        if (!ValidSession())
        {
            ReviewText = Feedback;
            return;
        }
        var review = workflow.Review(profile, session);
        ReviewText = $"{profile.Name} — {profile.Values.Count} values\nSource: {profile.SourceIdentity}; firmware: {profile.FirmwareFamily}; vehicle type: {profile.MavType}\n" +
            (review.Warnings.Count == 0 ? "No recorded identity mismatch. This does not guarantee hardware compatibility." : string.Join("\n", review.Warnings));
        foreach (var row in review.Comparison.Rows.Where(row => profile.Values.ContainsKey(row.Name)))
        {
            Rows.Add(new(row));
        }
    }

    [RelayCommand]
    private Task CreateAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        if (!ValidSession())
        {
            return;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(ProfileName);
        var names = Scope == "Selected names" ? SelectedNames.Split([',', ';', ' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray() : null;
        if (names is { Length: 0 } || names?.Any(name => session.GetField(name) is null) == true)
        {
            Feedback = "Enter at least one loaded parameter name. Unknown names: " + string.Join(", ", names?.Where(name => session.GetField(name) is null) ?? []);
            return;
        }
        var profile = workflow.Create(session, ProfileName, Description, Scope == "Modified parameters", names, ParseTags());
        if (profile.Values.Count == 0)
        {
            Feedback = "The chosen scope has no parameters. Nothing was saved.";
            return;
        }
        await repository.SaveAsync(profile, token);
        await ReloadAsync(profile.Id, token);
        Feedback = $"Created '{profile.Name}' with {profile.Values.Count} values. No vehicle values were written.";
    });
    }

    [RelayCommand]
    private Task SaveDetailsAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        if (SelectedProfile is not { } profile)
        {
            Feedback = "Select a profile first.";
            return;
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(ProfileName);
        await repository.SaveAsync(profile with
        {
            Name = ProfileName.Trim(),
            Description = Description,
            Tags = ParseTags(),
            UpdatedAt = DateTimeOffset.UtcNow
        }, token);
        await ReloadAsync(profile.Id, token);
        Feedback = "Saved profile details; stored parameter values are unchanged.";
    });
    }

    [RelayCommand]
    private Task DuplicateAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }
        var copy = await repository.DuplicateAsync(profile.Id, profile.Name + " (copy)", token);
        await ReloadAsync(copy.Id, token);
        Feedback = "Duplicated the stored values. Edit the name and use Save details to rename the copy.";
    });
    }

    [RelayCommand]
    private Task ImportAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        using var file = await files.OpenAsync("Import parameter profile", ["*.json"], token);
        if (file is null)
        {
            return;
        }
        var profile = await System.Text.Json.JsonSerializer.DeserializeAsync<ParameterProfile>(file.Content,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web), token)
            ?? throw new InvalidDataException("The profile is empty.");
        // Imported documents get a new identity: never overwrite an existing local profile implicitly.
        profile = profile with
        {
            Id = Guid.NewGuid()
        };
        await repository.SaveAsync(profile, token);
        await ReloadAsync(profile.Id, token);
        Feedback = $"Imported '{profile.Name}' as a new local profile. Review before staging.";
    });
    }

    [RelayCommand]
    private Task ExportAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }
        using var content = new MemoryStream();
        await repository.ExportAsync(profile.Id, content, token);
        content.Position = 0;
        var path = await exports.SaveAsync($"parameter-profile-{profile.Id:N}.json", content, token);
        Feedback = path is null ? "Export cancelled." : "Profile exported.";
    });
    }

    [RelayCommand]
    private Task DeleteAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        if (SelectedProfile is not { } profile)
        {
            return;
        }
        if (!await confirmation.ConfirmAsync("Delete parameter profile", $"Delete '{profile.Name}' from local storage?", "Delete profile", token))
        {
            return;
        }
        await repository.DeleteAsync(profile.Id, token);
        await ReloadAsync(null, token);
        Feedback = $"Deleted '{profile.Name}'.";
    });
    }

    [RelayCommand]
    private void SelectSafe()
    {
        foreach (var row in Rows)
        {
            row.IsSelected = row.CanStage;
        }
    }

    [RelayCommand]
    private Task StageAsync(CancellationToken token)
    {
        return RunAsync(async () =>
    {
        if (!ValidSession() || SelectedProfile is not { } profile)
        {
            return;
        }
        var names = Rows.Where(row => row.IsSelected && row.CanStage).Select(row => row.Name).ToArray();
        if (names.Length == 0)
        {
            Feedback = "Select compatible differences to stage.";
            return;
        }
        var review = workflow.Review(profile, session);
        var message = $"Stage {names.Length} selected values as pending edits? Existing pending edits for these names will be replaced. Apply remains a separate action.\n" + string.Join("\n", review.Warnings);
        if (!await confirmation.ConfirmAsync("Stage profile differences", message, "Stage selected", token) || !ValidSession())
        {
            return;
        }
        var fresh = workflow.Review(profile, session);
        if (!review.Comparison.Rows.SequenceEqual(fresh.Comparison.Rows))
        {
            Review();
            Feedback = "Vehicle values changed during confirmation. Review and select the differences again.";
            return;
        }
        var staged = workflow.Stage(fresh, session, names);
        Feedback = $"Staged {staged.Count} of {names.Length} values as pending edits. No values were written.";
        var skipped = names.Except(staged).ToArray();
        if (skipped.Length > 0)
        {
            Feedback += " Rejected or no longer compatible: " + string.Join(", ", skipped);
        }
    });
    }

    private string[] ParseTags()
    {
        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (Working)
        {
            return;
        }
        Working = true;
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            Feedback = "Profile operation cancelled.";
        }
        catch (Exception exception)
        {
            Feedback = $"Profile operation failed: {exception.Message}";
        }
        finally
        {
            Working = false;
        }
    }
}
