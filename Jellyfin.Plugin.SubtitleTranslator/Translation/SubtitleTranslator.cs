using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleTranslator.Subtitles;

namespace Jellyfin.Plugin.SubtitleTranslator.Translation;

/// <summary>
/// Translates full SRT documents in batches, preserving timing and indexes.
/// </summary>
public class SrtTranslator
{
    private readonly ITranslationService _translation;

    /// <summary>
    /// Initializes a new instance of the <see cref="SrtTranslator"/> class.
    /// </summary>
    /// <param name="translation">Underlying translation service.</param>
    public SrtTranslator(ITranslationService translation)
    {
        _translation = translation;
    }

    /// <summary>
    /// Translates SRT text to the target language.
    /// </summary>
    /// <param name="srt">Source SRT content.</param>
    /// <param name="sourceLanguage">Source language code or "auto".</param>
    /// <param name="targetLanguage">Target language code.</param>
    /// <param name="batchSize">Cues per request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Translated SRT content.</returns>
    public async Task<string> TranslateSrtAsync(string srt, string sourceLanguage, string targetLanguage, int batchSize, CancellationToken cancellationToken)
    {
        var cues = SrtSubtitle.Parse(srt);
        if (cues.Count == 0)
        {
            return srt;
        }

        var size = batchSize <= 0 ? 40 : batchSize;
        for (var start = 0; start < cues.Count; start += size)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slice = cues.GetRange(start, System.Math.Min(size, cues.Count - start));
            var texts = new List<string>(slice.Count);
            foreach (var cue in slice)
            {
                texts.Add(cue.Text);
            }

            var translated = await _translation.TranslateAsync(texts, sourceLanguage, targetLanguage, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < slice.Count; i++)
            {
                slice[i].Text = translated[i];
            }
        }

        return SrtSubtitle.Serialize(cues);
    }
}
