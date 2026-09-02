using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MissionPlanner.AvaloniaUI.App.Controls;

/// <summary>
/// Grid used as the root of VirtualizedItemsGrid header and row templates.
/// It automatically mirrors the owning VirtualizedItemsGrid column geometry,
/// so header and realized rows cannot drift apart.
/// </summary>
public sealed class VirtualizedItemsGridRow : Grid
{
    private VirtualizedItemsGrid? owner;

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachOwner();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachOwner();
        base.OnDetachedFromVisualTree(e);
    }

    private void AttachOwner()
    {
        DetachOwner();

        owner = this.GetVisualAncestors().OfType<VirtualizedItemsGrid>().FirstOrDefault();
        if (owner is null)
        {
            return;
        }

        owner.PropertyChanged += OwnerOnPropertyChanged;
        ApplyGeometry();
    }

    private void DetachOwner()
    {
        owner?.PropertyChanged -= OwnerOnPropertyChanged;
        owner = null;
    }

    private void OwnerOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == VirtualizedItemsGrid.ColumnWidthsProperty ||
            e.Property == VirtualizedItemsGrid.ColumnSpacingProperty)
        {
            ApplyGeometry();
        }
    }

    private void ApplyGeometry()
    {
        if (owner is null)
        {
            return;
        }

        ColumnDefinitions.Clear();
        foreach (var width in owner.GetParsedColumnWidths())
        {
            ColumnDefinitions.Add(new ColumnDefinition(width));
        }

        ColumnSpacing = owner.ColumnSpacing;
    }
}
