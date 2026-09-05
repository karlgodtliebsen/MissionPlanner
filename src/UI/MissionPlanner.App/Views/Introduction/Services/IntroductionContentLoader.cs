using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Introduction.Models;

namespace MissionPlanner.App.Views.Introduction.Services;

/// <summary>
/// Loads the Introduction content from JSON and Markdown files. 
/// </summary>
/// <param name="logger"></param>
public sealed class IntroductionContentLoader(ILogger<IntroductionContentLoader> logger) : IIntroductionContentLoader
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// Loads the Introduction document asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The loaded <see cref="IntroductionDocument"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown if the Introduction document is invalid.</exception>
    public async Task<IntroductionDocument> LoadAsync(CancellationToken cancellationToken = default)
    {
        Debug.Print("Loading Content");

        var json = await IntroductionAssetLoader
            .ReadTextAsync("Content/Introduction.json", cancellationToken)
            .ConfigureAwait(false);

        var document = JsonSerializer.Deserialize<IntroductionDocument>(json, jsonOptions)
                       ?? throw new InvalidDataException("Introduction.json did not contain a valid document.");

        if (document.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported Introduction schema version {document.SchemaVersion}. Expected version 1.");
        }

        document.Topics = document.Topics
            .OrderBy(static topic => topic.Order)
            .ThenBy(static topic => topic.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Validate(document);

        var loadTasks = document.Topics.Select(topic => LoadMarkdownAsync(topic, cancellationToken));
        await Task.WhenAll(loadTasks).ConfigureAwait(false);
        Debug.Print("Successfully Loaded Content");

        return document;
    }

    private async Task LoadMarkdownAsync(IntroductionTopic topic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(topic.MarkdownFile))
        {
            topic.Markdown = string.Empty;
            return;
        }

        try
        {
            topic.Markdown = await IntroductionAssetLoader
                .ReadTextAsync($"Content/{topic.MarkdownFile}", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.Print("Could not load Introduction markdown file " + topic.MarkdownFile + " for topic " + topic.Id + "\n" + ex.Message);

            logger.LogWarning(
                ex,
                "Could not load Introduction markdown file {MarkdownFile} for topic {TopicId}.",
                topic.MarkdownFile,
                topic.Id);

            topic.Markdown =
                $"> The help text for this section could not be loaded.\n\n" +
                $"Missing content: `{topic.MarkdownFile}`";
        }
    }

    private static void Validate(IntroductionDocument document)
    {
        if (document.Topics.Count == 0)
        {
            throw new InvalidDataException("Introduction.json must define at least one topic.");
        }

        var duplicate = document.Topics
            .Where(static topic => !string.IsNullOrWhiteSpace(topic.Id))
            .GroupBy(static topic => topic.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidDataException($"Duplicate Introduction topic id '{duplicate.Key}'.");
        }

        foreach (var topic in document.Topics)
        {
            if (string.IsNullOrWhiteSpace(topic.Id))
            {
                throw new InvalidDataException("Every Introduction topic requires an id.");
            }

            if (string.IsNullOrWhiteSpace(topic.Title))
            {
                throw new InvalidDataException($"Introduction topic '{topic.Id}' requires a title.");
            }
        }
    }
}

