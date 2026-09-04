using Microsoft.Extensions.Logging.Abstractions;
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

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Manager.ApplyAsync(ThemeIds.MissionLight, TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies all concrete palettes replace active semantic values.</summary>
    [Fact]
    public async Task ConcreteThemesReplaceSemanticValuesAndNotifyOnce()
    {
        using var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        var notifications = 0;
        fixture.Manager.ThemeChanged += (_, _) => notifications++;

        await fixture.Manager.ApplyAsync(ThemeIds.MissionLight, TestContext.Current.CancellationToken);
        var lightSurface = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Surface]);
        var lightPrimary = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Primary]);
        await fixture.Manager.ApplyAsync(ThemeIds.MissionDark, TestContext.Current.CancellationToken);
        var darkSurface = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Surface]);
        await fixture.Manager.ApplyAsync(ThemeIds.MissionBlue, TestContext.Current.CancellationToken);
        var bluePrimary = Assert.IsType<Color>(fixture.ActiveResources[ThemeResourceKeys.Primary]);
        await fixture.Manager.ApplyAsync(ThemeIds.MissionDark, TestContext.Current.CancellationToken);

        Assert.NotEqual(lightSurface, darkSurface);
        Assert.NotEqual(lightPrimary, bluePrimary);
        Assert.Equal(4, notifications);
        Assert.Equal(ThemeIds.MissionDark, fixture.Manager.ActiveTheme.Id);
        Assert.Equal(AppTheme.Dark, fixture.Environment.UserTheme);
    }

    /// <summary>Verifies unknown themes fall back safely and malformed palettes apply nothing.</summary>
    [Fact]
    public async Task InvalidThemesDoNotPartiallyApply()
    {
        using var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        await fixture.Manager.ApplyAsync(ThemeIds.MissionLight, TestContext.Current.CancellationToken);
        await fixture.Manager.ApplyAsync("not-installed", TestContext.Current.CancellationToken);
        Assert.Equal(ThemeIds.System, fixture.Manager.SelectedThemeId);
        Assert.Equal(ThemeIds.MissionLight, fixture.Manager.ActiveTheme.Id);

        await fixture.Manager.ApplyAsync(ThemeIds.MissionLight, TestContext.Current.CancellationToken);
        var original = fixture.ActiveResources[ThemeResourceKeys.Primary];
        fixture.Loader.ReturnMalformed = true;
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Manager.ApplyAsync(ThemeIds.MissionBlue, TestContext.Current.CancellationToken));

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
        await fixture.Manager.ApplyAsync(ThemeIds.System, TestContext.Current.CancellationToken);
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
        await transitioned.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        Assert.Equal(ThemeIds.MissionDark, fixture.Manager.ActiveTheme.Id);

        await fixture.Manager.ApplyAsync(ThemeIds.MissionBlue, TestContext.Current.CancellationToken);
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

    /// <summary>Verifies the regression switching sequence does not accumulate resources or subscriptions.</summary>
    [Fact]
    public async Task RepeatedSwitchingKeepsResourceAndSubscriptionCountsStable()
    {
        using var fixture = new ThemeManagerFixture();
        fixture.Initialize();
        var sequence = new[]
        {
            ThemeIds.MissionDark,
            ThemeIds.MissionLight,
            ThemeIds.MissionBlue,
            ThemeIds.MissionDark,
            ThemeIds.MissionBlue,
            ThemeIds.MissionLight
        };

        foreach (var themeId in sequence)
        {
            await fixture.Manager.ApplyAsync(themeId, TestContext.Current.CancellationToken);
            Assert.Equal(ThemeResourceKeys.RequiredColorKeys.Count, fixture.ActiveResources.Count);
            Assert.Equal(1, fixture.Environment.SubscriberCount);
        }

        Assert.Equal(sequence.Length, fixture.Loader.LoadCount);
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

        public ThemeManager Manager
        {
            get;
        }

        public TestPaletteLoader Loader
        {
            get;
        }

        public TestThemeEnvironment Environment
        {
            get;
        }

        public ResourceDictionary ActiveResources { get; } = [];

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
        public bool ReturnMalformed
        {
            get; set;
        }

        public int LoadCount
        {
            get; private set;
        }

        public ResourceDictionary Load(ThemeDescriptor theme)
        {
            LoadCount++;
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

        public AppTheme UserTheme
        {
            get; private set;
        }

        public int SubscriberCount
        {
            get; private set;
        }

        public bool IsDisposed
        {
            get; private set;
        }

        public event EventHandler<AppTheme>? RequestedThemeChanged
        {
            add
            {
                requestedThemeChanged += value;
                SubscriberCount = requestedThemeChanged?.GetInvocationList().Length ?? 0;
            }
            remove
            {
                requestedThemeChanged -= value;
                SubscriberCount = requestedThemeChanged?.GetInvocationList().Length ?? 0;
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
