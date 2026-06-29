using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SubtitlesParser.Classes.Parsers;

namespace Jellyfin.Plugin.SubtitleTranslator.Subtitles;

/// <summary>
/// A single SRT cue: index, time range and one or more text lines.
/// </summary>
public sealed class SrtCue
{
    /// <summary>Gets or sets the 1-based cue index.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the raw "start --&gt; end" timing line.</summary>
    public string Timing { get; set; } = string.Empty;

    /// <summary>Gets or sets the cue text (may contain newlines).</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Minimal SRT parser/serializer that preserves indexes and timestamps.
/// </summary>
public static class SrtSubtitle
{
    private static readonly Regex IndexRegex = new(@"^\d+$", RegexOptions.Compiled);

    /// <summary>
    /// Parses subtitle content (SRT/VTT/ASS) into cues via SubtitlesParser, with a
    /// raw-SRT fallback. Indexes and timestamps stay in these internal structures.
    /// </summary>
    /// <param name="content">Raw subtitle text.</param>
    /// <returns>Ordered list of cues.</returns>
    public static List<SrtCue> Parse(string content)
    {
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var items = new SubParser().ParseStream(stream, Encoding.UTF8);
            if (items.Count > 0)
            {
                return items.Select((item, i) => new SrtCue
                {
                    Index = i + 1,
                    Timing = $"{FormatTime(item.StartTime)} --> {FormatTime(item.EndTime)}",
                    Text = string.Join("\n", item.PlaintextLines),
                }).ToList();
            }
        }
        catch (ArgumentException)
        {
            // Unknown/unsupported format: fall back to the raw SRT parser.
        }

        return ParseRaw(content);
    }

    private static string FormatTime(int milliseconds)
    {
        var ts = System.TimeSpan.FromMilliseconds(milliseconds);
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00},{3:000}", (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
    }

    private static List<SrtCue> ParseRaw(string content)
    {
        var cues = new List<SrtCue>();
        var blocks = content.Replace("\r\n", "\n").Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var lines = block.Split('\n').Where(l => l.Length > 0).ToList();
            if (lines.Count < 2)
            {
                continue;
            }

            var offset = IndexRegex.IsMatch(lines[0].Trim()) ? 1 : 0;
            if (offset == 0 || lines.Count < offset + 2)
            {
                continue;
            }

            cues.Add(new SrtCue
            {
                Index = int.Parse(lines[0].Trim(), CultureInfo.InvariantCulture),
                Timing = lines[offset],
                Text = string.Join("\n", lines.Skip(offset + 1)),
            });
        }

        return cues;
    }

    /// <summary>
    /// Serializes cues back to SRT text.
    /// </summary>
    /// <param name="cues">Cues to serialize.</param>
    /// <returns>SRT formatted string.</returns>
    public static string Serialize(IEnumerable<SrtCue> cues)
    {
        var sb = new StringBuilder();
        foreach (var cue in cues)
        {
            sb.Append(cue.Index.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(cue.Timing).Append('\n');
            sb.Append(cue.Text).Append('\n').Append('\n');
        }

        return sb.ToString();
    }
}
