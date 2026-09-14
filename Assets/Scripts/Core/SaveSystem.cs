using UnityEngine;

/// <summary>
/// All persistence lives here, behind Save()/Load(). Storage is PlayerPrefs
/// (works on WebGL via IndexedDB, zero setup). Gameplay code never touches
/// PlayerPrefs directly — if we ever switch to a JSON file, only this file changes.
///
/// Usage:
///   SaveSystem.MusicVolume = 0.5f; SaveSystem.Save();
///   if (SaveSystem.HasSaveData) ShowContinueButton();
/// </summary>
public static class SaveSystem
{
    // ---- Defaults (also used by "Reset to defaults" in Settings) ------------
    private const float DefaultMaster = 1f;
    private const float DefaultMusic = 0.8f;
    private const float DefaultSfx = 1f;
    private const float DefaultSensitivity = 1f; // multiplier on the game's tuned sensitivity

    // ---- In-memory state (loaded once at boot by GameManager) ---------------
    public static float MasterVolume = DefaultMaster;
    public static float MusicVolume = DefaultMusic;
    public static float SfxVolume = DefaultSfx;
    public static bool Fullscreen = true;
    public static int ResolutionIndex = -1; // -1 = "never chosen, use current"
    public static float MouseSensitivity = DefaultSensitivity; // 0.3..2.5, 1 = tuned default

    /// <summary>Main Menu shows Continue only when this is true. Gameplay code
    /// should set it to true when there is real progress worth resuming.</summary>
    public static bool HasSaveData;

    // ---- Records (the day-1 hook) -------------------------------------------
    // A run used to evaporate the moment it ended, so there was never a reason to
    // press Retry beyond curiosity. These four values are what make the next run
    // mean something.
    public static int BestScore;
    public static float BestDistance;
    public static int FurthestZone;              // index into the streamer's zones
    public static string FurthestZoneName = "";  // shown to the player, e.g. "THE CAVES"
    public static int RunsPlayed;

    // PlayerPrefs keys — prefixed to avoid collisions with anything else.
    private const string KMaster = "jam.volume.master";
    private const string KMusic = "jam.volume.music";
    private const string KSfx = "jam.volume.sfx";
    private const string KFullscreen = "jam.video.fullscreen";
    private const string KResolution = "jam.video.resolution";
    private const string KHasSave = "jam.save.exists";
    private const string KSensitivity = "jam.input.sensitivity";
    private const string KBestScore = "jam.best.score";
    private const string KBestDist = "jam.best.distance";
    private const string KBestZone = "jam.best.zone";
    private const string KBestZoneName = "jam.best.zonename";
    private const string KRuns = "jam.runs";

    public static void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(KMaster, DefaultMaster);
        MusicVolume = PlayerPrefs.GetFloat(KMusic, DefaultMusic);
        SfxVolume = PlayerPrefs.GetFloat(KSfx, DefaultSfx);
        Fullscreen = PlayerPrefs.GetInt(KFullscreen, 1) == 1;
        ResolutionIndex = PlayerPrefs.GetInt(KResolution, -1);
        HasSaveData = PlayerPrefs.GetInt(KHasSave, 0) == 1;
        MouseSensitivity = PlayerPrefs.GetFloat(KSensitivity, DefaultSensitivity);

        BestScore = PlayerPrefs.GetInt(KBestScore, 0);
        BestDistance = PlayerPrefs.GetFloat(KBestDist, 0f);
        FurthestZone = PlayerPrefs.GetInt(KBestZone, 0);
        FurthestZoneName = PlayerPrefs.GetString(KBestZoneName, "");
        RunsPlayed = PlayerPrefs.GetInt(KRuns, 0);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(KMaster, MasterVolume);
        PlayerPrefs.SetFloat(KMusic, MusicVolume);
        PlayerPrefs.SetFloat(KSfx, SfxVolume);
        PlayerPrefs.SetInt(KFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KResolution, ResolutionIndex);
        PlayerPrefs.SetInt(KHasSave, HasSaveData ? 1 : 0);
        PlayerPrefs.SetFloat(KSensitivity, MouseSensitivity);

        PlayerPrefs.SetInt(KBestScore, BestScore);
        PlayerPrefs.SetFloat(KBestDist, BestDistance);
        PlayerPrefs.SetInt(KBestZone, FurthestZone);
        PlayerPrefs.SetString(KBestZoneName, FurthestZoneName);
        PlayerPrefs.SetInt(KRuns, RunsPlayed);

        PlayerPrefs.Save(); // flush immediately (important on WebGL)
    }

    /// <summary>Reset settings only — deliberately does NOT wipe save data.</summary>
    public static void ResetSettingsToDefaults()
    {
        MasterVolume = DefaultMaster;
        MusicVolume = DefaultMusic;
        SfxVolume = DefaultSfx;
        Fullscreen = true;
        ResolutionIndex = -1;
        MouseSensitivity = DefaultSensitivity;
        Save();
    }

    /// <summary>
    /// Bank a finished run. Returns which records it broke so the results screen can
    /// celebrate the right one. Called once per run, at death or at the finish line.
    /// </summary>
    public struct RunRecord
    {
        public bool NewBestDistance;
        public bool NewBestScore;
        public bool NewFurthestZone;
        public float PreviousBestDistance;
        public int PreviousBestScore;
        public bool AnyRecord => NewBestDistance || NewBestScore || NewFurthestZone;
    }

    public static RunRecord RecordRun(int score, float distance, int zoneIndex, string zoneName)
    {
        var r = new RunRecord
        {
            PreviousBestDistance = BestDistance,
            PreviousBestScore = BestScore,
        };

        RunsPlayed++;
        if (distance > BestDistance) { BestDistance = distance; r.NewBestDistance = true; }
        if (score > BestScore) { BestScore = score; r.NewBestScore = true; }
        if (zoneIndex > FurthestZone || string.IsNullOrEmpty(FurthestZoneName))
        {
            if (zoneIndex >= FurthestZone)
            {
                r.NewFurthestZone = zoneIndex > FurthestZone;
                FurthestZone = zoneIndex;
                if (!string.IsNullOrEmpty(zoneName)) FurthestZoneName = zoneName;
            }
        }

        HasSaveData = true;
        Save();
        return r;
    }

    /// <summary>Wipes records only — settings are left alone.</summary>
    public static void ClearRecords()
    {
        BestScore = 0;
        BestDistance = 0f;
        FurthestZone = 0;
        FurthestZoneName = "";
        RunsPlayed = 0;
        Save();
    }

    public static void ClearSaveData()
    {
        HasSaveData = false;
        Save();
    }
}
