using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.Graphics;
using MissionPlanner.App.Theming;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>
/// Verifies atomic application and policy behavior of the application theme manager.
/// </summary>
public sealed class ThemeManagerTests
{
    /// <summary>Verifies initialization is required before theme application.</summary>
    [Fact]
    public async Task ApplyRequiresInitialization()
    {
        using var fixture = new ThemeManagerFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Manager.ApplyAsync(ThemeIds.MissionLight));
    }

    /// <summary>Verifies all concrete palettes replace active semantic values.</summary>
    [Fact]
    public async Task ConcreteThemesReplaceSemanticValuesAndNotifyOnce()
    {
        using var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        var notifications = 0;
        fixture.Manager.ThemeChanged += (_, _) => notifications++;

        await fixture.Manager.ApplyAsync(ThemeIds.MissionLight);
        var lightSurface = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Surface]);
        var lightPrimary = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Primary]);
        await fixture.Manager.ApplyAsync(ThemeIds.MissionDark);
        var darkSurface = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Surface]);
        await fixture.Manager.ApplyAsync(ThemeIds.MissionBlue);
        var bluePrimary = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Primary]);
        await fixture.Manager.ApplyAsync(ThemeIds.MissionDark);

        Assert.NotEqual(lightSurface, darkSurface);
        Assert.NotEqual(lightPrimary, bluePrimary);
        Assert.Equal(4, notifications);
        Assert.Equal(ThemeIds.MissionDark, fixture.Manager.ActiveTheme.Id);
        Assert.Equal(AppTheme.Dark, fixture.Environment.UserTheme);
    }

    /// <summary>Verifies unknown and malformed themes leave the active palette unchanged.</summary>
    [Fact]
    public async Task InvalidThemesDoNotPartiallyApply()
    {
        using var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        await fixture.Manager.ApplyAsync(ThemeIds.MissionLight);
        var original = fixture.ActiveResources[ThemeResourceKeys.Primary];

        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Manager.ApplyAsync("not-installed"));
        fixture.Loader.ReturnMalformed = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Manager.ApplyAsync(ThemeIds.MissionBlue));

        Assert.Same(original, fixture.ActiveResources[ThemeResourceKeys.Primary]);
        Assert.Equal(ThemeIds.MissionLight, fixture.Manager.SelectedThemeId);
    }

    /// <summary>Verifies System follows OS changes while explicit Mission Blue does not.</summary>
    [Fact]
    public async Task SystemFollowsOperatingSystemButExplicitThemeDoesNot()
    {
        using var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        fixture.Environment.ChangeRequestedTheme(AppTheme.Light);
        await fixture.Manager.ApplyAsync(ThemeIds.System);
        Assert.Equal(ThemeIds.MissionLight, fixture.Manager.ActiveTheme.Id);
        Assert.Equal(AppTheme.Unspecified, fixture.Environment.UserTheme);

        var transitioned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Manager.ThemeChanged += (_, args) =>
        {
            if (args.ActiveTheme.Id == ThemeIds.MissionDark)
            {
                transitioned.TrySetResult();
            }
        };
        fixture.Environment.ChangeRequestedTheme(AppTheme.Dark);
        await transitioned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ThemeIds.MissionDark, fixture.Manager.ActiveTheme.Id);

        await fixture.Manager.ApplyAsync(ThemeIds.MissionBlue);
        fixture.Environment.ChangeRequestedTheme(AppTheme.Light);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(ThemeIds.MissionBlue, fixture.Manager.ActiveTheme.Id);
    }

    /// <summary>Verifies disposal releases the operating-system subscription.</summary>
    [Fact]
    public void DisposeUnsubscribesFromEnvironment()
    {
        var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        Assert.Equal(1, fixture.Environment.SubscriberCount);

        fixture.Dispose();

        Assert.Equal(0, fixture.Environment.SubscriberCount);
        Assert.True(fixture.Environment.IsDisposed);
    }

    private sealed class ThemeManagerFixture : IDisposable
    {
        public ThemeManagerFixture()
        {
            var dispatcher = Substitute.For<IDispatcher>();
            dispatcher.IsDispatchRequired.Returns(false);
            Loader = new TestPaletteLoader();
            Environment = new TestThemeEnvironment();
            Manager = new ThemeManager(
                new ThemeCatalog(),
                Loader,
                dispatcher,
                Environment,
                NullLogger<ThemeManager>.Instance);
        }

        public ThemeManager Manager { get; }

        public TestPaletteLoader Loader { get; }

        public TestThemeEnvironment Environment { get; }

        public ResourceDictionary ActiveResources { get; } = new();

        public void Initialize()
        {
            Manager.Initialize(ActiveResources);
        }

        public void Dispose()
        {
            Manager.Dispose();
        }
    }

    private sealed class TestPaletteLoader : IThemePaletteLoader
    {
        public bool ReturnMalformed { get; set; }

        public ResourceDictionary Load(ThemeDescriptor theme)
        {
            var dictionary = new ResourceDictionary();
            var seed = theme.Id switch
            {
                ThemeIds.MissionLight => 0x20,
                ThemeIds.MissionDark => 0x50,
                ThemeIds.MissionBlue => 0x80,
                _ => 0x10
            };

            foreach (var (key, index) in ThemeResourceKeys.RequiredColorKeys.Select((key, index) => (key, index)))
            {
                dictionary[key] = Color.FromRgb((byte)(seed + index), (byte)(seed + 1), (byte)(seed + 2));
            }

            if (ReturnMalformed)
            {
                dictionary.Remove(ThemeResourceKeys.OnSurface);
            }

            return dictionary;
        }
    }

    private sealed class TestThemeEnvironment : IThemeEnvironment
    {
        private EventHandler<AppTheme>? requestedThemeChanged;

        public AppTheme RequestedTheme { get; private set; } = AppTheme.Light;

        public AppTheme UserTheme { get; private set; }

        public int SubscriberCount { get; private set; }

        public bool IsDisposed { get; private set; }

        public event EventHandler<AppTheme>? RequestedThemeChanged
        {
            add
            {
                requestedThemeChanged += value;
                SubscriberCount++;
            }
            remove
            {
                requestedThemeChanged -= value;
                SubscriberCount--;
            }
        }

        public void Attach()
        {
        }

        public void SetUserTheme(AppTheme theme)
        {
            UserTheme = theme;
        }

        public void ChangeRequestedTheme(AppTheme theme)
        {
            RequestedTheme = theme;
            requestedThemeChanged?.Invoke(this, theme);
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
