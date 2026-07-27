using System.Reflection;

namespace TwitchDropsBot.Core
{
    public static class VersionHelper
    {
        public static string GetCoreVersion()
        {
            var assembly = typeof(VersionHelper).Assembly;
            var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            return infoVersion ?? assembly.GetName().Version?.ToString(3) ?? "Unknown";
        }

        public static string GetUIVersion(Assembly uiAssembly)
        {
            var infoVersion = uiAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            
            return infoVersion ?? uiAssembly.GetName().Version?.ToString(3) ?? "Unknown";
        }
    }
}
