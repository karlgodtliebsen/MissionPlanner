#nullable enable

using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Material.VirtualizedDataGrid.Controls;
using Xunit;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGrid_Layout_Tests
{
    public VirtualizedDataGrid_Layout_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void AllocatedWidth_ShouldNotBeWrittenBackAsTableWidthRequest()
    {
        var grid = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn
                {
                    Title = "Name",
                    ValueBinding = new Binding(nameof(Row.Name))
                },
                new DataGridColumn
                {
                    Title = "Done",
                    ValueBinding = new Binding(nameof(Row.IsDone))
                }
            ]
        };

        grid.Arrange(new Rect(0, 0, 600, 400));

        grid.ExposedTableLayout.WidthRequest.ShouldBe(-1);
    }

    [Fact]
    public void DefaultMaterialSpacing_ShouldRemainStable()
    {
        var grid = new TestableVirtualizedDataGrid();

        grid.CellPadding.ShouldBe(new Thickness(0));
        grid.HeaderPadding.ShouldBe(new Thickness(0));
        grid.FillAvailableWidth.ShouldBeFalse();
    }

    [Fact]
    public void SelectionColumn_ShouldNotAddHorizontalPaddingAroundCheckboxMargin()
    {
        var grid = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridSelectionColumn(),
                new DataGridColumn
                {
                    Title = "Name",
                    ValueBinding = new Binding(nameof(Row.Name))
                }
            ]
        };

        var row = grid.CreateRow();
        var cells = row.Children
            .OfType<Grid>()
            .Single()
            .Children
            .OfType<ContentView>()
            .ToArray();

        cells[0].Padding.ShouldBe(new Thickness(0));
        cells[1].Padding.ShouldBe(new Thickness(0));
        cells[1].Content
            .ShouldBeOfType<Label>()
            .Margin.ShouldBe(new Thickness(20));
    }

    [Fact]
    public void RowsViewport_ShouldRemainBoundedForVirtualization()
    {
        var grid = new TestableVirtualizedDataGrid();

        grid.ExposedRowsView.VerticalOptions.ShouldBe(LayoutOptions.Fill);
        grid.ExposedRowsHost.VerticalOptions.ShouldBe(LayoutOptions.Fill);
        grid.ExposedTableLayout.RowDefinitions[1].Height.ShouldBe(GridLength.Star);
        grid.ExposedRootLayout.RowDefinitions[1].Height.ShouldBe(GridLength.Star);
    }

    [Fact]
    public void FixedRowHeight_ShouldBeAppliedToVirtualizedPresenters()
    {
        var grid = new TestableVirtualizedDataGrid
        {
            RowHeight = 100,
            Columns =
            [
                new DataGridColumn
                {
                    Title = "Name",
                    ValueBinding = new Binding(nameof(Row.Name))
                }
            ]
        };

        grid.CreateRow().HeightRequest.ShouldBe(100);
    }

    [Fact]
    public void TemplatedAutoColumns_ShouldNotBeMeasuredWhenRowsAreRecycled()
    {
        var grid = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn
                {
                    Title = "Name",
                    CellItemTemplate = new DataTemplate(() => new Label())
                }
            ]
        };

        grid.ExposedHasContentMeasuredAutoColumn.ShouldBeFalse();
    }

    [Fact]
    public void GeneratedAutoColumns_ShouldBeMeasuredFromRealizedRows()
    {
        var grid = new TestableVirtualizedDataGrid
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

        grid.ExposedHasContentMeasuredAutoColumn.ShouldBeTrue();
    }

    [Fact]
    public void EmptySource_ShouldReleaseRealizedCellTrees()
    {
        var grid = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn
                {
                    Title = "Name",
                    ValueBinding = new Binding(nameof(Row.Name))
                }
            ],
            ItemsSource = new ObservableCollection<Row> { new("A", false) }
        };
        var row = grid.CreateRow();
        row.BindingContext = grid.ItemsSource[0];

        grid.ItemsSource = null;

        row.Children.ShouldBeEmpty();
        row.RowDefinitions.ShouldBeEmpty();
    }

    [Fact]
    public void ReleasedPresenter_ShouldRebuildWhenReusedForAnItem()
    {
        var grid = new TestableVirtualizedDataGrid
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
        var row = grid.CreateRow();

        grid.ItemsSource = new ObservableCollection<Row> { new("First", false) };
        grid.ItemsSource = null;
        row.BindingContext = new Row("Reused", false);

        row.Children.ShouldNotBeEmpty();
    }

    private sealed class TestableVirtualizedDataGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public CollectionView ExposedRowsView => RowsView;

        public Grid ExposedRowsHost => ExposedTableLayout.Children
            .OfType<Grid>()
            .Single(child => child.Children.Contains(RowsView));

        public bool ExposedHasContentMeasuredAutoColumn =>
            HasContentMeasuredAutoColumn();

        public Grid ExposedRootLayout => Content.ShouldBeOfType<Grid>();

        public Grid CreateRow()
        {
            return RowsView.ItemTemplate.CreateContent().ShouldBeAssignableTo<Grid>();
        }

        public Grid ExposedTableLayout
        {
            get
            {
                var root = Content.ShouldBeOfType<Grid>();
                var horizontalScroll = root.Children
                    .OfType<ScrollView>()
                    .Single();

                return horizontalScroll.Content.ShouldBeOfType<Grid>();
            }
        }
    }
    private sealed record Row(string Name, bool IsDone);
}
