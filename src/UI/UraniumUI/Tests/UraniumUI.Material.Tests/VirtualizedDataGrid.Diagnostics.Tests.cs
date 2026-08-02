#nullable enable

using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using Xunit;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGrid_Diagnostics_Tests
{
    public VirtualizedDataGrid_Diagnostics_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void PresenterLifecycle_ShouldPopulateDiagnostics()
    {
        var grid = CreateGrid();
        var row = grid.CreateRow();

        row.BindingContext = new Row("A");
        grid.ItemsSource = new ObservableCollection<Row> { new("A") };
        grid.ItemsSource = null;

        grid.Diagnostics.PresenterCreatedCount.ShouldBe(1);
        grid.Diagnostics.PresenterBindingContextChangeCount.ShouldBeGreaterThan(0);
        grid.Diagnostics.PresenterBuildCount.ShouldBeGreaterThanOrEqualTo(1);
        grid.Diagnostics.PresenterReleaseCount.ShouldBe(1);
        grid.Diagnostics.ReleasedCellCount.ShouldBe(1);
        grid.Diagnostics.LivePresenterCount.ShouldBe(0);
        grid.Diagnostics.DataViewRefreshCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void DisabledDiagnostics_ShouldNotCollectNewValues()
    {
        var grid = CreateGrid();
        grid.Diagnostics.Reset();
        grid.Diagnostics.IsEnabled = false;

        grid.ItemsSource = new ObservableCollection<Row> { new("A") };

        grid.Diagnostics.ItemsSourceChangeCount.ShouldBe(0);
        grid.Diagnostics.DataViewRefreshCount.ShouldBe(0);
    }

    [Fact]
    public void Reset_ShouldClearValuesAndPreserveEnabledState()
    {
        var grid = CreateGrid();
        grid.ItemsSource = new ObservableCollection<Row> { new("A") };

        grid.Diagnostics.Reset();

        grid.Diagnostics.IsEnabled.ShouldBeTrue();
        grid.Diagnostics.ItemsSourceChangeCount.ShouldBe(0);
        grid.Diagnostics.DataViewRefreshCount.ShouldBe(0);
        grid.Diagnostics.TotalDataViewRefreshDuration.ShouldBe(TimeSpan.Zero);
        grid.Diagnostics.LastGridLoadedAt.ShouldBeNull();
    }

    [Fact]
    public void CreateReport_ShouldProduceGroupedMultilineSnapshot()
    {
        var grid = CreateGrid();
        var row = grid.CreateRow();
        row.BindingContext = new Row("A");
        grid.ItemsSource = new ObservableCollection<Row> { new("A") };
        grid.ItemsSource = null;

        var report = grid.Diagnostics.CreateReport();

        report.ShouldContain("VirtualizedDataGrid diagnostics");
        report.ShouldContain("[Sources]");
        report.ShouldContain("[Presenters and cells]");
        report.ShouldContain("[Rendering and layout]");
        report.ShouldContain("[Lifecycle]");
        report.ShouldContain("Native ItemsSource setter: last");
        report.ShouldContain("Presenters created: 1");
        report.ShouldContain(" ms");
        report.ShouldNotEndWith(Environment.NewLine);
    }

    private static TestableVirtualizedDataGrid CreateGrid()
    {
        return new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn
                {
                    Title = "Name",
                    ValueBinding = new Binding(nameof(Row.Name))
                }
            ]
        };
    }

    private sealed class TestableVirtualizedDataGrid
        : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public Grid CreateRow() =>
            RowsView.ItemTemplate.CreateContent()
                .ShouldBeAssignableTo<Grid>();
    }

    private sealed record Row(string Name);
}
