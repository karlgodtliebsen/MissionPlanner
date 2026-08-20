using Microsoft.Extensions.Logging;

namespace MissionPlanner.App.Theming;

/// <summary>Owns the single active MissionPlanner semantic color palette.</summary>
public sealed class ThemeManager : IThemeManager
{
    private readonly IThemeCatalog catalog;
    private readonly IThemePaletteLoader paletteLoader;
    private readonly IDispatcher dispatcher;
    private readonly IThemeEnvironment environment;
    private readonly ILogger<ThemeManager> logger;
    private readonly SemaphoreSlim applyLock = new(1, 1);
    private ResourceDictionary? activeResources;
    private bool assigningNativeAppearance;
    private bool disposed;

    /// <summary>Initializes the theme manager.</summary>
    public ThemeManager(
        IThemeCatalog catalog,
        IThemePaletteLoader paletteLoader,
        IDispatcher dispatcher,
        IThemeEnvironment environment,
        ILogger<ThemeManager> logger)
    {
        this.catalog = catalog;
        this.paletteLoader = paletteLoader;
        this.dispatcher = dispatcher;
        this.environment = environment;
        this.logger = logger;
        ActiveTheme = catalog.ConcreteThemes.First(theme => theme.Id == ThemeIds.MissionDark);
    }

    /// <inheritdoc />
    public IReadOnlyList<ThemeOption> AvailableThemes => catalog.Options;

    /// <inheritdoc />
    public string SelectedThemeId { get; private set; } = ThemeIds.System;

    /// <inheritdoc />
    public ThemeDescriptor ActiveTheme { get; private set; }

    /// <inheritdoc />
    public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    /// <inheritdoc />
    public void Initialize(ResourceDictionary activeResources)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        this.activeResources = activeResources ?? throw new ArgumentNullException(nameof(activeResources));
        environment.RequestedThemeChanged -= OnRequestedThemeChanged;
        environment.RequestedThemeChanged += OnRequestedThemeChanged;
        environment.Attach();
    }

    /// <inheritdoc />
    public Task ApplyAsync(string themeId, CancellationToken cancellationToken = default)
    {
        return ApplyInternalAsync(themeId, true, cancellationToken);
    }

    /// <inheritdoc />
    public Task PreviewAsync(string themeId, CancellationToken cancellationToken = default)
    {
        return ApplyInternalAsync(themeId, false, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        environment.RequestedThemeChanged -= OnRequestedThemeChanged;
        environment.Dispose();

        applyLock.Dispose();
    }

    private async Task ApplyInternalAsync(string themeId, bool select, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (activeResources is null)
        {
            throw new InvalidOperationException("ThemeManager must be initialized with the active resource dictionary before applying a theme.");
        }

        var selectedThemeId = themeId;
        var usesSystemPolicy = string.Equals(themeId, ThemeIds.System, StringComparison.OrdinalIgnoreCase);
        var concreteThemeId = usesSystemPolicy
            ? ResolveSystemThemeId()
            : themeId;
        if (!catalog.TryGetTheme(concreteThemeId, out var theme) || theme is null)
        {
            logger.LogWarning("Theme {ThemeId} is not installed. Falling back to System.", themeId);
            selectedThemeId = ThemeIds.System;
            usesSystemPolicy = true;
            concreteThemeId = ResolveSystemThemeId();
            if (!catalog.TryGetTheme(concreteThemeId, out theme) || theme is null)
            {
                throw new InvalidOperationException("The theme catalog does not provide the System fallback palette.");
            }
        }

        await applyLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var palette = await RunOnDispatcherAsync(() => paletteLoader.Load(theme), cancellationToken).ConfigureAwait(false);
            var values = ValidatePalette(theme, palette);
            await RunOnDispatcherAsync(
                () => ApplyValidatedPalette(theme, values, select ? selectedThemeId : SelectedThemeId, usesSystemPolicy),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            applyLock.Release();
        }
    }

    private static IReadOnlyDictionary<string, Color> ValidatePalette(ThemeDescriptor theme, ResourceDictionary palette)
    {
        var values = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var key in ThemeResourceKeys.RequiredColorKeys)
        {
            if (!palette.TryGetValue(key, out var value))
            {
                throw new InvalidDataException($"Theme '{theme.Id}' does not provide required color '{key}'.");
            }

            if (value is not Color color)
            {
                throw new InvalidDataException($"Theme '{theme.Id}' resource '{key}' must be a Color.");
            }

            values.Add(key, color);
        }

        return values;
    }

    private void ApplyValidatedPalette(
        ThemeDescriptor theme,
        IReadOnlyDictionary<string, Color> values,
        string selectedThemeId,
        bool usesSystemPolicy)
    {
        foreach (var value in values)
        {
            activeResources![value.Key] = value.Value;
        }

        SelectedThemeId = selectedThemeId;
        assigningNativeAppearance = true;
        try
        {
            environment.SetUserTheme(usesSystemPolicy
                ? AppTheme.Unspecified
                : theme.BaseAppearance == ThemeBaseAppearance.Dark
                    ? AppTheme.Dark
                    : AppTheme.Light);
        }
        finally
        {
            assigningNativeAppearance = false;
        }

        ActiveTheme = theme;
        logger.LogInformation("Applied application theme {ThemeId} with {BaseAppearance} native appearance.", theme.Id, theme.BaseAppearance);
        ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(SelectedThemeId, ActiveTheme));
    }

    private string ResolveSystemThemeId()
    {
        return environment.RequestedTheme == AppTheme.Dark
            ? ThemeIds.MissionDark
            : ThemeIds.MissionLight;
    }

    private void OnRequestedThemeChanged(object? sender, AppTheme requestedTheme)
    {
        if (assigningNativeAppearance || !string.Equals(SelectedThemeId, ThemeIds.System, StringComparison.Ordinal))
        {
            return;
        }

        _ = ReapplySystemThemeAsync();
    }

    private async Task ReapplySystemThemeAsync()
    {
        try
        {
            await ApplyAsync(ThemeIds.System).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not ObjectDisposedException)
        {
            logger.LogError(exception, "Applying an operating-system theme transition failed.");
        }
    }

    private Task RunOnDispatcherAsync(Action action, CancellationToken cancellationToken)
    {
        return RunOnDispatcherAsync(
            () =>
            {
                action();
                return true;
            },
            cancellationToken);
    }

    private Task<T> RunOnDispatcherAsync<T>(Func<T> action, CancellationToken cancellationToken)
    {
        if (dispatcher.IsDispatchRequired)
        {
            var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            if (!dispatcher.Dispatch(() => Complete(action, completion)))
            {
                completion.TrySetException(new InvalidOperationException("The UI dispatcher rejected the theme operation."));
            }

            return completion.Task;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(action());
    }

    private static void Complete<T>(Func<T> action, TaskCompletionSource<T> completion)
    {
        try
        {
            completion.TrySetResult(action());
        }
        catch (Exception exception)
        {
            completion.TrySetException(exception);
        }
    }
}
