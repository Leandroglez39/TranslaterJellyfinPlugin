using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleTranslator.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleTranslator.Translation;

/// <summary>
/// Translation service backed by the OpenAI Responses API.
/// </summary>
public class OpenAiTranslationService : ITranslationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiTranslationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiTranslationService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">HTTP client factory.</param>
    /// <param name="logger">Logger.</param>
    public OpenAiTranslationService(IHttpClientFactory httpClientFactory, ILogger<OpenAiTranslationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    private static PluginConfiguration Config => Plugin.Instance!.Configuration;

    private static string ResolveApiKey()
    {
        var key = Config.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            key = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        }

        return key.Trim();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> segments, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken)
    {
        if (segments.Count == 0)
        {
            return Array.Empty<string>();
        }

        var apiKey = ResolveApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        // Send only the raw text segments as a compact JSON array. Indexes/timestamps
        // stay in our internal structures, so we never spend tokens on numbering.
        var input = JsonSerializer.Serialize(segments);

        var sourceText = string.Equals(sourceLanguage, "auto", StringComparison.OrdinalIgnoreCase)
            ? "the source language"
            : sourceLanguage;

        var instructions =
            $"You are a professional subtitle translator. The user message is a JSON array of subtitle lines in {sourceText}. " +
            $"Translate every element to {targetLanguage}, preserving order, count, meaning, tone and on-screen brevity. " +
            "Do not merge or split lines. Respond with the same number of strings in the 'translations' array.";

        var schema = new
        {
            type = "object",
            properties = new { translations = new { type = "array", items = new { type = "string" } } },
            required = new[] { "translations" },
            additionalProperties = false,
        };

        var payload = new
        {
            model = Config.Model,
            instructions,
            input,
            text = new { format = new { type = "json_schema", name = "subtitle_translations", strict = true, schema } },
        };

        using var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(8);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Config.ApiUrl.TrimEnd('/')}/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI request failed ({Status}): {Body}", response.StatusCode, body);
            throw new HttpRequestException($"OpenAI request failed with status {response.StatusCode}.");
        }

        var text = ExtractOutputText(body);
        var translations = ParseTranslations(text, segments.Count);
        return translations;
    }

    private static string ExtractOutputText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (item.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var part in content.EnumerateArray())
                    {
                        if (part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        {
                            return t.GetString() ?? string.Empty;
                        }
                    }
                }
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ParseTranslations(string text, int expected)
    {
        var result = new List<string>(expected);
        if (!string.IsNullOrWhiteSpace(text))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                var element = doc.RootElement;
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            element = prop.Value;
                            break;
                        }
                    }
                }

                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var v in element.EnumerateArray())
                    {
                        result.Add(ElementToString(v));
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to newline split below.
            }
        }

        if (result.Count != expected)
        {
            result = new List<string>(text.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        }

        while (result.Count < expected)
        {
            result.Add(string.Empty);
        }

        return result.GetRange(0, expected);
    }

    private static string ElementToString(JsonElement v)
    {
        if (v.ValueKind == JsonValueKind.String)
        {
            return v.GetString() ?? string.Empty;
        }

        if (v.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in v.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    return prop.Value.GetString() ?? string.Empty;
                }
            }
        }

        return v.ToString();
    }
}