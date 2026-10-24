using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Setup.OptionalHardware;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Reviews and configures a supported local GNSS base separately from RTCM forwarding.</summary>
public sealed partial class RtkBaseSetupViewModel(UbxBaseSession receiver, IPlannerSettingsService preferences,
    IUserConfirmationService confirmation, ILogger<RtkBaseSetupViewModel> logger) : OptionalHardwareBaseViewModel(logger)
{
    private CancellationTokenSource lifetime = new();
    /// <summary>Gets or sets the directly connected receiver port.</summary>
    [ObservableProperty] public partial string Port { get; set; } = string.Empty;
    /// <summary>Gets or sets the receiver's existing baud rate.</summary>
    [ObservableProperty] public partial int Baud { get; set; } = 115200;
    /// <summary>Gets current identity and support evidence.</summary>
    [ObservableProperty] public partial string ReceiverIdentity { get; private set; } = "Receiver disconnected.";
    /// <summary>Gets the latest mode and survey evidence.</summary>
    [ObservableProperty] public partial string SurveyStatus { get; private set; } = "Survey status unavailable.";
    /// <summary>Gets the last decoded fix diagnostics.</summary>
    [ObservableProperty] public partial string FixStatus { get; private set; } = "No fix received.";
    /// <summary>Gets checksum and correction traffic evidence.</summary>
    [ObservableProperty] public partial string StreamStatus { get; private set; } = "No receiver bytes observed.";
    /// <summary>Gets satellite/constellation diagnostics.</summary>
    [ObservableProperty] public partial string SatelliteStatus { get; private set; } = "No satellite report received.";
    /// <summary>Gets or sets the minimum survey duration in seconds.</summary>
    [ObservableProperty] public partial uint SurveySeconds { get; set; } = 60;
    /// <summary>Gets or sets the survey accuracy threshold in metres.</summary>
    [ObservableProperty] public partial double SurveyAccuracy { get; set; } = 2;
    /// <summary>Gets or sets the saved position name.</summary>
    [ObservableProperty] public partial string PositionName { get; set; } = "Base position";
    /// <summary>Gets or sets WGS84 latitude.</summary>
    [ObservableProperty] public partial double Latitude { get; set; }
    /// <summary>Gets or sets WGS84 longitude.</summary>
    [ObservableProperty] public partial double Longitude { get; set; }
    /// <summary>Gets or sets WGS84 ellipsoid height; this is not MSL altitude.</summary>
    [ObservableProperty] public partial double EllipsoidHeight { get; set; }
    /// <summary>Gets or sets the declared fixed-position accuracy.</summary>
    [ObservableProperty] public partial double PositionAccuracy { get; set; } = .02;
    /// <summary>Gets or sets whether RTCM configuration should also request Galileo MSM4.</summary>
    [ObservableProperty] public partial bool Galileo { get; set; }
    /// <summary>Gets or sets whether RTCM configuration should also request BeiDou MSM4.</summary>
    [ObservableProperty] public partial bool BeiDou { get; set; }
    /// <summary>Gets stored positions; selecting one only populates local editors.</summary>
    public ObservableCollection<RtkBasePosition> SavedPositions { get; } = new(preferences.Current.RtkBasePositions);
    /// <summary>Gets or sets the local preset selected for review.</summary>
    [ObservableProperty] public partial RtkBasePosition? SelectedPosition { get; set; }
    partial void OnSelectedPositionChanged(RtkBasePosition? value) { if (value is not null) ShowPosition(value); }
    private RtkBasePosition Position() => new(PositionName, Latitude, Longitude, EllipsoidHeight, PositionAccuracy);
    private void ShowPosition(RtkBasePosition position) { PositionName = position.Name; Latitude = position.Latitude; Longitude = position.Longitude; EllipsoidHeight = position.EllipsoidHeight; PositionAccuracy = position.Accuracy; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task MonitorAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try { await receiver.RunAsync(Port, Baud, new Progress<UbxBaseSnapshot>(Show), linked.Token); }
        catch (OperationCanceledException) { SetMessages("Monitoring stopped or timed out. The receiver keeps its last time-mode configuration; use Disable time mode explicitly when required."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { ReceiverIdentity = "Receiver disconnected. Reconnect before configuring."; SurveyStatus = "No current survey evidence; reconnect to read the receiver."; }
    }
    private void Show(UbxBaseSnapshot snapshot)
    {
        ReceiverIdentity = snapshot.Identity;
        SetMessages(snapshot.Status);
        var now = DateTimeOffset.UtcNow;
        SurveyStatus = snapshot.Mode + (snapshot.Survey is { } survey ?
            $" · survey active {survey.Active}, valid {survey.Valid}, {survey.Duration} s, {survey.Observations} observations, mean accuracy {survey.Accuracy:0.0000} m" +
                (now - survey.ObservedAt > TimeSpan.FromSeconds(3) ? " (STALE)" : "") : " · waiting for survey diagnostics");
        FixStatus = snapshot.Fix;
        StreamStatus = $"Checksum-valid RTCM frames: {snapshot.RtcmFrames}; last {snapshot.LastCorrection?.ToLocalTime():T}; UBX checksum errors: {snapshot.ChecksumErrors}. Monitoring does not forward to vehicles.";
        SatelliteStatus = string.Join(" · ", snapshot.Satellites.GroupBy(satellite => satellite.Constellation).Select(group => $"{group.Key}: {group.Count(satellite => satellite.Used)}/{group.Count()} used/tracked")) +
            (snapshot.SatellitesAt is not { } observed || now - observed > TimeSpan.FromSeconds(5) ? " (no current satellite report)" : "") +
            "\n" + string.Join("; ", snapshot.Satellites.Select(satellite => $"{satellite.Constellation} {satellite.Number}: {satellite.CarrierNoise} dB-Hz, elevation {satellite.Elevation}°{(satellite.Used ? ", used" : "")}"));
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task SurveyAsync(CancellationToken token)
    {
        var seconds = SurveySeconds; var accuracy = SurveyAccuracy;
        return ChangeAsync("Start new base survey", $"Restart survey-in with a minimum {seconds} seconds and {accuracy:0.000} m accuracy threshold? A previous survey is disabled first. Keep the antenna stationary. These thresholds do not prove geodetic accuracy.",
            (review, ct) => receiver.StartSurveyAsync(review, seconds, accuracy, true, ct), token);
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task FixedAsync(CancellationToken token)
    {
        var position = Position();
        return ChangeAsync("Use fixed base position", $"Set antenna reference point to {position.Latitude:F9}, {position.Longitude:F9}, WGS84 ellipsoid height {position.EllipsoidHeight:0.0000} m and declared accuracy {position.Accuracy:0.0000} m? Confirm the surveyed coordinates and height datum. An incorrect base shifts rover positions.",
            (review, ct) => receiver.SetFixedAsync(review, position, true, ct), token);
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private Task DisableAsync(CancellationToken token) => ChangeAsync("Disable base time mode", "Disable survey/fixed base mode? This can interrupt correction generation for connected rovers.", (review, ct) => receiver.DisableAsync(review, true, ct), token);
    [RelayCommand(IncludeCancelCommand = true)]
    private Task CorrectionsAsync(CancellationToken token)
    {
        var galileo = Galileo; var beidou = BeiDou;
        return ChangeAsync("Enable RTCM output", $"Enable RTCM 1005, GPS 1074, GLONASS 1084 and 1230 on this receiver port, plus Galileo={galileo}, BeiDou={beidou}? Other message rates remain unchanged. This changes RAM configuration only and leaves baud unchanged. Unsupported messages stop the operation and earlier changes may remain.",
            (review, ct) => receiver.EnableCorrectionsAsync(review, galileo, beidou, true, ct), token);
    }
    private async Task ChangeAsync(string title, string detail, Func<UbxBaseReview, CancellationToken, Task> change, CancellationToken token)
    {
        if (IsBusy) return;
        SetBusy();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            var review = receiver.Review();
            if (!await confirmation.ConfirmAsync(title, review.Identity + "\n" + detail, "Apply to receiver", linked.Token)) return;
            await change(review, linked.Token);
            if (receiver.Current is { } snapshot) Show(snapshot);
        }
        catch (OperationCanceledException) { SetMessages("Receiver change cancelled or timed out; earlier settings may remain. Read the receiver before retrying."); }
        catch (Exception ex) { SetMessages(ex); }
        finally { ResetBusy(); }
    }
    [RelayCommand]
    private void UseSurvey()
    {
        try
        {
            if (receiver.Current?.Survey is not { Valid: true, Active: false, MeanPosition: { } position } survey || DateTimeOffset.UtcNow - survey.ObservedAt > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException("Wait for a fresh completed survey with a valid mean position.");
            ShowPosition(position); SetMessages("Survey result copied into local fixed-position editors. Review before saving or applying; no receiver write was made.");
        }
        catch (Exception ex) { SetMessages(ex); }
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SavePositionAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            var position = Position(); position.Validate();
            var all = preferences.Current.RtkBasePositions.ToList();
            var existing = all.FindIndex(saved => string.Equals(saved.Name, position.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                if (!await confirmation.ConfirmAsync("Replace saved base position", $"Replace the local position named {position.Name}? This does not change the receiver.", "Replace", linked.Token)) return;
                all[existing] = position;
            }
            else all.Add(position);
            var result = await preferences.SaveAsync(preferences.Current with { RtkBasePositions = all }, linked.Token);
            if (!result.Success) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Message)));
            SavedPositions.Clear(); foreach (var saved in all) SavedPositions.Add(saved);
            SetMessages("Local base position saved; receiver unchanged.");
        }
        catch (OperationCanceledException) { SetMessages("Saving cancelled."); }
        catch (Exception ex) { SetMessages(ex); }
    }
    [RelayCommand(IncludeCancelCommand = true)]
    private async Task DeletePositionAsync(CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            if (SelectedPosition is not { } selected || !await confirmation.ConfirmAsync("Remove saved position", $"Remove {selected.Name} from local saved positions?", "Remove", linked.Token)) return;
            var all = preferences.Current.RtkBasePositions.Where(position => position.Name != selected.Name).ToArray();
            var result = await preferences.SaveAsync(preferences.Current with { RtkBasePositions = all }, linked.Token);
            if (!result.Success) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Message)));
            SavedPositions.Remove(selected); SelectedPosition = null;
        }
        catch (OperationCanceledException) { SetMessages("Removal cancelled."); }
        catch (Exception ex) { SetMessages(ex); }
    }
    /// <inheritdoc />
    public override Task ActivateAsync() { if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); } return base.ActivateAsync(); }
    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        await Task.WhenAll(new[] { MonitorCommand.ExecutionTask, SurveyCommand.ExecutionTask, FixedCommand.ExecutionTask, DisableCommand.ExecutionTask,
            CorrectionsCommand.ExecutionTask, SavePositionCommand.ExecutionTask, DeletePositionCommand.ExecutionTask }.OfType<Task>());
        await base.DeactivateAsync();
    }
    /// <inheritdoc />
    public override void Dispose() { lifetime.Cancel(); base.Dispose(); }
}
