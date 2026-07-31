using CommunityToolkit.Mvvm.ComponentModel;

namespace UraniumUI.Material.Extensions.Samples.DataGrids.Models;

public partial class TodoItem : ObservableObject
{
    [ObservableProperty] public partial string Content { get; set; } = null!;

    [ObservableProperty] public partial bool IsDone { get; set; }

    [ObservableProperty] public partial TodoItemType Type { get; set; }

    public static TodoItemType[]? AvailableTypes => Enum.GetValues(typeof(TodoItemType)) as TodoItemType[];

    public enum TodoItemType
    {
        Personal,
        Work,
        Hobby,
        Family
    }
}
