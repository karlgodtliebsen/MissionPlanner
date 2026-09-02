using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace MissionPlanner.AvaloniaUI.App.Controls;

/// <summary>
/// Grid used as the root of VirtualizedItemsGrid header and row templates.
/// It automatically mirrors the owning VirtualizedItemsGrid column geometry,
/// including the resolved finite table width, so header and realized rows
/// cannot drift apart when star columns are used.
/// </summary>
public sealed class VirtualizedItemsGridRow : Grid
{
    private VirtualizedItemsGrid? _owner;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachOwner();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachOwner();
        base.OnDetachedFromVisualTree(e);
    }

    private void AttachOwner()
    {
        DetachOwner();

        _owner = this.GetVisualAncestors().OfType<VirtualizedItemsGrid>().FirstOrDefault();
        if (_owner is null)
        {
            return;
        }

        _owner.PropertyChanged += OwnerOnPropertyChanged;
        ApplyGeometry();
    }

    private void DetachOwner()
    {
        if (_owner is not null)
        {
            _owner.PropertyChanged -= OwnerOnPropertyChanged;
            _owner = null;
        }
    }

    private void OwnerOnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == VirtualizedItemsGrid.ColumnWidthsProperty ||
            e.Property == VirtualizedItemsGrid.ColumnSpacingProperty)
        {
            ApplyGeometry();
            return;
        }

        if (e.Property == VirtualizedItemsGrid.ResolvedColumnsWidthProperty)
        {
            ApplyResolvedWidth();
        }
    }

    private void ApplyGeometry()
    {
        if (_owner is null)
        {
            return;
        }

        ColumnDefinitions.Clear();
        foreach (var width in _owner.GetParsedColumnWidths())
        {
            ColumnDefinitions.Add(new ColumnDefinition(width));
        }

        ColumnSpacing = _owner.ColumnSpacing;
        ApplyResolvedWidth();
    }

    private void ApplyResolvedWidth()
    {
        if (_owner is null)
        {
            return;
        }

        if (_owner.ResolvedColumnsWidth > 0d)
        {
            Width = _owner.ResolvedColumnsWidth;
            HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            ClearValue(WidthProperty);
        }
    }
}
