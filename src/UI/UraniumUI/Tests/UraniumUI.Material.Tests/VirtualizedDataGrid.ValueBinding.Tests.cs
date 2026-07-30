#nullable enable

using System.Globalization;
using Shouldly;
using UraniumUI.Material.Controls;
using UraniumUI.Material.Tests.UraniumUI.Core.Tests;
using UraniumUI.Material.VirtualizedDataGrid.Controls;
using Xunit;

namespace UraniumUI.Material.Tests;

public class VirtualizedDataGrid_ValueBinding_Tests
{
    public VirtualizedDataGrid_ValueBinding_Tests()
    {
        ApplicationExtensions.CreateAndSetMockApplication();
    }

    [Theory]
    [InlineData(null, "True")]
    [InlineData("yes", "yes")]
    public void BooleanColumn_ShouldRenderRawOrConvertedText(
        string? convertedText,
        string expectedText)
    {
        var binding = new Binding(nameof(Row.IsDone));

        if (convertedText is not null)
        {
            binding.Converter = new BooleanTextConverter(convertedText, "no");
        }

        var grid = new TestableVirtualizedDataGrid
        {
            Columns =
            [
                new DataGridColumn
                {
                    Title = "Done",
                    ValueBinding = binding
                }
            ]
        };

        var row = grid.CreateRow();
        row.BindingContext = new Row(true);

        FindLabels(row)
            .ShouldContain(label => label.Text == expectedText);
    }

    private static IEnumerable<Label> FindLabels(Element element)
    {
        if (element is Label label)
        {
            yield return label;
        }

        if (element is ContentView { Content: Element content })
        {
            foreach (var descendant in FindLabels(content))
            {
                yield return descendant;
            }
        }

        if (element is Layout layout)
        {
            foreach (var child in layout.Children.OfType<Element>())
            {
                foreach (var descendant in FindLabels(child))
                {
                    yield return descendant;
                }
            }
        }
    }

    private sealed class TestableVirtualizedDataGrid : VirtualizedDataGrid.Controls.VirtualizedDataGrid
    {
        public View CreateRow()
        {
            return (View)RowsView.ItemTemplate.CreateContent();
        }
    }

    private sealed record Row(bool IsDone);

    private sealed class BooleanTextConverter(
        string trueText,
        string falseText) : IValueConverter
    {
        public object Convert(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture)
        {
            return value is true ? trueText : falseText;
        }

        public object ConvertBack(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
