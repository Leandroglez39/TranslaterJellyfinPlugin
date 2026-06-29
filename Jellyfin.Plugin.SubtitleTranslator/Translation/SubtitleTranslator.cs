using System.Collections.Generic;
using System.Linq;
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
        var batches = new List<List<SrtCue>>();
        for (var start = 0; start < cues.Count; start += size)
        {
            batches.Add(cues.GetRange(start, System.Math.Min(size, cues.Count - start)));
        }

        // Run batches in parallel with bounded concurrency to keep latency low.
        using var gate = new SemaphoreSlim(4);
        var tasks = batches.Select(async slice =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var texts = slice.Select(c => c.Text).ToList();
                var translated = await _translation.TranslateAsync(texts, sourceLanguage, targetLanguage, cancellationToken).ConfigureAwait(false);
                for (var i = 0; i < slice.Count; i++)
                {
                    slice[i].Text = translated[i];
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return SrtSubtitle.Serialize(cues);
    }
}
