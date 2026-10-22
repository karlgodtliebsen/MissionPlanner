using CommunityToolkit.Mvvm.Input;
using Irihi.Avalonia.Shared.Contracts;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Utilities;

/// <summary>
/// Base class for dialog view models, providing common functionality for closing and confirming dialogs.
/// </summary>
public partial class DialogViewModelBase : ViewModelBase, IDialogContext
{
    /// <summary>Gets or sets the dialog title.</summary>
    public string? Title
    {
        get;
        set;
    }

    /// <summary>Gets or sets the text of the OK button.</summary>
    public string? OkText
    {
        get;
        set;
    }

    /// <summary>Gets or sets the text of the Cancel button.</summary>
    public string? CancelText
    {
        get;
        set;
    }


    /// <inheritdoc/>
    public bool Confirmation
    {
        get; set;
    }
    /// <summary>Gets or sets whether the dialog was closed.</summary>
    public bool Closed
    {
        get; set;
    }

    /// <summary>Creates the dialog view model with explicit services.</summary>
    protected DialogViewModelBase(ILogger logger, IUiDispatcher dispatcher, IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
    }

    /// <inheritdoc />
    protected DialogViewModelBase(ILogger logger) : base(logger)
    {

    }

    /// <inheritdoc />
    protected DialogViewModelBase() : base()
    {

    }


    /// <summary>
    /// Closes the dialog.
    /// </summary>
    public virtual void Close()
    {
        Closed = true;
        RequestClose?.Invoke(this, null);
    }

    /// <summary>
    /// Confirms the dialog.
    /// </summary>
    /// <param name="cancellationToken">Cancels a derived implementation's confirmation work.</param>
    [RelayCommand]
    public virtual Task OKAsync(CancellationToken cancellationToken = default)
    {
        Confirmation = true;
        RequestClose?.Invoke(this, true);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Cancels the dialog.
    /// </summary>
    [RelayCommand]
    public virtual void Cancel()
    {
        Confirmation = false;
        RequestClose?.Invoke(this, false);
    }


    /// <summary>
    /// Occurs when a request to close the dialog is made.
    /// The event argument indicates whether the dialog was confirmed (true) or canceled (false).
    /// </summary>
    public event EventHandler<object?>? RequestClose;

}
