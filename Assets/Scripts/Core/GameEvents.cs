using System;
using UnityEngine;

/// <summary>
/// Decoupling layer between gameplay (built during the jam) and the UI shell
/// (built now). Gameplay fires one event; it never needs to know any UI exists.
///
///   GameEvents.RaiseGameOver(won: true);   // → GameOverScreen shows "You Win"
///   GameEvents.RaiseGameOver(won: false);  // → GameOverScreen shows "Game Over"
/// </summary>
public static class GameEvents
{
    /// <summary>Fired when a run ends. bool = did the player win.</summary>
    public static event Action<bool> GameOver;

    /// <summary>Optional one-line run summary (distance / score / cause of death),
    /// set by gameplay right before RaiseGameOver — the Game Over screen shows it.</summary>
    public static string LastRunSummary = "";

    /// <summary>Fired when the pause state changes. bool = is now paused.
    /// Gameplay can subscribe to mute inputs, stop timers, etc.</summary>
    public static event Action<bool> PauseChanged;

    /// <summary>Record outcome of the run that just ended — set by the gameplay
    /// controller immediately before RaiseGameOver so the results screen can
    /// celebrate the right thing without recomputing anything.</summary>
    public static SaveSystem.RunRecord LastRunRecord;

    /// <summary>The run's best slow-mo moment, captured in memory only (never
    /// written to disk). Shown on the game-over screen, cleared each new run.</summary>
    public static Texture2D BestSnapTexture;
    public static int BestSnapScore;
    public static string BestSnapLabel = "";

    public static void ClearBestSnap()
    {
        if (BestSnapTexture != null) UnityEngine.Object.Destroy(BestSnapTexture);
        BestSnapTexture = null;
        BestSnapScore = 0;
        BestSnapLabel = "";
    }

    public static void RaiseGameOver(bool won) => GameOver?.Invoke(won);
    public static void RaisePauseChanged(bool paused) => PauseChanged?.Invoke(paused);
}
