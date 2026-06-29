using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleTranslator.Translation;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleTranslator.Providers;

/// <summary>
/// Extracts an embedded subtitle track from the video, translates it with OpenAI,
/// and offers it as a new selectable track.
/// </summary>
public class TranslateSubtitleProvider : ISubtitleProvider
{
    private readonly ILogger<TranslateSubtitleProvider> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly ISubtitleEncoder _subtitleEncoder;
    private readonly SrtTranslator _translator;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslateSubtitleProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="subtitleEncoder">Subtitle encoder used to extract embedded tracks.</param>
    /// <param name="translator">SRT translator.</param>
    public TranslateSubtitleProvider(
        ILogger<TranslateSubtitleProvider> logger,
        ILibraryManager libraryManager,
        ISubtitleEncoder subtitleEncoder,
        SrtTranslator translator)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _subtitleEncoder = subtitleEncoder;
        _translator = translator;
    }

    /// <inheritdoc />
    public string Name => "Subtitle Translator (OpenAI)";

    /// <inheritdoc />
    public IEnumerable<VideoContentType> SupportedMediaTypes => new[] { VideoContentType.Movie, VideoContentType.Episode };

    /// <inheritdoc />
    public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request, CancellationToken cancellationToken)
    {
        var target = Plugin.Instance!.Configuration.TargetLanguage;
        var results = new List<RemoteSubtitleInfo>();

        if (request.ContentType == VideoContentType.Movie || request.ContentType == VideoContentType.Episode)
        {
            var item = _libraryManager.FindByPath(request.MediaPath, false);
            if (item is Video video)
            {
                foreach (var stream in video.GetMediaStreams().Where(s => s.Type == MediaStreamType.Subtitle && s.IsTextSubtitleStream))
                {
                    results.Add(new RemoteSubtitleInfo
                    {
                        Id = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", item.Id, stream.Index),
                        ProviderName = Name,
                        Name = $"Translated to {target} (from track #{stream.Index})",
                        Format = "srt",
                        ThreeLetterISOLanguageName = target,
                    });
                }
            }
        }

        return Task.FromResult<IEnumerable<RemoteSubtitleInfo>>(results);
    }

    /// <inheritdoc />
    public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
    {
        var parts = id.Split(':');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var itemId) || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var streamIndex))
        {
            throw new ArgumentException("Invalid subtitle id.", nameof(id));
        }

        var item = _libraryManager.GetItemById(itemId) as Video
            ?? throw new ArgumentException("Media item not found.", nameof(id));

        var config = Plugin.Instance!.Configuration;

        await using var source = await _subtitleEncoder
            .GetSubtitles(item, item.Id.ToString("N"), streamIndex, "srt", 0, 0, false, cancellationToken)
            .ConfigureAwait(false);

        using var reader = new StreamReader(source, Encoding.UTF8);
        var srt = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var translated = await _translator
            .TranslateSrtAsync(srt, config.SourceLanguage, config.TargetLanguage, config.BatchSize, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation("Translated subtitle track {Index} of {Item} to {Lang}", streamIndex, item.Name, config.TargetLanguage);

        return new SubtitleResponse
        {
            Format = "srt",
            Language = config.TargetLanguage,
            Stream = new MemoryStream(Encoding.UTF8.GetBytes(translated)),
        };
    }
}

