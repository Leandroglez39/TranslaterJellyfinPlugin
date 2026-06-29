using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.SubtitleTranslator.Translation;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SubtitleTranslator.Api;

/// <summary>Subtitle stream candidate for translation.</summary>
public class CandidateStream
{
    /// <summary>Gets or sets the stream index.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the display label.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>An item with translatable embedded subtitle tracks.</summary>
public class CandidateItem
{
    /// <summary>Gets or sets the item id.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets the item name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the embedded text subtitle streams.</summary>
    public IReadOnlyList<CandidateStream> Streams { get; set; } = Array.Empty<CandidateStream>();
}

/// <summary>Translate request body.</summary>
public class TranslateRequest
{
    /// <summary>Gets or sets the item id.</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the subtitle stream index.</summary>
    public int StreamIndex { get; set; }

    /// <summary>Gets or sets the target language (overrides config when set).</summary>
    public string? TargetLanguage { get; set; }
}

/// <summary>
/// REST API used by the plugin config page to translate embedded subtitles on demand.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("SubtitleTranslator")]
public class TranslationController : ControllerBase
{
    private readonly ILogger<TranslationController> _logger;
    private readonly ILibraryManager _libraryManager;
    private readonly ISubtitleEncoder _subtitleEncoder;
    private readonly SrtTranslator _translator;

    /// <summary>Initializes a new instance of the <see cref="TranslationController"/> class.</summary>
    /// <param name="logger">Logger.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="subtitleEncoder">Subtitle encoder.</param>
    /// <param name="translator">SRT translator.</param>
    public TranslationController(
        ILogger<TranslationController> logger,
        ILibraryManager libraryManager,
        ISubtitleEncoder subtitleEncoder,
        SrtTranslator translator)
    {
        _logger = logger;
        _libraryManager = libraryManager;
        _subtitleEncoder = subtitleEncoder;
        _translator = translator;
    }

    /// <summary>Lists items that have embedded text subtitle tracks.</summary>
    /// <param name="search">Optional name filter.</param>
    /// <returns>Candidate items.</returns>
    [HttpGet("Candidates")]
    public ActionResult<IEnumerable<CandidateItem>> GetCandidates([FromQuery] string? search)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Episode },
            Recursive = true,
            SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search,
        };

        var items = _libraryManager.GetItemList(query).OfType<Video>().Take(200);
        var result = new List<CandidateItem>();

        foreach (var video in items)
        {
            var streams = video.GetMediaStreams()
                .Where(s => s.Type == MediaStreamType.Subtitle && s.IsTextSubtitleStream)
                .Select(s => new CandidateStream
                {
                    Index = s.Index,
                    Label = $"#{s.Index} {s.DisplayTitle ?? s.Language ?? "subtitle"}",
                })
                .ToList();

            if (streams.Count > 0)
            {
                result.Add(new CandidateItem { Id = video.Id.ToString("N"), Name = video.Name, Streams = streams });
            }
        }

        return Ok(result);
    }

    /// <summary>Extracts the chosen embedded subtitle, translates it and saves a sidecar SRT.</summary>
    /// <param name="request">Translate request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Status.</returns>
    [HttpPost("Translate")]
    public async Task<ActionResult> Translate([FromBody] TranslateRequest request, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance!.Configuration;
        var target = string.IsNullOrWhiteSpace(request.TargetLanguage) ? config.TargetLanguage : request.TargetLanguage!;

        if (_libraryManager.GetItemById(request.ItemId) is not Video video)
        {
            return NotFound("Item not found.");
        }

        await using var source = await _subtitleEncoder
            .GetSubtitles(video, video.Id.ToString("N"), request.StreamIndex, "srt", 0, 0, false, cancellationToken)
            .ConfigureAwait(false);

        using var reader = new StreamReader(source, Encoding.UTF8);
        var srt = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        var translated = await _translator
            .TranslateSrtAsync(srt, config.SourceLanguage, target, config.BatchSize, cancellationToken)
            .ConfigureAwait(false);

        var dir = Path.GetDirectoryName(video.Path) ?? throw new InvalidOperationException("Item has no path.");
        var baseName = Path.GetFileNameWithoutExtension(video.Path);
        var outPath = Path.Combine(dir, string.Format(CultureInfo.InvariantCulture, "{0}.{1}.srt", baseName, target));

        await System.IO.File.WriteAllTextAsync(outPath, translated, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Saved translated subtitle: {Path}", outPath);

        return Ok(new { saved = outPath });
    }
}
