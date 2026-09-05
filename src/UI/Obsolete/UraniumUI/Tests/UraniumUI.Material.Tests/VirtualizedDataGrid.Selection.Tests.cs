#nullable enable

using System.Collections.ObjectModel;
using Shouldly;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Material.VirtualizedDataGrid.Controls;
using Xunit;
using VirtualizedGrid = UraniumUI.Material.VirtualizedDataGrid.Controls.VirtualizedDataGrid;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGrid_Selection_Tests
{
    public VirtualizedDataGrid_Selection_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Fact]
    public void SelectionMode_ShouldDefaultToMultiple()
    {
        new VirtualizedGrid().SelectionMode.ShouldBe(SelectionMode.Multiple);
    }

    [Fact]
    public void SelectedItem_ShouldSynchronizeSelectedItemsInSingleMode()
    {
        var first = new Row("First");
        var second = new Row("Second");
        var control = new VirtualizedGrid
        {
            SelectionMode = SelectionMode.Single,
            SelectedItems = new ObservableCollection<object> { first }
        };

        control.SelectedItem = second;

        control.SelectedItem.ShouldBeSameAs(second);
        control.SelectedItems.Count.ShouldBe(1);
        control.SelectedItems[0].ShouldBeSameAs(second);
    }

    [Fact]
    public void SingleMode_ShouldKeepOnlyNewestExternallySelectedItem()
    {
        var first = new Row("First");
        var second = new Row("Second");
        var selectedItems = new ObservableCollection<object> { first };
        var control = new VirtualizedGrid
        {
            SelectionMode = SelectionMode.Single,
            SelectedItems = selectedItems
        };

        selectedItems.Add(second);

        control.SelectedItem.ShouldBeSameAs(second);
        selectedItems.Count.ShouldBe(1);
        selectedItems[0].ShouldBeSameAs(second);
    }

    [Fact]
    public void SelectionChangedCommand_ShouldReceivePreviousAndCurrentSelection()
    {
        var first = new Row("First");
        VirtualizedDataGridSelectionChangedEventArgs? received = null;
        var control = new VirtualizedGrid
        {
            SelectionMode = SelectionMode.Single,
            SelectionChangedCommand = new Command<VirtualizedDataGridSelectionChangedEventArgs>(args => received = args)
        };

        control.SelectedItem = first;

        received.ShouldNotBeNull();
        received.PreviousSelection.ShouldBeEmpty();
        received.CurrentSelection.ShouldHaveSingleItem().ShouldBeSameAs(first);
        received.SelectedItem.ShouldBeSameAs(first);
    }

    [Fact]
    public void NoneMode_ShouldClearSelection()
    {
        var control = new VirtualizedGrid
        {
            SelectedItems = new ObservableCollection<object> { new Row("First") }
        };

        control.SelectionMode = SelectionMode.None;

        control.SelectedItem.ShouldBeNull();
        control.SelectedItems.Count.ShouldBe(0);
    }

    private sealed record Row(string Name);
}
