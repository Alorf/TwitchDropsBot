using System.Text.Json;

namespace TwitchDropsBot.Core.Platform.Youtube.Utils;

public static class YoutubeCookieParser
{
    private const int NetscapeFieldCount = 7;
    private const int NetscapeDomainIndex = 0;
    private const int NetscapePathIndex = 2;
    private const int NetscapeNameIndex = 5;
    private const int NetscapeValueIndex = 6;

    public static string NormalizeForStorage(string? input)
    {
        var cookies = ParseCookies(input);
        if (cookies.Count == 0)
            return string.Empty;

        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var cookie in cookies)
        {
            merged[cookie.Name] = cookie.Value;
        }

        return string.Join("; ", merged.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    public static IReadOnlyList<ParsedCookie> ParseCookies(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        var trimmedInput = input.Trim();

        if (TryParseJson(trimmedInput, out var jsonCookies))
            return jsonCookies;

        var allLines = trimmedInput
            .Split(Environment.NewLine, StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (TryParseNetscape(allLines, out var netscapeCookies))
            return netscapeCookies;

        if (allLines.Count <= 1)
            return ParseCookieHeader(trimmedInput);

        var lineCookies = new List<ParsedCookie>();
        foreach (var line in allLines)
        {
            lineCookies.AddRange(ParseSetCookieLine(line));
        }

        return lineCookies;
    }

    private static bool TryParseJson(string input, out List<ParsedCookie> cookies)
    {
        cookies = [];

        if (!input.StartsWith('{') && !input.StartsWith('['))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(input);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                    TryAddFromJsonObject(item, cookies);
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("cookies", out var cookieArray) &&
                    cookieArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in cookieArray.EnumerateArray())
                        TryAddFromJsonObject(item, cookies);
                }
                else
                {
                    TryAddFromJsonObject(root, cookies);
                }
            }

            return cookies.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseNetscape(IReadOnlyCollection<string> lines, out List<ParsedCookie> cookies)
    {
        cookies = [];
        var seenNetscapeLine = false;

        foreach (var line in lines)
        {
            if (line.StartsWith('#'))
                continue;

            var parts = line.Split('\t', StringSplitOptions.None);
            if (parts.Length < NetscapeFieldCount)
                continue;

            seenNetscapeLine = true;
            var domain = string.IsNullOrWhiteSpace(parts[NetscapeDomainIndex]) ? ".youtube.com" : parts[NetscapeDomainIndex].Trim();
            var path = string.IsNullOrWhiteSpace(parts[NetscapePathIndex]) ? "/" : parts[NetscapePathIndex].Trim();
            var name = parts[NetscapeNameIndex].Trim();
            var value = parts[NetscapeValueIndex].Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            cookies.Add(new ParsedCookie(name, value, domain, path));
        }

        return seenNetscapeLine && cookies.Count > 0;
    }

    private static List<ParsedCookie> ParseCookieHeader(string input)
    {
        var result = new List<ParsedCookie>();
        var cleaned = input.Trim();

        if (cleaned.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned["Cookie:".Length..].Trim();

        foreach (var segment in cleaned.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = segment.Trim();
            var equalIndex = part.IndexOf('=');
            if (equalIndex <= 0)
                continue;

            var name = part[..equalIndex].Trim();
            var value = part[(equalIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            result.Add(new ParsedCookie(name, value, ".youtube.com", "/"));
        }

        return result;
    }

    private static List<ParsedCookie> ParseSetCookieLine(string line)
    {
        var result = new List<ParsedCookie>();
        var cleaned = line.Trim();
        if (cleaned.StartsWith("Set-Cookie:", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned["Set-Cookie:".Length..].Trim();

        var parts = cleaned.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .ToList();

        if (parts.Count == 0)
            return result;

        var first = parts[0];
        var equalIndex = first.IndexOf('=');
        if (equalIndex <= 0)
            return result;

        var name = first[..equalIndex].Trim();
        var value = first[(equalIndex + 1)..].Trim();

        if (string.IsNullOrWhiteSpace(name))
            return result;

        var domain = ".youtube.com";
        var path = "/";

        foreach (var attribute in parts.Skip(1))
        {
            if (attribute.StartsWith("Domain=", StringComparison.OrdinalIgnoreCase))
            {
                domain = attribute["Domain=".Length..].Trim();
            }
            else if (attribute.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
            {
                path = attribute["Path=".Length..].Trim();
            }
        }

        result.Add(new ParsedCookie(name, value, domain, path));
        return result;
    }

    private static void TryAddFromJsonObject(JsonElement element, ICollection<ParsedCookie> output)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        if (!element.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            return;

        if (!element.TryGetProperty("value", out var valueEl))
            return;

        var name = nameEl.GetString()?.Trim();
        var value = valueEl.ValueKind switch
        {
            JsonValueKind.String => valueEl.GetString(),
            JsonValueKind.Number => valueEl.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => valueEl.GetRawText()
        };
        if (string.IsNullOrWhiteSpace(name))
            return;

        var domain = ".youtube.com";
        if (element.TryGetProperty("domain", out var domainEl) && domainEl.ValueKind == JsonValueKind.String)
        {
            var tempDomain = domainEl.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(tempDomain))
                domain = tempDomain;
        }

        var path = "/";
        if (element.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
        {
            var tempPath = pathEl.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(tempPath))
                path = tempPath;
        }

        output.Add(new ParsedCookie(name, value ?? string.Empty, domain, path));
    }

    public readonly record struct ParsedCookie(string Name, string Value, string Domain, string Path);
}
