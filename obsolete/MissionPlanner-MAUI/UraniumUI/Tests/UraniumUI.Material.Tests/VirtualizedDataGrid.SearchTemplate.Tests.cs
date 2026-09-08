#nullable enable

using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGrid_SearchTemplate_Tests
{
    public VirtualizedDataGrid_SearchTemplate_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void SearchTemplate_ShouldReceiveGridAsBindingContext()
    {
        var templateContent = new Label();

        var control = AnimationReadyHandler.Prepare(CreateGrid(
                new DataTemplate(() => templateContent)));

        control.ExposedSearchHost.Content.ShouldBeSameAs(templateContent);
        templateContent.BindingContext.ShouldBeSameAs(control);
    }

    [Fact]
    public void SearchView_ShouldTakePrecedenceAndKeepInheritedContext()
    {
        var directView = new Label();
        var templateView = new Label();

        var control = AnimationReadyHandler.Prepare(
            CreateGrid(new DataTemplate(() => templateView)));

        control.SearchView = directView;

        control.ExposedSearchHost.Content.ShouldBeSameAs(directView);
        directView.BindingContext.ShouldNotBeSameAs(control);
    }

    [Fact]
    public void SearchBar_ShouldRemainVisibleWhenFilterHasNoMatches()
    {
        var control = AnimationReadyHandler.Prepare(CreateGrid());
        control.ItemsSource = new ObservableCollection<Row> { new("ARMING_CHECK") };

        control.FilterMemberPaths = nameof(Row.Name);
        control.FilterText = "NO_MATCH";

        control.IsEmpty.ShouldBeTrue();
        control.ExposedSearchHost.IsVisible.ShouldBeTrue();
        control.ExposedEmptyViewHost.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void ClearSearchCommand_ShouldClearFilterText()
    {
        var control = AnimationReadyHandler.Prepare(CreateGrid());
        control.FilterText = "battery";

        control.HasSearchText.ShouldBeTrue();
        control.ClearSearchCommand.CanExecute(null).ShouldBeTrue();

        control.ClearSearchCommand.Execute(null);

        control.FilterText.ShouldBe(string.Empty);
        control.HasSearchText.ShouldBeFalse();
        control.ClearSearchCommand.CanExecute(null).ShouldBeFalse();
    }

    private static TestableGrid CreateGrid(DataTemplate? searchTemplate = null)
    {
        return new TestableGrid
        {
            ShowSearchBar = true,
            SearchDelayMilliseconds = 0,
            SearchTemplate = searchTemplate,
            Columns =
            [
                new DataGridColumn { Title = "Name", Width = GridLength.Star, ValueBinding = new Binding(nameof(Row.Name)) }
            ]
        };
    }

    private sealed class TestableGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public ContentView ExposedSearchHost => SearchHost;
        public ContentView ExposedEmptyViewHost => EmptyViewHost;
    }

    private sealed record Row(string Name);
}
