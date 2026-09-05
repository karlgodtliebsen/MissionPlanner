using System.Windows.Input;
using UraniumUI.Material.Controls;
using UraniumUI.Resources;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// A selection column whose checkbox can update a property on the row and invoke
/// a command when its selection changes.
/// </summary>
public class ExtendedSelectableDataGridColumn : DataGridColumn, IDataGridSelectionColumn
{
    /// <summary>
    /// Gets or sets the row property that stores the selection state.
    /// A fresh two-way binding is created for every realized checkbox.
    /// </summary>
    public string? SelectionMemberPath { get; set; }

    /// <summary>
    /// Gets or sets the binding used to resolve the command for each row.
    /// </summary>
    /// <remarks>
    /// Unlike a bindable property on the shared column, this binding is applied to
    /// every realized checkbox and is therefore evaluated against the row item.
    /// </remarks>
    public BindingBase? SelectionCommand { get; set; }

    /// <summary>
    /// Gets or sets the row command property. Use this instead of
    /// <see cref="SelectionCommand"/> when compiled bindings are enabled.
    /// </summary>
    public string? SelectionCommandMemberPath { get; set; }

    /// <summary>
    /// Gets or sets the optional binding used to resolve the command parameter.
    /// The row item is used when this property is not set.
    /// </summary>
    public BindingBase? SelectionCommandParameter { get; set; }

    /// <summary>
    /// Gets or sets the row property used as the command parameter.
    /// </summary>
    public string? SelectionCommandParameterMemberPath { get; set; }

    /// <inheritdoc />
    public event EventHandler<bool>? SelectionChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExtendedSelectableDataGridColumn"/> class.
    /// </summary>
    public ExtendedSelectableDataGridColumn()
    {
        CellItemTemplate = new DataTemplate(CreateSelectionCell);
    }

    private View CreateSelectionCell()
    {
        var checkBox = new CommandCheckBox
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Type = InputKit.Shared.Controls.CheckBox.CheckType.Filled,
            Margin = 10,
            Color = ColorResource.GetColor("Primary", Colors.Red)
        };

        checkBox.Children.Remove(checkBox.Children.FirstOrDefault(child => child is Label));
        checkBox.SetDynamicResource(
            InputKit.Shared.Controls.CheckBox.BorderColorProperty,
            "OnBackground");

        if (!string.IsNullOrWhiteSpace(SelectionMemberPath))
        {
            checkBox.SetBinding(
                InputKit.Shared.Controls.CheckBox.IsCheckedProperty,
                new Binding(SelectionMemberPath, BindingMode.TwoWay));
        }
        else if (TryCloneBinding(ValueBinding) is { } valueBinding)
        {
            checkBox.SetBinding(
                InputKit.Shared.Controls.CheckBox.IsCheckedProperty,
                valueBinding);
        }

        var selectionCommand = !string.IsNullOrWhiteSpace(SelectionCommandMemberPath)
            ? new Binding(SelectionCommandMemberPath)
            : TryCloneBinding(SelectionCommand);
        if (selectionCommand is not null)
        {
            checkBox.SetBinding(
                CommandCheckBox.SelectionCommandProperty,
                selectionCommand);
        }

        var selectionCommandParameter =
            !string.IsNullOrWhiteSpace(SelectionCommandParameterMemberPath)
                ? new Binding(SelectionCommandParameterMemberPath)
                : TryCloneBinding(SelectionCommandParameter);
        if (selectionCommandParameter is not null)
        {
            checkBox.SetBinding(
                CommandCheckBox.SelectionCommandParameterProperty,
                selectionCommandParameter);
        }

        checkBox.CheckChanged += OnCheckChanged;
        return new ContentView { Content = checkBox };
    }

    private static BindingBase? TryCloneBinding(BindingBase? binding)
    {
        if (binding is null)
        {
            return null;
        }

        try
        {
            return binding.SafeCopyAsClone();
        }
        catch (NotSupportedException)
        {
            // A future binding implementation may not provide a MAUI clone path.
            return null;
        }
    }

    private void OnCheckChanged(object? sender, EventArgs args)
    {
        if (sender is not CommandCheckBox checkBox)
        {
            return;
        }

        SelectionChanged?.Invoke(checkBox, checkBox.IsChecked);

        var parameter = checkBox.SelectionCommandParameter ?? checkBox.BindingContext;
        if (checkBox.SelectionCommand?.CanExecute(parameter) == true)
        {
            checkBox.SelectionCommand.Execute(parameter);
        }
    }

    private sealed class CommandCheckBox : UraniumUI.Material.Controls.CheckBox
    {
        public static readonly BindableProperty SelectionCommandProperty = BindableProperty.Create(
            nameof(SelectionCommand),
            typeof(ICommand),
            typeof(CommandCheckBox));

        public static readonly BindableProperty SelectionCommandParameterProperty = BindableProperty.Create(
            nameof(SelectionCommandParameter),
            typeof(object),
            typeof(CommandCheckBox));

        public ICommand? SelectionCommand
        {
            get => (ICommand?)GetValue(SelectionCommandProperty);
            set => SetValue(SelectionCommandProperty, value);
        }

        public object? SelectionCommandParameter
        {
            get => GetValue(SelectionCommandParameterProperty);
            set => SetValue(SelectionCommandParameterProperty, value);
        }
    }
}
