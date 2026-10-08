using System.Text.RegularExpressions;

namespace TwitchDropsBot.Web;

/// <summary>
/// Masks credentials that can ride along in exception messages and log lines: user:password in URLs
/// (an HTTP proxy, for one), token-like query values and webhook paths. Reward codes are left alone.
/// </summary>
public static partial class Redactor
{
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        text = UserInfo().Replace(text, "***@");
        text = TokenParameter().Replace(text, "***");
        text = Webhook().Replace(text, "${host}/api/webhooks/***");
        return AuthorizationValue().Replace(text, "${scheme} ***");
    }

    [GeneratedRegex(@"(?<=://)[^/\s:@]+:[^/\s@]+@")]
    private static partial Regex UserInfo();

    [GeneratedRegex(@"(?<=[?&](?:token|sig|signature|access_token|auth|password|client_secret|oauth)=)[^&\s""']+",
        RegexOptions.IgnoreCase)]
    private static partial Regex TokenParameter();

    [GeneratedRegex(@"(?<host>discord(?:app)?\.com)/api/webhooks/[^\s""']+", RegexOptions.IgnoreCase)]
    private static partial Regex Webhook();

    [GeneratedRegex(@"(?<scheme>OAuth|Bearer)\s+[A-Za-z0-9._\-]{10,}", RegexOptions.IgnoreCase)]
    private static partial Regex AuthorizationValue();
}
