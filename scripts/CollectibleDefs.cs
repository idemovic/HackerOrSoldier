using Godot;

namespace HackerOrSoldier;

// One row of the README collectibles table: instant score and coins on pickup.
public record CollectibleDef(string Type, string Name, Color Color, int Score, int Coins, string Letter = null);

public static class CollectibleDefs
{
    public const string LetterA = "letter_a";
    public const string LetterG = "letter_g";
    public const string LetterE = "letter_e";
    public const string LetterN = "letter_n";
    public const string LetterT = "letter_t";
    public const string AdminPassword = "admin_password";
    public const string Diamond = "diamond";
    public const string Notebook = "notebook";
    public const string Mobile = "mobile";
    public const string Smartwatch = "smartwatch";
    public const string AudioRecorder = "audio_recorder";
    public const string SimCard = "sim_card";
    public const string CashStack = "cash_stack";
    public const string BusinessCard = "business_card";
    public const string Tombstone = "tombstone";

    // README: each letter 1k; collecting all five is worth 40k total + 5 coins.
    public const int LetterSetBonusScore = 35000;
    public const int LetterSetBonusCoins = 5;

    public static readonly CollectibleDef[] All =
    {
        new(LetterA, "Letter A", new Color(0.95f, 0.80f, 0.25f), 1000, 0, "A"),
        new(LetterG, "Letter G", new Color(0.95f, 0.80f, 0.25f), 1000, 0, "G"),
        new(LetterE, "Letter E", new Color(0.95f, 0.80f, 0.25f), 1000, 0, "E"),
        new(LetterN, "Letter N", new Color(0.95f, 0.80f, 0.25f), 1000, 0, "N"),
        new(LetterT, "Letter T", new Color(0.95f, 0.80f, 0.25f), 1000, 0, "T"),
        new(AdminPassword, "Admin password", new Color(0.30f, 0.95f, 0.55f), 30000, 10),
        new(Diamond, "Diamond", new Color(0.70f, 0.90f, 1.00f), 25000, 7),
        new(Notebook, "Notebook", new Color(0.55f, 0.55f, 0.60f), 20000, 6),
        new(Mobile, "Mobile", new Color(0.25f, 0.30f, 0.35f), 15000, 5),
        new(Smartwatch, "Smartwatch", new Color(0.35f, 0.40f, 0.90f), 10000, 5),
        new(AudioRecorder, "Audio recorder", new Color(0.75f, 0.35f, 0.35f), 7000, 2),
        new(SimCard, "SIM card", new Color(0.90f, 0.60f, 0.20f), 5000, 2),
        new(CashStack, "Cash stack", new Color(0.45f, 0.75f, 0.35f), 3000, 1),
        new(BusinessCard, "Business card", new Color(0.90f, 0.90f, 0.85f), 2000, 1),
        new(Tombstone, "Tombstone", new Color(0.60f, 0.60f, 0.65f), 1000, 0),
    };

    public static readonly string[] LetterTypes = { LetterA, LetterG, LetterE, LetterN, LetterT };

    public static CollectibleDef Get(string type)
    {
        foreach (var def in All)
            if (def.Type == type)
                return def;
        return null;
    }

    public static bool IsLetter(string type) => System.Array.IndexOf(LetterTypes, type) >= 0;
}
