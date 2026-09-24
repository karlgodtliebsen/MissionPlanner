using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Styling;
using LiveMarkdown.Avalonia;
using Markdig;
using Markdig.Parsers.Inlines;
using MissionPlanner.App.Presentation.Documents;

namespace MissionPlanner.App.Controls;

/// <summary>Selectable, read-only application documents with no executable links or images.</summary>
public partial class InformationDocumentView : UserControl
{
    private static readonly MarkdownPipeline pipeline = CreatePipeline();
    private string? source;

    /// <summary>The application document to display.</summary>
    public static readonly StyledProperty<UserDocument?> DocumentProperty =
        AvaloniaProperty.Register<InformationDocumentView, UserDocument?>(nameof(Document));

    /// <summary>Whether document copy actions are visible.</summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<InformationDocumentView, bool>(nameof(ShowToolbar), true);

    /// <summary>Gets or sets the document; identical source preserves the current selection.</summary>
    public UserDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    /// <summary>Gets or sets copy toolbar visibility.</summary>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <summary>Creates the renderer and its explicit selection scope.</summary>
    public InformationDocumentView()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => UpdateTheme();
        AttachedToVisualTree += (_, _) => UpdateTheme();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DocumentProperty && Renderer is not null)
        {
            var markdown = Document?.Markdown ?? string.Empty;
            if (source == markdown)
            {
                return;
            }
            source = markdown;
            CopyFeedback.Text = string.Empty;
            Renderer.DocumentUpdate = new MarkdownDocumentUpdate.Full(Markdown.Parse(markdown, pipeline));
        }
        else if (change.Property == ShowToolbarProperty && Toolbar is not null)
        {
            Toolbar.IsVisible = ShowToolbar;
        }
    }

    private static MarkdownPipeline CreatePipeline()
    {
        var builder = new MarkdownPipelineBuilder().UsePipeTables().DisableHtml();
        // Remove parsers, not just click handlers: images must never initiate I/O.
        builder.InlineParsers.RemoveAll(parser => parser is LinkInlineParser or AutolinkInlineParser);
        return builder.Build();
    }

    private void UpdateTheme()
    {
        foreach (var (target, theme) in new[]
        {
            ("ForegroundColor", "SemiColorText0"), ("CodeInlineColor", "SemiColorText0"),
            ("BorderColor", "SemiColorBorder"), ("BorderBrush", "SemiColorBorder"),
            ("QuoteBorderColor", "SemiColorPrimary"), ("CardBackgroundColor", "SemiColorBackground2"),
            ("SecondaryCardBackgroundColor", "SemiColorFill0")
        })
        {
            if (this.TryFindResource(theme, ActualThemeVariant, out var value))
            {
                Renderer.Resources[target] = value;
            }
        }
        Renderer.CodeBlockColorTheme = ActualThemeVariant == ThemeVariant.Dark
            ? TextMateSharp.Grammars.ThemeName.DarkPlus : TextMateSharp.Grammars.ThemeName.LightPlus;
    }

    private async void CopyAllClicked(object? sender, RoutedEventArgs args)
    {
        var text = string.Join(Environment.NewLine,
            Renderer.RenderedTextProjection?.Buffers.Select(buffer => buffer.Text.ToString()) ?? []);
        await CopyAsync(text);
    }

    private async void CopyMarkdownClicked(object? sender, RoutedEventArgs args)
    {
        await CopyAsync(Document?.Markdown ?? string.Empty);
    }

    private async Task CopyAsync(string text)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                CopyFeedback.Text = "Clipboard unavailable";
                return;
            }
            await clipboard.SetTextAsync(text);
            CopyFeedback.Text = "Copied";
        }
        catch (Exception)
        {
            CopyFeedback.Text = "Could not copy";
        }
    }
}
