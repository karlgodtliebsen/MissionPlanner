using System.Collections;
using System.Collections.Specialized;
using UraniumUI.Material.Controls;

namespace UraniumUI.Material.TabViews;

/// <summary>
/// Extends <see cref="LifecycleTabView"/> with a separate, index-aligned data source for rich tab headers.
/// Static tab contents keep their own binding contexts and lifecycle ownership.
/// </summary>
public class ExtendedTabView : LifecycleTabView
{
    private INotifyCollectionChanged? observedHeaders;
    private bool synchronizingSelection;

    /// <summary>Identifies the header data collection.</summary>
    public static readonly BindableProperty HeaderItemsSourceProperty = BindableProperty.Create(
        nameof(HeaderItemsSource), typeof(IList), typeof(ExtendedTabView), propertyChanged: OnHeaderItemsSourceChanged);

    /// <summary>Identifies the selected header data item.</summary>
    public static readonly BindableProperty SelectedHeaderItemProperty = BindableProperty.Create(
        nameof(SelectedHeaderItem), typeof(object), typeof(ExtendedTabView), defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: OnSelectedHeaderItemChanged);

    /// <summary>Identifies whether an element belongs to the selected extended-tab header.</summary>
    public static readonly BindableProperty IsHeaderSelectedProperty = BindableProperty.CreateAttached(
        "IsHeaderSelected", typeof(bool), typeof(ExtendedTabView), false);

    /// <summary>Gets whether an element belongs to the selected extended-tab header.</summary>
    public static bool GetIsHeaderSelected(BindableObject bindable)
    {
        return (bool)bindable.GetValue(IsHeaderSelectedProperty);
    }

    /// <summary>Sets whether an element belongs to the selected extended-tab header.</summary>
    public static void SetIsHeaderSelected(BindableObject bindable, bool value)
    {
        bindable.SetValue(IsHeaderSelectedProperty, value);
    }

    /// <summary>Gets or sets the index-aligned data objects used by <see cref="TabView.TabHeaderItemTemplate"/>.</summary>
    public IList? HeaderItemsSource
    {
        get => (IList?)GetValue(HeaderItemsSourceProperty);
        set => SetValue(HeaderItemsSourceProperty, value);
    }

    /// <summary>Gets or sets the header data object associated with the selected tab.</summary>
    public object? SelectedHeaderItem
    {
        get => GetValue(SelectedHeaderItemProperty);
        set => SetValue(SelectedHeaderItemProperty, value);
    }

    /// <summary>Initializes a rich-header lifecycle tab view.</summary>
    public ExtendedTabView()
    {
        Loaded += (_, _) => ApplyHeaderContexts();
        SelectedTabChanged += OnSelectedTabChanged;
    }

    private static void OnHeaderItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var control = (ExtendedTabView)bindable;
        control.observedHeaders?.CollectionChanged -= control.OnHeadersChanged;
        control.observedHeaders = newValue as INotifyCollectionChanged;
        control.observedHeaders?.CollectionChanged += control.OnHeadersChanged;
        control.ApplyHeaderContexts();
    }

    private static void OnSelectedHeaderItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var control = (ExtendedTabView)bindable;
        if (control.synchronizingSelection || control.HeaderItemsSource is null)
        {
            return;
        }

        var index = control.HeaderItemsSource.IndexOf(newValue);
        if (index >= 0 && index < control.Tabs.Count)
        {
            control.SelectedTab = control.Tabs[index];
        }
    }

    private void OnHeadersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Dispatch(ApplyHeaderContexts);
    }

    private void OnSelectedTabChanged(object? sender, TabItem selected)
    {
        ApplyHeaderContexts();
        var index = Tabs.IndexOf(selected);
        if (HeaderItemsSource is null || index < 0 || index >= HeaderItemsSource.Count)
        {
            return;
        }

        synchronizingSelection = true;
        SelectedHeaderItem = HeaderItemsSource[index];
        synchronizingSelection = false;
    }

    private void ApplyHeaderContexts()
    {
        if (HeaderItemsSource is null)
        {
            return;
        }

        for (var index = 0; index < Tabs.Count && index < HeaderItemsSource.Count; index++)
        {
            if (Tabs[index].Header is { } header)
            {
                SetHeaderState(header, HeaderItemsSource[index], Tabs[index] == SelectedTab);
            }
        }
    }

    private static void SetHeaderState(View view, object? context, bool isSelected)
    {
        view.BindingContext = context;
        if (GetIsHeaderSelected(view) != isSelected)
        {
            SetIsHeaderSelected(view, isSelected);
        }

        if (view is ExtendedTabHeaderView header && header.IsHeaderSelected != isSelected)
        {
            header.IsHeaderSelected = isSelected;
        }
        switch (view)
        {
            case ContentView contentView when contentView.Content is View content:
                SetHeaderState(content, context, isSelected);
                break;
            case Border border when border.Content is View content:
                SetHeaderState(content, context, isSelected);
                break;
            case Layout layout:
                foreach (var child in layout.Children.OfType<View>())
                {
                    SetHeaderState(child, context, isSelected);
                }

                break;
        }
    }
}
