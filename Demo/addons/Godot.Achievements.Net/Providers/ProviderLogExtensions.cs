using Godot.Achievements.Core;

namespace Godot.Achievements.Providers;

/// <summary>
/// Extension methods for provider logging with consistent [Achievements] [ProviderName] format
/// </summary>
public static class ProviderLogExtensions
{
    public static void Log(this IAchievementProvider provider, string message)
    {
        AchievementLogger.Log(provider.ProviderName, message);
    }

    public static void LogWarning(this IAchievementProvider provider, string message)
    {
        AchievementLogger.Warning(provider.ProviderName, message);
    }

    public static void LogError(this IAchievementProvider provider, string message)
    {
        AchievementLogger.Error(provider.ProviderName, message);
    }
}
