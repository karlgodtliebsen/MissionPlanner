using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using UraniumUI.Material.Extensions.Samples.DataGrids.Models;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples;

public partial class CompareDataGridsViewModel : ObservableObject
{
    public ObservableRangeCollection<TodoItem> Items { get; } = [];
    public ObservableCollection<TodoItem> SelectedItems { get; set; } = [];

    private TodoItem newItem = new();

    public TodoItem NewItem
    {
        get => newItem;
        set
        {
            newItem = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddNewItemCommand { get; private set; }

    public ICommand RemoveSelectedItemsCommand { get; private set; }

    public CompareDataGridsViewModel()
    {
        if (Items.Count == 0)
        {
            Items.Add(new TodoItem { Content = "Throw away the rubbish", Type = TodoItem.TodoItemType.Personal });
            Items.Add(new TodoItem { Content = "Attend the meeting today\n11:00AM", Type = TodoItem.TodoItemType.Work });
            Items.Add(new TodoItem { Content = "Prepare presentation for new project", Type = TodoItem.TodoItemType.Work });
            Items.Add(new TodoItem { Content = "Spend time with family", Type = TodoItem.TodoItemType.Family });
            Items.Add(new TodoItem { Content = "Complete the puzzle", Type = TodoItem.TodoItemType.Hobby });
            Items.Add(new TodoItem { Content = "Don't forget to call dad", Type = TodoItem.TodoItemType.Family });
        }

        AddNewItemCommand = new Command(() =>
        {
            Items.Insert(0, NewItem);
            NewItem = new TodoItem();
        });

        RemoveSelectedItemsCommand = new Command(() =>
        {
            foreach (var item in SelectedItems)
            {
                Items.Remove(item);
            }
        });
    }
}
