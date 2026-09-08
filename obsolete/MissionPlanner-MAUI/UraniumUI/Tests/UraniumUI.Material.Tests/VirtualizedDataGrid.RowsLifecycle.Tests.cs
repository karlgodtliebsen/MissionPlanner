#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using Xunit;

namespace UraniumUI.Material.Tests;

/// <summary>
/// Managed-state tests for the lightweight rows-host lifecycle.
/// </summary>
public class VirtualizedDataGrid_RowsLifecycle_Tests
{
    public VirtualizedDataGrid_RowsLifecycle_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void SourceAssignedBeforeLoad_ShouldBeAvailableImmediately()
    {
        var rows = new ObservableCollection<Row> { new("A") };

        var control = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn { Title = "Name", ValueBinding = new Binding(nameof(Row.Name)) }
            ],
            ItemsSource = rows
        };

        control.PendingRowsUpdate.ShouldBeFalse();
        control.DesiredSource.ShouldBeSameAs(rows);
    }

    [Fact]
    public void MultipleDetachedAssignments_ShouldKeepLatestDesiredSource()
    {
        var first = new ObservableCollection<Row> { new("Old") };

        var latest = new ObservableCollection<Row> { new("New") };

        var control = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn { Title = "Name", ValueBinding = new Binding(nameof(Row.Name)) }
            ],
            ItemsSource = first
        };
        control.ItemsSource = null;
        control.ItemsSource = latest;

        control.PendingRowsUpdate.ShouldBeFalse();
        control.DesiredSource.ShouldBeSameAs(latest);
    }

    [Fact]
    public void SuspendedRowsHost_ShouldIgnoreLateViewportUpdates()
    {
        var control = new TestableVirtualizedDataGrid
        {
            RowHeight = 40,
            Columns =
            [
                new DataGridColumn { Title = "Name", ValueBinding = new Binding(nameof(Row.Name)) }
            ],
            ItemsSource = new ObservableCollection<Row>(
                Enumerable.Range(0, 100).Select(index => new Row($"Row {index}")))
        };
        control.CalculateViewport();

        control.SuspendPresentation();
        control.CalculateViewport();

        control.RealizedRows.ShouldBe(0);
    }

    private sealed class TestableVirtualizedDataGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public bool PendingRowsUpdate => HasPendingRowsSourceUpdate;
        public int RealizedRows => RealizedRowCount;

        public void CalculateViewport() => UpdateRowsViewport(0, 400);

        public void SuspendPresentation()
        {
            typeof(VirtualizedDataGrid.Controls.VirtualizedDataGrid)
                .GetMethod(
                    "SuspendRowsPresentation",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)!
                .Invoke(this, null);
        }

        public IList? DesiredSource
        {
            get
            {
                var fieldInfo = typeof(VirtualizedDataGrid.Controls.VirtualizedDataGrid)
                    .GetField(
                        "desiredRowsSource",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);

                return (IList?)fieldInfo?.GetValue(this);
            }
        }
    }

    private sealed record Row(string Name);
}
