using Godot;
using Godot.Achievements.Core;

namespace Godot.Achievements.Examples;

/// <summary>
/// Example usage of the Godot.Achievements.NET system
/// This demonstrates the most common achievement patterns
/// </summary>
public partial class ExampleUsage : Node
{
    public override void _Ready()
    {
        // Connect to achievement signals
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.AchievementUnlocked += OnAchievementUnlocked;
            AchievementManager.Instance.AchievementProgressChanged += OnProgressChanged;
        }
    }

    // Example 1: Simple achievement unlock
    public async void UnlockSimpleAchievement()
    {
        await AchievementManager.Instance.Unlock("first_kill");
    }

    // Example 2: Progressive achievement (kill 100 enemies)
    private int _enemiesKilled = 0;

    public async void OnEnemyKilled()
    {
        _enemiesKilled++;

        // Update progress (0.0 to 1.0)
        float progress = _enemiesKilled / 100f;
        await AchievementManager.Instance.SetProgress("kill_100_enemies", progress);

        // Achievement will auto-unlock when progress reaches 1.0
    }

    // Example 3: Check if achievement is unlocked
    public void CheckAchievementStatus()
    {
        var achievement = AchievementManager.Instance.GetAchievement("first_kill");
        if (achievement != null)
        {
            if (achievement.IsUnlocked)
            {
                GD.Print($"Achievement '{achievement.DisplayName}' was unlocked on {achievement.UnlockedAt}");
            }
            else
            {
                GD.Print($"Achievement '{achievement.DisplayName}' is locked");
                if (achievement.Progress > 0)
                {
                    GD.Print($"Progress: {achievement.Progress * 100}%");
                }
            }
        }
    }

    // Example 4: Display all achievements
    public void DisplayAllAchievements()
    {
        var achievements = AchievementManager.Instance.GetAllAchievements();

        GD.Print("=== All Achievements ===");
        foreach (var achievement in achievements)
        {
            var status = achievement.IsUnlocked ? "✓ UNLOCKED" : "✗ Locked";
            var progressText = achievement.Progress > 0 && achievement.Progress < 1.0f
                ? $" ({achievement.Progress * 100:F1}%)"
                : "";

            GD.Print($"{status} - {achievement.DisplayName}{progressText}");
            GD.Print($"   {achievement.Description}");
        }
    }

    // Example 5: Conditional achievement unlock based on game state
    public async void CheckForSecretAchievement(Player player)
    {
        // Example: Unlock "Speedrunner" if level completed in under 60 seconds
        if (player.LevelCompletionTime < 60f)
        {
            await AchievementManager.Instance.Unlock("speedrunner");
        }

        // Example: Unlock "Perfect" if no damage taken
        if (player.HealthLost == 0)
        {
            await AchievementManager.Instance.Unlock("no_damage");
        }
    }

    // Example 6: Combo/Multi-condition achievement
    private bool _hasCollectedAllCoins = false;
    private bool _hasDefeatedAllBosses = false;
    private bool _hasFoundAllSecrets = false;

    public async void CheckMasterAchievement()
    {
        if (_hasCollectedAllCoins && _hasDefeatedAllBosses && _hasFoundAllSecrets)
        {
            await AchievementManager.Instance.Unlock("completionist");
        }
    }

    // Example 7: Debug menu to test achievements
    public void ShowDebugMenu()
    {
        var achievements = AchievementManager.Instance.GetAllAchievements();

        GD.Print("=== Achievement Debug Menu ===");
        GD.Print("Registered Providers:");

        var providers = AchievementManager.Instance.GetRegisteredProviders();
        foreach (var provider in providers)
        {
            GD.Print($"  - {provider.ProviderName} (Available: {provider.IsAvailable})");
        }

        GD.Print($"\nTotal Achievements: {achievements.Length}");
        var unlocked = achievements.Count(a => a.IsUnlocked);
        GD.Print($"Unlocked: {unlocked}/{achievements.Length} ({(float)unlocked / achievements.Length * 100:F1}%)");
    }

    // Example 8: Listening to achievement events
    private void OnAchievementUnlocked(string achievementId, Achievement achievement)
    {
        GD.Print($"🏆 Achievement Unlocked: {achievement.DisplayName}");
        GD.Print($"   {achievement.Description}");

        // You can add custom logic here, such as:
        // - Playing a sound effect
        // - Updating UI
        // - Saving to analytics
        // - Posting to social media
    }

    private void OnProgressChanged(string achievementId, float progress)
    {
        GD.Print($"Achievement {achievementId} progress: {progress * 100:F1}%");

        // Update UI progress bar
        // UpdateAchievementProgressUI(achievementId, progress);
    }

    // Example 9: Disable/customize toast notifications
    public void CustomizeToastBehavior()
    {
        // Disable default toasts
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.ShowToasts = false;
        }

        // Now handle unlocks with your own UI
        AchievementManager.Instance.AchievementUnlocked += (id, achievement) =>
        {
            // Show your custom notification
            GD.Print($"Custom notification: {achievement.DisplayName}");
        };
    }
}

/// <summary>
/// Placeholder for player class used in examples
/// </summary>
public partial class Player : CharacterBody2D
{
    public float LevelCompletionTime { get; set; }
    public int HealthLost { get; set; }
}
