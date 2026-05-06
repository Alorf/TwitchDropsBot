using System.Diagnostics;
using System.Text.Json;

namespace TwitchDropsBot.Core.Platform.YouTube.Repository;

/// <summary>
/// Runs the bundled Node.js worker (<c>worker.mjs</c>) in a subprocess and
/// communicates with it via stdout/stdin JSON messages.
///
/// The worker uses the <c>youtubei.js</c> library
/// (https://github.com/LuanRT/YouTube.js) to call YouTube's private InnerTube
/// API, so all session setup, request signing, and header management is
/// delegated to the JavaScript library instead of being reimplemented in C#.
///
/// Prerequisites: Node.js ≥ 18 must be available on <c>PATH</c>.
/// </summary>
internal static class YoutubeNodeBridge
{
    // Path to the worker script, relative to the output directory.
    private const string WorkerRelativePath = "NodeWorker/worker.mjs";

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the video ID of the active live stream for the given channel
    /// handle/ID, or <c>null</c> when the channel is not currently live.
    /// </summary>
    /// <param name="handle">YouTube channel handle (e.g. <c>@LinusTechTips</c>)
    /// or UC… channel ID.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<string?> GetLiveVideoIdAsync(
        string handle,
        CancellationToken ct = default)
    {
        var result = await RunWorkerAsync("live", handle, ct);
        if (result.TryGetProperty("videoId", out var videoIdProp) &&
            videoIdProp.ValueKind == JsonValueKind.String)
        {
            return videoIdProp.GetString();
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> when the video with the given ID is currently an
    /// active live stream.
    /// </summary>
    public static async Task<bool> IsVideoLiveAsync(
        string videoId,
        CancellationToken ct = default)
    {
        var result = await RunWorkerAsync("islive", videoId, ct);
        return result.TryGetProperty("isLive", out var isLive) &&
               isLive.ValueKind == JsonValueKind.True;
    }

    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    /// <summary>
    /// Launches <c>node worker.mjs &lt;action&gt; &lt;arg&gt;</c>, waits for it
    /// to exit and returns the parsed JSON output.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the worker exits with a non-zero code or when its output
    /// contains an <c>error</c> field.
    /// </exception>
    private static async Task<JsonElement> RunWorkerAsync(
        string action,
        string arg,
        CancellationToken ct)
    {
        var workerPath = ResolveWorkerPath();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName               = "node",
            Arguments              = $"\"{workerPath}\" {action} \"{arg}\"",
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            WorkingDirectory       = Path.GetDirectoryName(workerPath)!
        };

        process.Start();

        // Read stdout/stderr concurrently so we never deadlock on full pipe buffers.
        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var diagnostics = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            throw new InvalidOperationException(
                $"Node worker exited with code {process.ExitCode}. Details: {diagnostics}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException("Node worker produced no output.");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(stdout);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Node worker output is not valid JSON: {ex.Message}. Output: {stdout}");
        }

        // The worker writes { "error": "..." } on failure instead of exiting non-zero,
        // so that the error message is always captured as structured JSON.
        if (doc.RootElement.TryGetProperty("error", out var errorProp))
        {
            throw new InvalidOperationException(
                $"Node worker reported error: {errorProp.GetString()}");
        }

        // Return root element - note: the JsonDocument is intentionally not
        // disposed here because the caller accesses the element after the call.
        // We transfer ownership via GC; if this becomes hot-path, consider
        // cloning into a struct and disposing immediately.
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Resolves the absolute path to <c>worker.mjs</c> relative to the
    /// running assembly's location (i.e. the application output directory).
    /// </summary>
    private static string ResolveWorkerPath()
    {
        var assemblyDir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location)!;
        var path = Path.Combine(assemblyDir, WorkerRelativePath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Node worker script not found at '{path}'. " +
                "Ensure the project was built so that NodeWorker files are copied to output.",
                path);
        }

        return path;
    }
}
