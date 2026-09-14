using UnityEngine;

/// <summary>
/// The player's chosen blaster, shared between the menu carousel and the Game
/// scene (ShooterGunEquip). Persisted in PlayerPrefs so the choice survives
/// sessions; defaults to blasterG (the original hand-authored gun).
/// </summary>
public static class GunSelection
{
    private const string Key = "fired.selectedGun";
    public const string Default = "blasterG";

    private static string _cached;

    public static string SelectedName
    {
        get
        {
            if (string.IsNullOrEmpty(_cached))
                _cached = PlayerPrefs.GetString(Key, Default);
            return _cached;
        }
        set
        {
            _cached = string.IsNullOrEmpty(value) ? Default : value;
            PlayerPrefs.SetString(Key, _cached);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Direction the barrel points, in the model's local space.
    /// Kenney's blaster kit consistently models the barrel along local -Z
    /// (verified visually against a rendered lineup of all 14 blasters), so
    /// alignment code should map THIS vector to "forward", not the raw
    /// longest-bounds axis (which is +Z and points out the stock).</summary>
    public static readonly Vector3 KitBarrelAxis = Vector3.back;

    /// <summary>Pretty display name: "blasterG" → "BLASTER G".</summary>
    public static string DisplayName(string assetName)
    {
        if (string.IsNullOrEmpty(assetName)) return "";
        string s = assetName;
        if (s.StartsWith("blaster") && s.Length > 7)
            return "BLASTER " + s.Substring(7).ToUpperInvariant();
        return s.ToUpperInvariant();
    }
}
