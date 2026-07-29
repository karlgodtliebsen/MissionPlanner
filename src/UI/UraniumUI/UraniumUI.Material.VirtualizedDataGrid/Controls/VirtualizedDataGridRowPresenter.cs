using UraniumUI.Extensions;
using UraniumUI.Material.Controls;
using CheckBox = Microsoft.Maui.Controls.CheckBox;

namespace UraniumUI.Material.VirtualizedDataGrid.Controls;

/// <summary>
/// Represents one realized CollectionView row. The number of these presenters is controlled
/// by the platform CollectionView rather than by ItemsSource.Count.
/// </summary>
internal sealed class VirtualizedDataGridRowPresenter : Grid
{
    private readonly VirtualizedDataGrid owner;
    private readonly List<ContentView> selectionCells = [];
    private readonly List<TemplateValueBindingCell> templateValueBindingCells = [];

    private Grid? cellsGrid;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizedDataGridRowPresenter"/> class.
    /// </summary>
    /// <param name="owner">The data grid that owns the row presenter.</param>
    public VirtualizedDataGridRowPresenter(VirtualizedDataGrid owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));

        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Start;

        owner.RegisterPresenter(this);
        RefreshFromOwner();
    }

    /// <inheritdoc />
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        UpdateTemplateValueBindings();
        owner.ApplySelectionState(this);
        owner.RequestAutoColumnMeasurement();
    }

    /// <summary>
    /// Rebuilds the row content from the owning data grid's current configuration.
    /// </summary>
    internal void RefreshFromOwner()
    {
        Children.Clear();
        RowDefinitions.Clear();
        selectionCells.Clear();
        templateValueBindingCells.Clear();

        owner.ConfigureSelectionVisualStates(this);

        cellsGrid = new Grid { HorizontalOptions = LayoutOptions.Fill, ColumnSpacing = Math.Max(0, owner.ColumnSpacing) };

        var columns = owner.GetColumnsSnapshot();
        var widths = owner.GetResolvedColumnWidths();

        BuildColumnDefinitions(columns, widths);
        BuildCells(columns);

        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetColumn(cellsGrid, 0);
        Grid.SetRow(cellsGrid, 0);
        Children.Add(cellsGrid);

        if (owner.ShowRowSeparators)
        {
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var separator = owner.CreateRowSeparator();
            Grid.SetRow(separator, 1);
            Children.Add(separator);
        }

        HeightRequest = owner.RowHeight > 0
            ? owner.RowHeight
            : -1;

        UpdateTemplateValueBindings();
        owner.ApplySelectionState(this);
    }

    /// <summary>
    /// Applies resolved absolute widths to the realized cells.
    /// </summary>
    /// <param name="widths">The width of each column, in device-independent units.</param>
    internal void ApplyColumnWidths(IReadOnlyList<double> widths)
    {
        if (cellsGrid is null)
        {
            return;
        }

        var columnCount = Math.Min(
            cellsGrid.ColumnDefinitions.Count,
            widths.Count);

        for (var index = 0; index < columnCount; index++)
        {
            cellsGrid.ColumnDefinitions[index].Width =
                new GridLength(Math.Max(0, widths[index]), GridUnitType.Absolute);
        }
    }

    /// <summary>
    /// Measures each realized cell without the shared column constraint. This lets
    /// the owner resolve Auto columns from visible content while retaining separate
    /// grids for the virtualized rows.
    /// </summary>
    internal IReadOnlyList<double> MeasureNaturalColumnWidths()
    {
        if (cellsGrid is null)
        {
            return [];
        }

        var widths = new double[cellsGrid.ColumnDefinitions.Count];
        var columns = owner.GetColumnsSnapshot();

        foreach (var child in cellsGrid.Children.OfType<View>())
        {
            var column = Grid.GetColumn(child);

            if (column >= 0 &&
                column < widths.Length &&
                column < columns.Count &&
                child.IsVisible &&
                columns[column].CellItemTemplate is null &&
                owner.CellItemTemplate is null)
            {
                var desiredWidth = child is ContentView { Content: View contentView } cell
                    ? contentView.Measure(
                        double.PositiveInfinity,
                        double.PositiveInfinity).Width +
                      cell.Padding.Left +
                      cell.Padding.Right
                    : child.Measure(
                        double.PositiveInfinity,
                        double.PositiveInfinity).Width;

                widths[column] = Math.Max(
                    widths[column],
                    desiredWidth);
            }
        }

        return widths;
    }

    /// <summary>
    /// Updates the row and its selection cells to reflect the selection state.
    /// </summary>
    /// <param name="isSelected"><see langword="true"/> when the row is selected; otherwise, <see langword="false"/>.</param>
    internal void ApplySelectionStateCore(bool isSelected)
    {
        VisualStateManager.GoToState(
            this,
            isSelected
                ? VirtualizedDataGrid.DataGridCellVisualStates.Selected
                : VirtualizedDataGrid.DataGridCellVisualStates.Unselected);

        foreach (var selectionCell in selectionCells)
        {
            var checkBox = FindSelectionCheckBox(selectionCell);

            if (checkBox is not null && checkBox.IsChecked != isSelected)
            {
                checkBox.IsChecked = isSelected;
            }
        }
    }

    private void BuildColumnDefinitions(
        IReadOnlyList<DataGridColumn> columns,
        IReadOnlyList<double> widths)
    {
        if (cellsGrid is null)
        {
            return;
        }

        for (var index = 0; index < columns.Count; index++)
        {
            var width = index < widths.Count
                ? widths[index]
                : 0;

            cellsGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(Math.Max(0, width), GridUnitType.Absolute) });
        }
    }

    private void BuildCells(IReadOnlyList<DataGridColumn> columns)
    {
        if (cellsGrid is null)
        {
            return;
        }

        for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
        {
            var column = columns[columnIndex];
            BindingBase? valueBinding = null;
            if (column.ValueBinding is { } configuredBinding)
            {
                try
                {
                    valueBinding = configuredBinding.CopyAsClone();
                }
                catch (NotSupportedException)
                {
                    // Compiled x:DataType bindings are TypedBinding instances. UraniumUI's
                    // clone helper intentionally cannot clone BindingBase subclasses, but
                    // MAUI can still create a target-specific binding expression from the
                    // configured binding.
                    valueBinding = configuredBinding;
                }
            }

            var created =
                column.CellItemTemplate?.CreateContent() as View
                ?? owner.CellItemTemplate?.CreateContent() as View
                ?? (valueBinding is not null
                    ? owner.LabelFactory(valueBinding)
                    : null)
                ?? new Label();

            var cell = new ContentView
            {
                Content = created,
                Padding = owner.GetCellPadding(column),
                IsVisible = column.IsVisible
            };

            cell.SetBinding(
                IsVisibleProperty,
                new Binding(nameof(DataGridColumn.IsVisible), source: column));

            VirtualizedDataGrid.ApplyStyleClassToView(
                cell,
                column.CellStyleClass);

            if (column is IDataGridSelectionColumn)
            {
                selectionCells.Add(cell);
            }

            // Preserve UraniumUI's DataGridValueBindingExtension contract for a shared
            // CellItemTemplate. The template receives a BindingBase as its BindingContext.
            if (column.CellItemTemplate is null &&
                owner.CellItemTemplate is not null &&
                column.ValueBinding is Binding)
            {
                templateValueBindingCells.Add(
                    new TemplateValueBindingCell(cell, column.ValueBinding));
            }

            Grid.SetColumn(cell, columnIndex);
            cellsGrid.Children.Add(cell);
        }
    }

    private void UpdateTemplateValueBindings()
    {
        foreach (var entry in templateValueBindingCells)
        {
            if (entry.BindingTemplate.CopyAsClone() is Binding binding)
            {
                binding.Source = BindingContext;
                entry.Cell.BindingContext = binding;
            }
        }
    }

    private static CheckBox? FindSelectionCheckBox(Element element)
    {
        if (element is CheckBox checkBox)
        {
            return checkBox;
        }

        if (element is ContentView { Content: Element content })
        {
            return FindSelectionCheckBox(content);
        }

        if (element is Layout layout)
        {
            foreach (var child in layout.Children.OfType<Element>())
            {
                var checkBoxChild = FindSelectionCheckBox(child);

                if (checkBoxChild is not null)
                {
                    return checkBoxChild;
                }
            }
        }

        return null;
    }

    private sealed record TemplateValueBindingCell(
        ContentView Cell,
        BindingBase BindingTemplate);
}
