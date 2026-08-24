using System.Diagnostics;
using MissionPlanner.App.Helpers;
using UraniumUI.Material.TabViews;
using UraniumUI.Pages;

namespace MissionPlanner.App.Navigation;

/// <summary>
/// Represents a content page view that is associated with a specific view model type.
/// Handles viewmodel allocation and cleanup for navigation events.
/// Used to Enforce Cleanup when Navigating away from a view. The view model is automatically disposed when the view is disposed.
/// </summary>
public class ExtendedContentPage<TViewModel> : UraniumContentPage, IDisposable
    where TViewModel : class, IDisposable, IActivationLifeCycle

{
    private bool isActive;

    /// <summary>
    ///  
    /// </summary>
    protected TViewModel? ViewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedContentPage{TViewModel}"/> class.
    /// </summary>
    protected ExtendedContentPage(string? key = null)
    {
        ViewModel = key is not null
            ? ServiceHelper.GetRequiredKeyedService<TViewModel>(key)
            : ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
    }

    /// <inheritdoc />
    protected ExtendedContentPage()
    {
        ViewModel = ServiceHelper.GetRequiredService<TViewModel>();
        BindingContext = ViewModel;
    }

    //private void SetBindingContext()
    //{
    //    Dispatcher.Dispatch(() =>
    //    {
    //        if (ViewModel is null)
    //        {
    //            Debug.Print("ViewModel is null");
    //            return;
    //        }

    //        var t1 = Task.Yield().GetAwaiter();
    //        t1.OnCompleted(() => BindingContext = ViewModel);
    //    });
    //}

    /// <inheritdoc />
    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), () =>
            {
                if (ViewModel is null)
                {
                    Debug.Print("ViewModel is null");
                    return;
                }

                var t1 = Task.Yield().GetAwaiter();
                t1.OnCompleted(async () => await DeactivateAsync());
            });
        }
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (args.NavigationType is NavigationType.Replace or NavigationType.Remove)
        {
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), () =>
            {
                if (ViewModel is null)
                {
                    Debug.Print("ViewModel is null");
                    return;
                }

                var t1 = Task.Yield().GetAwaiter();
                t1.OnCompleted(async () => await DeactivateAsync());
            });
        }
    }

    /// <inheritdoc/>
    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        if (args.NavigationType is not (NavigationType.Replace or NavigationType.Remove))
        {
            return;
        }

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1), () =>
        {
            if (ViewModel is null)
            {
                Debug.Print("ViewModel is null");
                return;
            }
            var t1 = Task.Yield().GetAwaiter();
            t1.OnCompleted(async () => await ActivateAsync());
        });
    }

    /// <inheritdoc/>
    public virtual void Dispose()
    {
        ViewModel?.Dispose();
        ViewModel = null;
    }

    /// <summary>
    /// Called when the view model needs activating.
    /// </summary>
    protected virtual async Task ActivateAsync()
    {
        if (isActive)
        {
            return;
        }

        if (ViewModel is null)
        {
            return;
        }

        try
        {
            await ViewModel.ActivateAsync();
        }
        catch (OperationCanceledException ex)
        {
            Debug.Print(ex.Message);
        }
        catch (Exception ex)
        {
            Debug.Print(ex.Message);
        }

        isActive = true;
    }

    /// <summary>
    /// Called when the view model needs deactivating.
    /// </summary>
    protected virtual async Task DeactivateAsync()
    {
        if (!isActive)
        {
            return;
        }

        if (ViewModel is null)
        {
            return;
        }

        try
        {
            await ViewModel.DeactivateAsync();
        }
        catch (OperationCanceledException ex)
        {
            Debug.Print(ex.Message);
        }
        catch (Exception ex)
        {
            Debug.Print(ex.Message);
        }

        isActive = false;
    }
}
