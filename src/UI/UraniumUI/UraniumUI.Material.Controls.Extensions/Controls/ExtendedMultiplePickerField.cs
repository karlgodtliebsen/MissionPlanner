using System.Collections;
using System.Globalization;
using UraniumUI.Material.Dialogs;
using MaterialCheckBox = UraniumUI.Material.Controls.CheckBox;

namespace UraniumUI.Material.Controls;

/// <summary>
/// A compact multiple-selection field that uses the resilient extended dialog
/// service and displays selection state as a fixed-height text summary.
/// </summary>
public class ExtendedMultiplePickerField : MultiplePickerField
{
    private readonly Label summaryLabel;
    private readonly IExtendedDialogService extendedDialogService;
    private bool summaryRefreshPending;
    private bool summaryRefreshScheduled;

    /// <summary>
    /// Initializes a compact multiple-selection field.
    /// </summary>
    public ExtendedMultiplePickerField()
    {
        extendedDialogService =
            UraniumServiceProvider.Current
                .GetRequiredService<IExtendedDialogService>();

        summaryLabel = new Label { VerticalOptions = LayoutOptions.Center, VerticalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.TailTruncation };

        var chevron = new Label { Text = "▾", Margin = new Thickness(8, 0, 0, 0), VerticalOptions = LayoutOptions.Center, VerticalTextAlignment = TextAlignment.Center };

        var summaryLayout = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, Padding = new Thickness(8, 4), MinimumHeightRequest = 32 };

        summaryLayout.Add(summaryLabel, 0, 0);
        summaryLayout.Add(chevron, 1, 0);

        MainContentView.Content = summaryLayout;
        BindableLayout.SetItemsSource(chipsHolderLayout, null);
        RefreshChipLayout();
    }

    /// <summary>
    /// Gets or sets the text displayed when no options are selected.
    /// </summary>
    public string EmptySelectionText
    {
        get => (string)GetValue(EmptySelectionTextProperty);
        set => SetValue(EmptySelectionTextProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="EmptySelectionText"/> bindable property.
    /// </summary>
    public static readonly BindableProperty EmptySelectionTextProperty =
        BindableProperty.Create(
            nameof(EmptySelectionText),
            typeof(string),
            typeof(ExtendedMultiplePickerField),
            "No options selected",
            propertyChanged: OnSummaryFormatChanged);

    /// <summary>
    /// Gets or sets the composite format used for one selected option.
    /// Placeholder <c>{0}</c> receives the selection count.
    /// </summary>
    public string SingleSelectionTextFormat
    {
        get => (string)GetValue(SingleSelectionTextFormatProperty);
        set => SetValue(SingleSelectionTextFormatProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SingleSelectionTextFormat"/> bindable property.
    /// </summary>
    public static readonly BindableProperty SingleSelectionTextFormatProperty =
        BindableProperty.Create(nameof(SingleSelectionTextFormat), typeof(string), typeof(ExtendedMultiplePickerField), "{0} option selected", propertyChanged: OnSummaryFormatChanged);

    /// <summary>
    /// Gets or sets the composite format used for multiple selected options.
    /// Placeholder <c>{0}</c> receives the selection count.
    /// </summary>
    public string MultipleSelectionTextFormat
    {
        get => (string)GetValue(MultipleSelectionTextFormatProperty);
        set => SetValue(MultipleSelectionTextFormatProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MultipleSelectionTextFormat"/> bindable property.
    /// </summary>
    public static readonly BindableProperty MultipleSelectionTextFormatProperty =
        BindableProperty.Create(
            nameof(MultipleSelectionTextFormat),
            typeof(string),
            typeof(ExtendedMultiplePickerField),
            "{0} options selected",
            propertyChanged: OnSummaryFormatChanged);

    /// <summary>
    /// Gets the current compact selection summary.
    /// </summary>
    public string SelectionSummary => summaryLabel.Text ?? string.Empty;

    /// <inheritdoc />
    public override bool HasValue =>
        base.HasValue ||
        (!string.IsNullOrWhiteSpace(Title) &&
         !string.IsNullOrWhiteSpace(EmptySelectionText));

    /// <inheritdoc />
    protected override async Task<IEnumerable<object>> DisplayPickerPromptAsync()
    {
        var selectionSource =
            ItemsSource?.Cast<object>() ?? Enumerable.Empty<object>();
        var selectedItems =
            SelectedItems?.Cast<object>() ?? Enumerable.Empty<object>();
        var checkBoxGroup = CreateCheckBoxPromptContent(
            selectionSource,
            selectedItems);

        var accepted = await extendedDialogService.DisplayViewAsync(Title, CreateCheckBoxPromptView(checkBoxGroup), "OK", "Cancel");

        return accepted
            ? checkBoxGroup.Children
                .OfType<MaterialCheckBox>()
                .Where(checkBox => checkBox.IsChecked)
                .Select(checkBox => checkBox.CommandParameter)
                .ToList()
            : null!;
    }

    /// <inheritdoc />
    protected override void OnSelectedItemsSet(
        IList oldValue,
        IList newValue)
    {
        base.OnSelectedItemsSet(oldValue, newValue);

        // The inherited field uses this holder for chips. This presentation is
        // text-only, but retains the holder so base selection subscriptions and
        // dialog behavior remain intact.
        BindableLayout.SetItemsSource(chipsHolderLayout, null);
        RefreshChipLayout();
    }

    /// <inheritdoc />
    protected override void RefreshChipLayout()
    {
        summaryRefreshPending = true;

        if (!CanUpdateNativeSummary())
        {
            return;
        }

        if (Dispatcher?.IsDispatchRequired ?? false)
        {
            if (summaryRefreshScheduled)
            {
                return;
            }

            summaryRefreshScheduled = true;
            if (!Dispatcher.Dispatch(() =>
                {
                    summaryRefreshScheduled = false;
                    ApplyPendingSummaryRefresh();
                }))
            {
                summaryRefreshScheduled = false;
            }

            return;
        }

        ApplyPendingSummaryRefresh();
    }

    /// <inheritdoc />
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        ApplyPendingSummaryRefresh();
    }

    /// <summary>
    /// Gets whether summary properties can safely reach the native view.
    /// </summary>
    protected virtual bool CanUpdateNativeSummary()
    {
        return IsHandlerUsable(Handler) &&
               IsHandlerUsable(summaryLabel?.Handler);
    }

    private static void OnSummaryFormatChanged(
        BindableObject bindable,
        object oldValue,
        object newValue)
    {
        var field = (ExtendedMultiplePickerField)bindable;
        field.RefreshChipLayout();
        field.UpdateState();
    }

    private void ApplyPendingSummaryRefresh()
    {
        if (!summaryRefreshPending || !CanUpdateNativeSummary())
        {
            return;
        }

        BindableLayout.SetItemsSource(chipsHolderLayout, null);
        chipsHolderLayout.Children.Clear();
        summaryLabel.Text = FormatSelectionSummary(
            SelectedItems?.Count ?? 0);
        summaryRefreshPending = false;

        base.RefreshChipLayout();
    }

    private string FormatSelectionSummary(int count)
    {
        if (count == 0)
        {
            return EmptySelectionText;
        }

        var format = count == 1
            ? SingleSelectionTextFormat
            : MultipleSelectionTextFormat;

        try
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                format,
                count);
        }
        catch (FormatException)
        {
            return count == 1
                ? "1 option selected"
                : $"{count} options selected";
        }
    }

    private static bool IsHandlerUsable(IElementHandler? handler)
    {
        return handler is null || handler.PlatformView is not null;
    }
}
