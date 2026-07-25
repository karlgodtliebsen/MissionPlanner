#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

/// <summary>
/// Managed-state tests for the reversible CollectionView lifecycle.
///
/// Native PlatformView teardown/recreation still needs a Windows/Android handler
/// integration test because the mock handler does not reproduce that MAUI race.
/// </summary>
public class VirtualizedDataGrid_RowsLifecycle_Tests
{
    public VirtualizedDataGrid_RowsLifecycle_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void SourceAssignedBeforeLoad_ShouldRemainPending()
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

        control.PendingRowsUpdate.ShouldBeTrue();
        control.ExposedRowsView.ItemsSource.ShouldBeNull();
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

        control.PendingRowsUpdate.ShouldBeTrue();
        control.DesiredSource.ShouldBeSameAs(latest);
    }

    private sealed class TestableVirtualizedDataGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public CollectionView ExposedRowsView => RowsView;

        public bool PendingRowsUpdate => HasPendingRowsSourceUpdate;

        public IList? DesiredSource
        {
            get
            {
                var field = typeof(VirtualizedDataGrid.Controls.VirtualizedDataGrid)
                    .GetField(
                        "desiredRowsSource",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);

                return (IList?)field?.GetValue(this);
            }
        }
    }

    private sealed record Row(string Name);
}
