using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
    /// Parses SRT content into cues.
    /// </summary>
    /// <param name="content">Raw SRT text.</param>
    /// <returns>Ordered list of cues.</returns>
    public static List<SrtCue> Parse(string content)
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
