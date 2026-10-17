using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Controls;
using MissionPlanner.App.Models;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Exercises the shared toolbar with the real parameter tab markup.</summary>
[Collection("Document rendering")]
public sealed class ParameterEditorToolbarTests
{
    /// <summary>Creates an offline application with the production control themes.</summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure(() =>
        new MissionPlanner.App.App(FirmwarePanelViewModelTests.CreateServices(services =>
        {
            services.AddSingleton(Substitute.For<IVehicleConnectionSession>());
            services.AddSingleton(Substitute.For<IVehicleConnectionService>());
            services.AddSingleton(Substitute.For<IParameterEditSessionFactory>());
            services.AddSingleton(Substitute.For<IVehicleParameterLoadStatusContext>());
            services.AddSingleton(Substitute.For<IParameterProfileRepository>());
            services.AddSingleton(Substitute.For<IParameterProfileService>());
            services.AddSingleton(Substitute.For<IDateTimeProvider>());
            services.AddSingleton<IParameterComparisonService>(new ParameterComparisonService(ParameterValueEquivalence.Default));
            services.AddSingleton(new ParametersFileHandler(Substitute.For<IFileOpenService>(), Substitute.For<IFileSaveService>()));
            var factory = Substitute.For<IDomainFactory>();
            factory.Create<ParametersEditorViewModel>().Returns(_ => new ParametersEditorViewModel(Substitute.For<IDialogService>()));
            services.AddSingleton(factory);
            services.AddTransient<ParametersEditorTabViewModel>();
        })))
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Width changes reuse the editor and its search state, while other tabs remain compact.</summary>
    [Fact]
    public async Task EditorWidthTogglePreservesViewAndModel()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ParameterEditorToolbarTests));
        try
        {
            await session.Dispatch(() =>
            {
                var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
                var previous = Design.IsDesignMode;
                using var model = ServiceHelper.GetRequiredService<ParametersEditorTabViewModel>();
                Window? window = null;
                try
                {
                    designMode.SetValue(null, true);
                    var view = new ParametersEditorTabView { DataContext = model };
                    window = new Window { Width = 1500, Height = 900, Content = view };
                    window.Show();
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var layout = view.FindControl<Grid>("ParameterEditorLayout")!;
                    var tabs = view.FindControl<TabControl>("EditorTabs")!;
                    var toggle = view.FindControl<Button>("ToggleEditorWidthButton")!;
                    var editor = view.GetVisualDescendants().OfType<ParametersMainEditorView>().Single();
                    var grid = editor.GetVisualDescendants().OfType<VirtualizedItemsGrid>().Single();
                    grid.SearchText = "ACRO";
                    var parameters = model.Parameters;
                    Assert.Equal(900, layout.Bounds.Width);
                    Assert.Equal("Expand editor to full width", AutomationProperties.GetName(toggle));

                    toggle.Command!.Execute(null);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.True(layout.Bounds.Width > 1200);
                    Assert.True(editor.Bounds.Width > 1200);
                    Assert.Same(editor, view.GetVisualDescendants().OfType<ParametersMainEditorView>().Single());
                    Assert.Same(model, editor.DataContext);
                    Assert.Same(parameters, model.Parameters);
                    Assert.Equal("ACRO", grid.SearchText);
                    Assert.Equal("Restore compact editor width", AutomationProperties.GetName(toggle));

                    toggle.Command.Execute(null);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.Equal(900, layout.Bounds.Width);
                    Assert.Equal("ACRO", grid.SearchText);

                    toggle.Command.Execute(null);
                    tabs.SelectedIndex = 1;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.False(toggle.IsEffectivelyVisible);
                    Assert.Equal(900, layout.Bounds.Width);

                    tabs.SelectedIndex = 0;
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Assert.True(toggle.IsVisible);
                    Assert.True(layout.Bounds.Width > 1200);
                    Assert.Same(editor, view.GetVisualDescendants().OfType<ParametersMainEditorView>().Single());
                    Assert.Same(model, editor.DataContext);
                    Assert.Equal("ACRO", grid.SearchText);
                }
                finally
                {
                    window?.Close();
                    designMode.SetValue(null, previous);
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    /// <summary>Tab changes replace the action set, preserve Quick Editor text, and bind Clear to that editor.</summary>
    [Fact]
    public async Task SelectedTabControlsVisibleActionsAndQuickEditorCommands()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(ParameterEditorToolbarTests));
        try
        {
            await session.Dispatch(() =>
            {
                var designMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!;
                var previous = Design.IsDesignMode;
                using var model = ServiceHelper.GetRequiredService<ParametersEditorTabViewModel>();
                Window? window = null;
                try
                {
                    designMode.SetValue(null, true);
                    var view = new ParametersEditorTabView { DataContext = model };
                    window = new Window { Width = 1200, Height = 900, Content = view };
                    window.Show();
                    var tabs = view.FindControl<TabControl>("EditorTabs")!;
                    var toolbars = new[] { "EditorToolbar", "QuickEditorToolbar", "ProfileEditorToolbar", "CompareEditorToolbar" }
                        .Select(name => view.FindControl<WrapPanel>(name)!).ToArray();

                    void AssertToolbar(int selected, params string[] expected)
                    {
                        Dispatcher.UIThread.RunJobs();
                        window.UpdateLayout();
                        Assert.Same(toolbars[selected], Assert.Single(toolbars, toolbar => toolbar.IsVisible));
                        var toolbar = toolbars[selected];
                        foreach (var name in expected)
                        {
                            Assert.Contains(toolbar.Children, child => AutomationProperties.GetName(child) == name);
                        }
                        foreach (var button in toolbar.Children.OfType<Button>().Where(button => button.IsVisible && button is not DropDownButton))
                        {
                            Assert.NotNull(button.Command);
                        }
                    }

                    Assert.Equal(0, tabs.SelectedIndex);
                    AssertToolbar(0, "Load parameter file", "Save parameter file", "Parameter actions",
                        "Revert changes", "Copy all", "Cancel parameter load");

                    tabs.SelectedIndex = 1;
                    Assert.Equal(1, model.SelectedTabIndex);
                    AssertToolbar(1, "Clear Quick Editor");
                    var quickView = view.GetVisualDescendants().OfType<ParametersEditorView>().Single();
                    Assert.Same(model.QuickEditor, quickView.DataContext);
                    Assert.False(quickView.FindControl<StackPanel>("StandaloneQuickEditorToolbar")!.IsVisible);
                    model.QuickEditor.Text = "TEST_PARAM = 2";

                    tabs.SelectedIndex = 2;
                    Assert.Equal(2, model.SelectedTabIndex);
                    AssertToolbar(2);
                    tabs.SelectedIndex = 3;
                    Assert.Equal(3, model.SelectedTabIndex);
                    AssertToolbar(3, "Filter parameter differences", "Select parameters to write",
                        "Export parameter comparison", "Copy comparison", "Write selected parameters");

                    model.SelectedTabIndex = 1;
                    Assert.Equal(1, tabs.SelectedIndex);
                    AssertToolbar(1, "Clear Quick Editor");
                    Assert.Equal("TEST_PARAM = 2", model.QuickEditor.Text);
                    var clear = toolbars[1].Children.OfType<Button>().Single(button => AutomationProperties.GetName(button) == "Clear Quick Editor");
                    clear.Command!.Execute(null);
                    Assert.Empty(model.QuickEditor.Text);

                    model.SelectedTabIndex = 0;
                    model.SelectQuickEditorCommand.Execute(null);
                    Assert.Equal(1, tabs.SelectedIndex);
                }
                finally
                {
                    window?.Close();
                    designMode.SetValue(null, previous);
                }
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }
}
