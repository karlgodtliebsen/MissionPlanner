using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Material.VirtualizedDataGrid.Controls;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGridDataViewTests
{
    public VirtualizedDataGridDataViewTests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void EmptyView_ShouldBeVisibleOnlyWhenDisplayedViewIsEmpty()
    {
        var rows = new ObservableCollection<Row>();
        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));

        control.IsEmpty.ShouldBeTrue();
        control.ExposedEmptyViewHost.IsVisible.ShouldBeTrue();
        control.ExposedRowsView.IsVisible.ShouldBeFalse();

        rows.Add(new Row("A", "Alpha"));

        control.IsEmpty.ShouldBeFalse();
        control.ExposedEmptyViewHost.IsVisible.ShouldBeFalse();
        control.ExposedRowsView.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public void TextFilter_ShouldFilterConfiguredProperties()
    {
        var rows = new ObservableCollection<Row> { new("WP_SPEED", "Waypoint speed"), new("BATT_MONITOR", "Battery monitor") };

        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));
        control.FilterMemberPaths = "Name,Description";
        control.FilterText = "battery";

        control.TotalItemCount.ShouldBe(2);
        control.FilteredItemCount.ShouldBe(1);
        control.PageItemCount.ShouldBe(1);
        control.ExposedDisplayedItemsSource!.Cast<Row>().Single().Name.ShouldBe("BATT_MONITOR");
    }

    [Fact]
    public void Paging_ShouldExposeOnlyCurrentPageAndClampNavigation()
    {
        var rows = new ObservableCollection<Row>(
            Enumerable.Range(1, 25).Select(index => new Row($"P{index}", $"Parameter {index}")));

        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));
        control.EnablePaging = true;
        control.PageSize = 10;

        control.TotalPageCount.ShouldBe(3);
        control.PageItemCount.ShouldBe(10);
        control.HasPreviousPage.ShouldBeFalse();
        control.HasNextPage.ShouldBeTrue();

        control.GoToPage(3);

        control.CurrentPage.ShouldBe(3);
        control.PageItemCount.ShouldBe(5);
        control.HasPreviousPage.ShouldBeTrue();
        control.HasNextPage.ShouldBeFalse();

        control.GoToPage(99);
        control.CurrentPage.ShouldBe(3);
    }

    [Fact]
    public void FilterChange_ShouldReturnToFirstPage()
    {
        var rows = new ObservableCollection<Row>(
            Enumerable.Range(1, 30).Select(index => new Row($"P{index}", $"Parameter {index}")));

        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));
        control.EnablePaging = true;
        control.PageSize = 10;
        control.GoToPage(3);

        control.FilterMemberPaths = "Name";
        control.FilterText = "P1";

        control.CurrentPage.ShouldBe(1);
        control.FilteredItemCount.ShouldBe(11);
        control.TotalPageCount.ShouldBe(2);
    }

    [Fact]
    public void RemotePaging_ShouldDisplayFetchedPageWithoutSlicingItAgain()
    {
        var rows = new ObservableCollection<Row>(
            Enumerable.Range(1, 10)
                .Select(index => new Row($"P{index}", $"Parameter {index}")));
        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));

        control.EnablePaging = true;
        control.PageSize = 10;
        control.RemoteTotalItemCount = 194;
        control.IsRemotePaging = true;

        control.TotalItemCount.ShouldBe(194);
        control.TotalPageCount.ShouldBe(20);
        control.PageItemCount.ShouldBe(10);
        control.ExposedDisplayedItemsSource.ShouldBeSameAs(rows);
        control.HasNextPage.ShouldBeTrue();
    }

    [Fact]
    public void RemotePageNavigation_ShouldRaiseEventAndExecuteRequestCommand()
    {
        var rows = new ObservableCollection<Row>(
            Enumerable.Range(1, 10)
                .Select(index => new Row($"P{index}", $"Parameter {index}")));
        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));
        VirtualizedDataGridPageRequestEventArgs? eventRequest = null;
        VirtualizedDataGridPageRequestEventArgs? commandRequest = null;

        control.EnablePaging = true;
        control.PageSize = 10;
        control.RemoteTotalItemCount = 194;
        control.PageRequestedCommand = new Command<VirtualizedDataGridPageRequestEventArgs>(
            request => commandRequest = request);
        control.PageRequested += (_, request) => eventRequest = request;
        control.IsRemotePaging = true;
        eventRequest = null;
        commandRequest = null;

        control.GoToPage(3);

        eventRequest.ShouldNotBeNull();
        var request = eventRequest!;
        request.PageNumber.ShouldBe(3);
        request.PageSize.ShouldBe(10);
        request.Limit.ShouldBe(10);
        request.Skip.ShouldBe(20);
        commandRequest.ShouldBeSameAs(request);
    }

    [Fact]
    public void ReplacingRemotePageItems_ShouldNotRequestAnotherPage()
    {
        var rows = new ObservableCollection<Row> { new("P1", "Parameter 1") };
        var control = AnimationReadyHandler.Prepare(CreateGrid(rows));
        var requestCount = 0;
        control.EnablePaging = true;
        control.RemoteTotalItemCount = 194;
        control.PageRequested += (_, _) => requestCount++;
        control.IsRemotePaging = true;
        requestCount = 0;

        rows.Clear();
        rows.Add(new Row("P2", "Parameter 2"));

        requestCount.ShouldBe(0);
    }

    [Fact]
    public void RemotePageSizeChange_ShouldResetAndRequestFirstPage()
    {
        var control = AnimationReadyHandler.Prepare(CreateGrid(
            new ObservableCollection<Row> { new("P1", "Parameter 1") }));
        VirtualizedDataGridPageRequestEventArgs? request = null;
        control.EnablePaging = true;
        control.RemoteTotalItemCount = 194;
        control.IsRemotePaging = true;
        control.GoToPage(3);
        control.PageRequested += (_, args) => request = args;

        control.PageSize = 50;

        control.CurrentPage.ShouldBe(1);
        request.ShouldNotBeNull();
        request!.PageNumber.ShouldBe(1);
        request.PageSize.ShouldBe(50);
        request.Skip.ShouldBe(0);
    }

    private static TestableVirtualizedDataGrid CreateGrid(ObservableCollection<Row> rows)
    {
        return new TestableVirtualizedDataGrid
        {
            ItemsSource = rows,
            EmptyView = new Label { Text = "No rows" },
            Columns =
            [
                new DataGridColumn { Title = "Name", Width = 160, ValueBinding = new Binding(nameof(Row.Name)) },
                new DataGridColumn { Title = "Description", Width = GridLength.Star, ValueBinding = new Binding(nameof(Row.Description)) }
            ]
        };
    }

    private sealed class TestableVirtualizedDataGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public ScrollView ExposedRowsView => RowsView;
        public ContentView ExposedEmptyViewHost => EmptyViewHost;
        public System.Collections.IList? ExposedDisplayedItemsSource => DisplayedItemsSource;
    }

    private sealed record Row(string Name, string Description);
}
