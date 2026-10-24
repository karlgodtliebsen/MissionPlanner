using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.Setup.Advanced.Scripting;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Scripting;

/// <summary>Owns console command history and the lifetime of an explicitly opened onboard REPL.</summary>
public sealed partial class ScriptingConsoleViewModel(ScriptingConsoleService console, IUserConfirmationService confirmation,
    ILogger<ScriptingConsoleViewModel> logger) : ViewModelBase(logger)
{
    private CancellationTokenSource lifetime = new();
    private readonly object outputLock = new();
    private readonly StringBuilder pending = new();
    private bool dispatchPending;
    private int escapeState;

    /// <summary>Gets supported console protocols.</summary>
    public IReadOnlyList<ScriptingConsoleMode> Modes { get; } = Enum.GetValues<ScriptingConsoleMode>();
    /// <summary>Gets or sets the reviewed protocol; the installed Lua applet is the default.</summary>
    [ObservableProperty] public partial ScriptingConsoleMode Mode { get; set; }
    /// <summary>Gets or sets a single command line.</summary>
    [ObservableProperty] public partial string Input { get; set; } = string.Empty;
    /// <summary>Gets the bounded plain-text output, held only for this page session.</summary>
    [ObservableProperty] public partial string Output { get; private set; } = string.Empty;
    /// <summary>Gets recent commands; none are saved or replayed automatically.</summary>
    public ObservableCollection<string> History { get; } = [];
    /// <summary>Gets or sets a history entry to copy into the command editor.</summary>
    [ObservableProperty] public partial string? SelectedHistory { get; set; }
    partial void OnSelectedHistoryChanged(string? value) { if (value is not null) Input = value; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            var selected = Mode;
            if (!await confirmation.ConfirmAsync("Open onboard scripting console", "Lua commands can change parameters, files and outputs. Confirm the disarmed vehicle is secured and the chosen REPL is installed. No command is run when opening. Closing the Lua applet channel does not stop the applet or undo executed commands.", "Open console", linked.Token)) return;
            IsBusy = true;
            SetMessages("Opening console. Use Stop to close; commands are sent once and are not retried automatically.", null);
            await console.RunAsync(selected, true, new OutputSink(AppendOutput), linked.Token);
        }
        catch (OperationCanceledException) { SetMessages("Console closed or connection cancelled. Executed scripts and changes are not undone.", null); }
        catch (Exception ex) { SetMessages(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        var line = Input;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
        try
        {
            await console.SendAsync(line, linked.Token);
            History.Add(line);
            while (History.Count > 50) History.RemoveAt(0);
            Input = string.Empty;
            SetMessages("Line sent once. Firmware output determines the result; do not resend after a timeout without checking its effects.", null);
        }
        catch (OperationCanceledException) { SetMessages("Send cancelled or timed out. Execution may already have occurred; do not retry blindly.", null); }
        catch (Exception ex) { SetMessages(ex.Message + " A partially sent line may remain on the vehicle; reopen/check the console before retrying.", null); }
    }

    [RelayCommand]
    private void Clear() { Output = string.Empty; History.Clear(); }

    private void AppendOutput(string text)
    {
        lock (outputLock)
        {
            // Strip terminal escape sequences from the plain-text viewer, including sequences split across packets.
            foreach (var c in text)
            {
                if (escapeState == 1) { escapeState = c == '[' ? 2 : 0; continue; }
                if (escapeState == 2) { if (c is >= '@' and <= '~') escapeState = 0; continue; }
                if (c == '\u001b') { escapeState = 1; continue; }
                if (!char.IsControl(c) || c is '\n' or '\t') pending.Append(c);
            }
            if (pending.Length > 65536) pending.Remove(0, pending.Length - 65536);
            if (dispatchPending) return;
            dispatchPending = true;
        }
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            string chunk;
            lock (outputLock) { chunk = pending.ToString(); pending.Clear(); dispatchPending = false; }
            if (lifetime.IsCancellationRequested) return;
            var combined = Output + chunk;
            Output = combined.Length > 65536 ? combined[^65536..] : combined;
        });
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (lifetime.IsCancellationRequested) { lifetime.Dispose(); lifetime = new(); }
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override async Task DeactivateAsync()
    {
        lifetime.Cancel();
        if (OpenCommand.ExecutionTask is { } task) await task;
        Input = string.Empty;
        History.Clear();
        await base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose() { lifetime.Cancel(); base.Dispose(); }

    private sealed class OutputSink(Action<string> write) : IProgress<string>
    {
        public void Report(string value) => write(value);
    }
}
