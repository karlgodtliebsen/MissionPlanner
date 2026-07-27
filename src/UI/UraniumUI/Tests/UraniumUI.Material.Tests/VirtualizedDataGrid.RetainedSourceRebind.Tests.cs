#nullable enable

using System.Collections;
using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Tests.Core;
using Xunit;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGrid_RetainedSourceRebind_Tests
{
    public VirtualizedDataGrid_RetainedSourceRebind_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void SameSourceAfterPresentationResume_ShouldRemainPendingForRebind()
    {
        var source = new ObservableCollection<Row> { new(1), new(2) };

        var grid = AnimationReadyHandler.Prepare(
            new TestGrid
            {
                Columns =
                [
                    new DataGridColumn { Title = "Id", ValueBinding = new Binding(nameof(Row.Id)) }
                ],
                ItemsSource = source
            });

        grid.SimulateSuspend();
        grid.ItemsSource = source;

        grid.HasPending.ShouldBeTrue();
    }

    [Fact]
    public void LatestSource_ShouldWinAcrossDetachedChanges()
    {
        var first = new ObservableCollection<Row> { new(1) };
        var second = new ObservableCollection<Row> { new(2), new(3) };

        var grid = AnimationReadyHandler.Prepare(
            new TestGrid
            {
                Columns =
                [
                    new DataGridColumn { Title = "Id", ValueBinding = new Binding(nameof(Row.Id)) }
                ],
                ItemsSource = first
            });

        grid.SimulateSuspend();
        grid.ItemsSource = null;
        grid.ItemsSource = second;

        grid.DesiredSource.ShouldBeSameAs(second);
        grid.HasPending.ShouldBeTrue();
    }

    private sealed class TestGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public bool HasPending => HasPendingRowsSourceUpdate;

        public IList? DesiredSource
        {
            get
            {
                var field = typeof(VirtualizedDataGrid.Controls.VirtualizedDataGrid)
                    .GetField(
                        "desiredRowsSource",
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.NonPublic);

                return field?.GetValue(this) as IList;
            }
        }

        public void SimulateSuspend()
        {
            var method = typeof(VirtualizedDataGrid.Controls.VirtualizedDataGrid)
                .GetMethod(
                    "SuspendRowsPresentation",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);

            method?.Invoke(this, null);
        }
    }

    private sealed record Row(int Id);
}
