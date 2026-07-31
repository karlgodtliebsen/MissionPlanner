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
    [InlineData(0, "No options selected")]
    [InlineData(1, "1 option selected")]
    [InlineData(3, "3 options selected")]
    public void SelectedItems_ShouldRenderCompactCountSummary(int count, string expected)
    {
        var control = new ExtendedMultiplePickerField
        {
            SelectedItems = new ObservableCollection<object>(
                Enumerable.Range(1, count)
                    .Select(value => (object)$"Option {value}"))
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

        control.SelectionSummary.ShouldBe("2 options selected");
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
        var control = new ExtendedMultiplePickerField
        {
            Title = title!,
            SelectedItems = new ObservableCollection<object>()
        };

        control.HasValue.ShouldBe(shouldFloat);
        control.SelectionSummary.ShouldBe("No options selected");
    }

    [Fact]
    public void Title_ShouldRemainInsideField_WhenEmptySelectionTextIsEmpty()
    {
        var control = new ExtendedMultiplePickerField
        {
            Title = "Multiple options",
            EmptySelectionText = string.Empty,
            SelectedItems = new ObservableCollection<object>()
        };

        control.HasValue.ShouldBeFalse();
        control.SelectionSummary.ShouldBeEmpty();
    }

    [Fact]
    public void Title_ShouldFloat_WhenSelectionExistsAndEmptySelectionTextIsEmpty()
    {
        var control = new ExtendedMultiplePickerField
        {
            Title = "Multiple options",
            EmptySelectionText = string.Empty,
            SelectedItems = new ObservableCollection<object> { "Logging" }
        };

        control.HasValue.ShouldBeTrue();
        control.SelectionSummary.ShouldBe("1 option selected");
    }

    [Fact]
    public async Task Picker_ShouldUseExtendedDialogService()
    {
        dialogService.DisplayViewAsync(
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
        await dialogService.Received(1).DisplayViewAsync(
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
        control.SelectionSummary.ShouldBe("1 option selected");

        control.IsVisualTreeAvailable = true;
        control.ApplyPendingRefresh();

        control.SelectionSummary.ShouldBe("2 options selected");
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
