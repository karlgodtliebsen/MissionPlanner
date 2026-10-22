using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Irihi.Avalonia.Shared.Helpers;

namespace MissionPlanner.Themes.Controls;

[TemplatePart(PART_CloseButton, typeof(Button))]
[TemplatePart(PART_ToggleExpandButton, typeof(Button))]
[TemplatePart(PART_ToggleShrinkButton, typeof(Button))]
public class ExpandBanner : HeaderedContentControl
{
    public const string PART_CloseButton = "PART_CloseButton";
    public const string PART_ToggleExpandButton = "PART_ToggleExpandButton";
    public const string PART_ToggleShrinkButton = "PART_ToggleShrinkButton";

    private Button? _closeButton;
    private Button? _toggleExpandButton;
    private Button? _toggleShrinkButton;

    public static readonly StyledProperty<bool> CanCloseProperty = AvaloniaProperty.Register<ExpandBanner, bool>(nameof(CanClose), false);

    [ExcludeFromCodeCoverage]
    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    public static readonly StyledProperty<bool> CanExpandProperty = AvaloniaProperty.Register<ExpandBanner, bool>(nameof(CanExpand), true);

    [ExcludeFromCodeCoverage]
    public bool CanExpand
    {
        get => GetValue(CanExpandProperty);
        set => SetValue(CanExpandProperty, value);
    }

    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<ExpandBanner, bool>(nameof(IsExpanded), false);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Button.ClickEvent.RemoveHandler(OnCloseClick, _closeButton);
        _closeButton = e.NameScope.Find<Button>(PART_CloseButton);
        Button.ClickEvent.AddHandler(OnCloseClick, _closeButton);
        Button.ClickEvent.RemoveHandler(OnToggleExpandClick, _toggleExpandButton);
        Button.ClickEvent.RemoveHandler(OnToggleExpandClick, _toggleShrinkButton);
        _toggleExpandButton = e.NameScope.Find<Button>(PART_ToggleExpandButton);
        _toggleShrinkButton = e.NameScope.Find<Button>(PART_ToggleShrinkButton);
        Button.ClickEvent.AddHandler(OnToggleExpandClick, _toggleExpandButton);
        Button.ClickEvent.AddHandler(OnToggleExpandClick, _toggleShrinkButton);
    }

    [ExcludeFromCodeCoverage]
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private void OnToggleExpandClick(object? sender, RoutedEventArgs args)
    {
        if (CanExpand)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs args)
    {
        SetCurrentValue(IsVisibleProperty, false);
    }
}
