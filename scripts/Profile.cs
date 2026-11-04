using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace HackerOrSoldier;

// Persistent player profile: coins, campaign progress and settings.
// Saved to user://profile.json.
public class Profile
{
    public const string DefaultFileName = "profile.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static Profile Current { get; set; } = new();

    public int Coins { get; set; } = 100;   // README: default 100 coins
    public int LevelIndex { get; set; }
    public string Difficulty { get; set; } = "medium";
    public int MusicVolume { get; set; } = 5;   // 0-10; applied once audio exists
    public int SoundVolume { get; set; } = 5;
    public bool HasHackingTool { get; set; }
    public Dictionary<string, long> KeyBinds { get; set; } = new();

    // README difficulty: low +1 agent life / -1 enemy life, hard the reverse.
    [JsonIgnore] public int AgentLivesBonus => Difficulty == "low" ? 1 : Difficulty == "hard" ? -1 : 0;
    [JsonIgnore] public int EnemyLivesDelta => Difficulty == "low" ? -1 : Difficulty == "hard" ? 1 : 0;
    [JsonIgnore] public bool IsLowDifficulty => Difficulty == "low";

    public void Save(string fileName = DefaultFileName)
    {
        LevelStore.WriteAtomic($"user://{fileName}", JsonSerializer.Serialize(this, JsonOptions));
    }

    public static Profile Load(string fileName = DefaultFileName)
    {
        using var file = FileAccess.Open($"user://{fileName}", FileAccess.ModeFlags.Read);
        if (file == null)
            return new Profile();
        return JsonSerializer.Deserialize<Profile>(file.GetAsText(), JsonOptions) ?? new Profile();
    }

    // Replaces the keyboard events of rebound actions, keeping gamepad ones.
    public void ApplyKeyBinds()
    {
        foreach (var (action, keycode) in KeyBinds)
        {
            if (!InputMap.HasAction(action))
                continue;
            foreach (var existing in InputMap.ActionGetEvents(action))
                if (existing is InputEventKey)
                    InputMap.ActionEraseEvent(action, existing);
            InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = (Key)keycode });
        }
    }
}

// State carried across the levels of one campaign run (README: ammo and
// coins transfer between levels).
public class RunState
{
    public int Lives { get; set; }
    public int Ammo { get; set; }
    public int Briefcases { get; set; }

    public static RunState NewRun() => new()
    {
        Lives = 3 + Profile.Current.AgentLivesBonus,
        Ammo = 10,
    };
}

public static class Shop
{
    public const string BriefcaseItem = "briefcase";
    public const string HackingTool = "hacking_tool";
    public const string ExtraLife = "extra_life";
    public const string AmmoPack = "ammo_pack";

    public record ShopItem(string Id, string Name, int Price);

    // README shop, minus the weapon entries (weapon switching is not built yet).
    public static readonly ShopItem[] Items =
    {
        new(BriefcaseItem, "Briefcase", 20),
        new(HackingTool, "Hacking tool", 30),
        new(ExtraLife, "Extra life", 50),
        new(AmmoPack, "10 ammo pack", 5),
    };

    public static bool TryBuy(string id, RunState run)
    {
        ShopItem item = null;
        foreach (var candidate in Items)
            if (candidate.Id == id)
                item = candidate;
        if (item == null || Profile.Current.Coins < item.Price)
            return false;
        if (id == HackingTool && Profile.Current.HasHackingTool)
            return false;   // permanent upgrade, only sold once

        Profile.Current.Coins -= item.Price;
        switch (id)
        {
            case BriefcaseItem:
                run.Briefcases++;
                break;
            case HackingTool:
                Profile.Current.HasHackingTool = true;
                break;
            case ExtraLife:
                run.Lives++;
                break;
            case AmmoPack:
                run.Ammo += 10;
                break;
        }
        return true;
    }
}
