using YoutubeDLSharp;
using YoutubeDLSharp.Options;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

/// <summary>
/// Bridge for invoking <c>yt-dlp</c> via YoutubeDLSharp to query YouTube live status.
///
/// Prerequisite: <c>yt-dlp</c> must be installed and available in PATH on host.
/// </summary>
internal static class YoutubeYtDlpBridge
{
    private const string LiveStatusPrintTemplate = "%(id)s\t%(live_status)s";

    /// <summary>
    /// Returns the active live stream watch URL for a handle/channel, or null when not live.
    /// </summary>
    public static async Task<string?> GetActiveLiveStreamAsync(
        string handle,
        string? cookiesFilePath,
        CancellationToken ct = default)
    {
        var probeUrl = BuildChannelLiveUrl(handle);
        var probe = await ProbeAsync(probeUrl, cookiesFilePath, ct);

        if (!probe.Success)
        {
            if (LooksLikeNotLiveCondition(probe.Diagnostics))
                return null;

            throw new InvalidOperationException(
                $"yt-dlp failed for channel '{handle}'. Details: {probe.Diagnostics}");
        }

        if (!string.Equals(probe.LiveStatus, "is_live", StringComparison.OrdinalIgnoreCase))
            return null;

        if (string.IsNullOrWhiteSpace(probe.VideoId))
            return null;

        return $"https://www.youtube.com/watch?v={probe.VideoId}";
    }

    /// <summary>
    /// Returns true only while a video is actively live.
    /// </summary>
    public static async Task<bool> IsVideoLiveAsync(
        string videoId,
        string? cookiesFilePath,
        CancellationToken ct = default)
    {
        var url = $"https://www.youtube.com/watch?v={videoId}";
        var probe = await ProbeAsync(url, cookiesFilePath, ct);

        if (!probe.Success)
        {
            if (LooksLikeNotLiveCondition(probe.Diagnostics))
                return false;

            throw new InvalidOperationException(
                $"yt-dlp failed for video '{videoId}'. Details: {probe.Diagnostics}");
        }

        return string.Equals(probe.LiveStatus, "is_live", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<YtDlpProbeResult> ProbeAsync(
        string url,
        string? cookiesFilePath,
        CancellationToken ct)
    {
        var options = new OptionSet
        {
            SkipDownload = true,
            NoWarnings = true,
            NoPlaylist = true
        };

        // Keep extractor behavior aligned with current site changes.
        options.AddCustomOption<string>("--remote-components", "ejs:npm");
        options.AddCustomOption<string>("--print", LiveStatusPrintTemplate);

        if (!string.IsNullOrWhiteSpace(cookiesFilePath))
            options.Cookies = cookiesFilePath;

        RunResult<string[]> result;
        try
        {
            var ytDlp = new YoutubeDL { YoutubeDLPath = "yt-dlp" };
            result = await ytDlp.RunWithOptions(new[] { url }, options, ct);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start 'yt-dlp'. Ensure yt-dlp is installed and available in PATH.", ex);
        }

        if (!result.Success)
        {
            var diagnostics = string.Join(Environment.NewLine, result.ErrorOutput ?? Array.Empty<string>()).Trim();
            return new YtDlpProbeResult(false, null, null, diagnostics);
        }

        var line = result.Data.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        if (string.IsNullOrWhiteSpace(line))
            return new YtDlpProbeResult(false, null, null, "yt-dlp returned no output.");

        var parts = line.Split('\t');
        var videoId = parts.Length > 0 ? parts[0].Trim() : null;
        var liveStatus = parts.Length > 1 ? parts[1].Trim() : null;

        return new YtDlpProbeResult(true, videoId, liveStatus, string.Empty);
    }

    private static string BuildChannelLiveUrl(string handle)
    {
        if (handle.StartsWith("UC", StringComparison.Ordinal) && handle.Length > 10)
            return $"https://www.youtube.com/channel/{handle}/live";

        if (handle.StartsWith("@", StringComparison.Ordinal))
            return $"https://www.youtube.com/{handle}/live";

        return $"https://www.youtube.com/@{handle}/live";
    }

    private static bool LooksLikeNotLiveCondition(string diagnostics)
    {
        if (string.IsNullOrWhiteSpace(diagnostics))
            return false;

        return diagnostics.Contains("This live event will begin in", StringComparison.OrdinalIgnoreCase) ||
               diagnostics.Contains("does not have a live stream", StringComparison.OrdinalIgnoreCase) ||
               diagnostics.Contains("is not currently live", StringComparison.OrdinalIgnoreCase) ||
               diagnostics.Contains("video is unavailable", StringComparison.OrdinalIgnoreCase) ||
               diagnostics.Contains("This live event has ended", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record YtDlpProbeResult(
        bool Success,
        string? VideoId,
        string? LiveStatus,
        string Diagnostics);
}
