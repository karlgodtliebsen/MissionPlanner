using System.Collections;
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
    /// Gets or sets the maximum number of characters used by the selection
    /// summary. Additional selections are represented by a <c>+N</c> suffix.
    /// </summary>
    public int MaximumSelectionTextLength
    {
        get => (int)GetValue(MaximumSelectionTextLengthProperty);
        set => SetValue(MaximumSelectionTextLengthProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MaximumSelectionTextLength"/> bindable
    /// property.
    /// </summary>
    public static readonly BindableProperty MaximumSelectionTextLengthProperty =
        BindableProperty.Create(
            nameof(MaximumSelectionTextLength),
            typeof(int),
            typeof(ExtendedMultiplePickerField),
            20,
            validateValue: (_, value) => (int)value > 0,
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
        summaryLabel.Text = FormatSelectionSummary();
        summaryRefreshPending = false;

        base.RefreshChipLayout();
    }

    private string FormatSelectionSummary()
    {
        var selections = SelectedItems?
            .Cast<object?>()
            .Select(item => item?.ToString() ?? string.Empty)
            .ToList();

        if (selections is not { Count: > 0 })
        {
            return EmptySelectionText;
        }

        if (selections.Count == 1)
        {
            return Truncate(selections[0], MaximumSelectionTextLength);
        }

        var visibleItems = new List<string> { selections[0] };

        for (var index = 1; index < selections.Count; index++)
        {
            var candidateItems = visibleItems
                .Append(selections[index])
                .ToList();
            var remaining = selections.Count - candidateItems.Count;
            var candidate = BuildSelectionSummary(candidateItems, remaining);

            if (candidate.Length > MaximumSelectionTextLength)
            {
                break;
            }

            visibleItems.Add(selections[index]);
        }

        var hiddenCount = selections.Count - visibleItems.Count;
        var summary = BuildSelectionSummary(visibleItems, hiddenCount);
        if (summary.Length <= MaximumSelectionTextLength)
        {
            return summary;
        }

        var suffix = $", +{hiddenCount}";
        var firstItemLength =
            Math.Max(1, MaximumSelectionTextLength - suffix.Length);

        return $"{Truncate(visibleItems[0], firstItemLength)}{suffix}";
    }

    private static string BuildSelectionSummary(
        IEnumerable<string> visibleItems,
        int hiddenCount)
    {
        var summary = string.Join(", ", visibleItems);

        return hiddenCount > 0
            ? $"{summary}, +{hiddenCount}"
            : summary;
    }

    private static string Truncate(string text, int maximumLength)
    {
        if (text.Length <= maximumLength)
        {
            return text;
        }

        const string ellipsis = "...";
        if (maximumLength <= ellipsis.Length)
        {
            return ellipsis[..maximumLength];
        }

        return $"{text[..(maximumLength - ellipsis.Length)]}{ellipsis}";
    }

    private static bool IsHandlerUsable(IElementHandler? handler)
    {
        return handler is null || handler.PlatformView is not null;
    }
}
