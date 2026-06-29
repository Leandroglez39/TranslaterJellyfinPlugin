using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.SubtitleTranslator.Translation;

/// <summary>
/// Abstraction over a translation engine (OpenAI, etc.).
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Translates a batch of text segments, preserving order and count.
    /// </summary>
    /// <param name="segments">Segments to translate.</param>
    /// <param name="sourceLanguage">Source language code, or "auto".</param>
    /// <param name="targetLanguage">Target language code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Translated segments, same length and order as input.</returns>
    Task<IReadOnlyList<string>> TranslateAsync(IReadOnlyList<string> segments, string sourceLanguage, string targetLanguage, CancellationToken cancellationToken);
}

