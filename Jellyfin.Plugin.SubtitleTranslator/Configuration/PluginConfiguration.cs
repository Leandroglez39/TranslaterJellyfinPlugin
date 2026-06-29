using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.SubtitleTranslator.Configuration;

/// <summary>
/// Plugin configuration. Persisted by Jellyfin as XML.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the OpenAI API base URL.
    /// </summary>
    public string ApiUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>
    /// Gets or sets the OpenAI API key. If empty, falls back to the OPENAI_API_KEY environment variable.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OpenAI model used for translation (latest nano model).
    /// </summary>
    public string Model { get; set; } = "gpt-5.4-nano";

    /// <summary>
    /// Gets or sets the target language code (ISO 639-1, e.g. "es").
    /// </summary>
    public string TargetLanguage { get; set; } = "es";

    /// <summary>
    /// Gets or sets the source language code, or "auto" for auto-detection.
    /// </summary>
    public string SourceLanguage { get; set; } = "auto";

    /// <summary>
    /// Gets or sets how many subtitle cues are sent to OpenAI per request.
    /// </summary>
    public int BatchSize { get; set; } = 40;
}
