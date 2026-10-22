using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Irihi.Avalonia.Shared.Helpers;

namespace MissionPlanner.Themes.Controls;

/// <summary>A banner with a header and content that can be expanded, shrunk and closed.</summary>
[TemplatePart(PART_CloseButton, typeof(Button))]
[TemplatePart(PART_ToggleExpandButton, typeof(Button))]
public class ExpandBanner : HeaderedContentControl
{
    /// <summary>The template part name of the close button.</summary>
    public const string PART_CloseButton = "PART_CloseButton";
    /// <summary>The template part name of the button that toggles the expanded state.</summary>
    public const string PART_ToggleExpandButton = "PART_ToggleExpandButton";
    public const string PART_Avatar = "PART_Avatar";

    private Button? _closeButton;
    private Button? _toggleExpandButton;

    /// <summary>Identifies the <see cref="CanClose"/> styled property.</summary>
    public static readonly StyledProperty<bool> CanCloseProperty = AvaloniaProperty.Register<ExpandBanner, bool>(nameof(CanClose), false);

    /// <summary>Gets or sets whether the banner shows a close button.</summary>
    [ExcludeFromCodeCoverage]
    public bool CanClose
    {
        get => GetValue(CanCloseProperty);
        set => SetValue(CanCloseProperty, value);
    }

    /// <summary>Identifies the <see cref="CanExpand"/> styled property.</summary>
    public static readonly StyledProperty<bool> CanExpandProperty = AvaloniaProperty.Register<ExpandBanner, bool>(nameof(CanExpand), true);

    /// <summary>Gets or sets whether the banner can be expanded.</summary>
    [ExcludeFromCodeCoverage]
    public bool CanExpand
    {
        get => GetValue(CanExpandProperty);
        set => SetValue(CanExpandProperty, value);
    }

    /// <summary>Identifies the <see cref="IsExpanded"/> styled property.</summary>
    public static readonly StyledProperty<bool> IsExpandedProperty = AvaloniaProperty.Register<ExpandBanner, bool>(nameof(IsExpanded), false);

    /// <summary>Connects the template parts when the template is applied.</summary>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        Button.ClickEvent.RemoveHandler(OnCloseClick, _closeButton);
        _closeButton = e.NameScope.Find<Button>(PART_CloseButton);
        Button.ClickEvent.AddHandler(OnCloseClick, _closeButton);

        Button.ClickEvent.RemoveHandler(OnToggleExpandClick, _toggleExpandButton);
        _toggleExpandButton = e.NameScope.Find<Button>(PART_ToggleExpandButton);
        Button.ClickEvent.AddHandler(OnToggleExpandClick, _toggleExpandButton);


    }

    /// <summary>Gets or sets whether the banner is expanded.</summary>
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
