#nullable enable

using System.Collections.ObjectModel;
using NSubstitute;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Dialogs;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using Xunit;

namespace UraniumUI.Material.Tests;

public class ExtendedMultiplePickerField_Test
{
    private readonly IExtendedDialogService dialogService;

    public ExtendedMultiplePickerField_Test()
    {
        dialogService = Substitute.For<IExtendedDialogService>();
        ApplicationExtensions.CreateAndSetMockApplication(builder => builder.Services.AddSingleton(dialogService));
    }

    [Theory]
    [InlineData(new string[0], "No options selected")]
    [InlineData(new[] { "Logging" }, "Logging")]
    [InlineData(new[] { "Logging", "Gps" }, "Logging, Gps")]
    [InlineData(new[] { "Logging", "Gps", "A", "B", "C" }, "Logging, Gps, A, +2")]
    [InlineData(new[] { "Logging", "LongLongLong" }, "Logging, +1")]
    [InlineData(new[] { "Logging", "LongLongLong", "Gps" }, "Logging, +2")]
    public void SelectedItems_ShouldRenderCompactSelectionSummary(
        string[] selections,
        string expected)
    {
        var control = new ExtendedMultiplePickerField
        {
            SelectedItems = new ObservableCollection<object>(
                selections.Cast<object>())
        };

        control.SelectionSummary.ShouldBe(expected);
        control.MainContentView.Content
            .ShouldBeOfType<Grid>()
            .Children
            .OfType<Chip>()
            .ShouldBeEmpty();
    }

    [Fact]
    public void SelectionChanges_ShouldRefreshSummary()
    {
        var selectedItems =
            new ObservableCollection<object> { "Logging" };
        var control = new ExtendedMultiplePickerField { SelectedItems = selectedItems };

        selectedItems.Add("GPS");

        control.SelectionSummary.ShouldBe("Logging, GPS");
    }

    [Theory]
    [InlineData(new[] { "ExtremelyLongOption" }, "Extremely...")]
    [InlineData(new[] { "ExtremelyLongOption", "GPS" }, "Extre..., +1")]
    public void SelectionSummary_ShouldRespectConfiguredMaximumLength(
        string[] selections,
        string expected)
    {
        var control = new ExtendedMultiplePickerField
        {
            MaximumSelectionTextLength = 12,
            SelectedItems = new ObservableCollection<object>(
                selections.Cast<object>())
        };

        control.SelectionSummary.ShouldBe(expected);
        control.SelectionSummary.Length.ShouldBeLessThanOrEqualTo(12);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("Flags", true)]
    public void Title_ShouldFloatOnlyWhenItHasText(
        string? title,
        bool shouldFloat)
    {
        var control = new ExtendedMultiplePickerField { Title = title!, SelectedItems = new ObservableCollection<object>() };

        control.HasValue.ShouldBe(shouldFloat);
        control.SelectionSummary.ShouldBe("No options selected");
    }

    [Fact]
    public void Title_ShouldRemainInsideField_WhenEmptySelectionTextIsEmpty()
    {
        var control = new ExtendedMultiplePickerField { Title = "Multiple options", EmptySelectionText = string.Empty, SelectedItems = new ObservableCollection<object>() };

        control.HasValue.ShouldBeFalse();
        control.SelectionSummary.ShouldBeEmpty();
    }

    [Fact]
    public void Title_ShouldFloat_WhenSelectionExistsAndEmptySelectionTextIsEmpty()
    {
        var control = new ExtendedMultiplePickerField { Title = "Multiple options", EmptySelectionText = string.Empty, SelectedItems = new ObservableCollection<object> { "Logging" } };

        control.HasValue.ShouldBeTrue();
        control.SelectionSummary.ShouldBe("Logging");
    }

    [Fact]
    public async Task Picker_ShouldUseExtendedDialogService()
    {
        dialogService.DisplayViewExtendedAsync(
                Arg.Any<string>(),
                Arg.Any<View>(),
                "OK",
                "Cancel")
            .Returns(true);
        var selectedItems =
            new ObservableCollection<object> { "Logging" };
        var control = new TestableExtendedMultiplePickerField { Title = "Flags", ItemsSource = new[] { "Logging", "GPS" }, SelectedItems = selectedItems };

        var result = await control.ShowPickerAsync();

        result.ShouldBe(["Logging"]);
        await dialogService.Received(1).DisplayViewExtendedAsync(
            "Flags",
            Arg.Any<View>(),
            "OK",
            "Cancel");
    }

    [Fact]
    public void SelectionChangeDuringNativeTeardown_ShouldDeferSummaryRefresh()
    {
        var selectedItems =
            new ObservableCollection<object> { "Logging" };
        var control = new TestableExtendedMultiplePickerField { IsVisualTreeAvailable = true, SelectedItems = selectedItems };
        control.IsVisualTreeAvailable = false;

        Should.NotThrow(() => selectedItems.Add("GPS"));
        control.SelectionSummary.ShouldBe("Logging");

        control.IsVisualTreeAvailable = true;
        control.ApplyPendingRefresh();

        control.SelectionSummary.ShouldBe("Logging, GPS");
    }

    private sealed class TestableExtendedMultiplePickerField
        : ExtendedMultiplePickerField
    {
        public bool IsVisualTreeAvailable { get; set; } = true;

        public Task<IEnumerable<object>> ShowPickerAsync()
        {
            return DisplayPickerPromptAsync();
        }

        public void ApplyPendingRefresh()
        {
            RefreshChipLayout();
        }

        protected override bool CanUpdateNativeSummary()
        {
            return IsVisualTreeAvailable;
        }
    }
}
