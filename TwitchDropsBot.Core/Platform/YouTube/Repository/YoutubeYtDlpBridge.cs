using System.ComponentModel;
using System.Diagnostics;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

/// <summary>
/// Bridge for invoking <c>yt-dlp</c> in a subprocess to query YouTube live status.
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
        CancellationToken ct = default)
    {
        var probeUrl = BuildChannelLiveUrl(handle);
        var probe = await ProbeAsync(probeUrl, ct);

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
        CancellationToken ct = default)
    {
        var url = $"https://www.youtube.com/watch?v={videoId}";
        var probe = await ProbeAsync(url, ct);

        if (!probe.Success)
        {
            if (LooksLikeNotLiveCondition(probe.Diagnostics))
                return false;

            throw new InvalidOperationException(
                $"yt-dlp failed for video '{videoId}'. Details: {probe.Diagnostics}");
        }

        return string.Equals(probe.LiveStatus, "is_live", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<YtDlpProbeResult> ProbeAsync(string url, CancellationToken ct)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName               = "yt-dlp",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        // yt-dlp can load extractor patches/components from npm via the `ejs`
        // provider; this keeps YouTube extractor behavior aligned with current site changes.
        process.StartInfo.ArgumentList.Add("--remote-components");
        process.StartInfo.ArgumentList.Add("ejs:npm");
        process.StartInfo.ArgumentList.Add("--skip-download");
        process.StartInfo.ArgumentList.Add("--no-warnings");
        process.StartInfo.ArgumentList.Add("--no-playlist");
        process.StartInfo.ArgumentList.Add("--print");
        process.StartInfo.ArgumentList.Add(LiveStatusPrintTemplate);
        process.StartInfo.ArgumentList.Add(url);

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start 'yt-dlp'. Ensure yt-dlp is installed and available in PATH.", ex);
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var diagnostics = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            return new YtDlpProbeResult(false, null, null, diagnostics.Trim());
        }

        var line = stdout
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

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
