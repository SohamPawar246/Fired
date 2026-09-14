using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The settings panel (shared prefab — the same panel is instantiated in the
/// Main Menu and the Pause Menu, so it behaves identically in both).
///
/// Volumes drive AudioManager live; video settings apply immediately; everything
/// persists through SaveSystem the moment it changes. On WebGL the resolution
/// row hides itself (browsers own the canvas size).
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Slider sensitivitySlider; // 0.3..2.5, 1 = tuned default
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private GameObject resolutionRow;      // hidden on WebGL
    [SerializeField] private TMP_Dropdown resolutionDropdown;

    private readonly List<Resolution> _resolutions = new();
    private bool _initializing; // suppress onValueChanged while populating UI

    private void OnEnable()
    {
        _initializing = true;

        masterSlider.value = SaveSystem.MasterVolume;
        musicSlider.value = SaveSystem.MusicVolume;
        sfxSlider.value = SaveSystem.SfxVolume;
        if (sensitivitySlider != null) sensitivitySlider.value = SaveSystem.MouseSensitivity;
        fullscreenToggle.isOn = SaveSystem.Fullscreen;

#if UNITY_WEBGL && !UNITY_EDITOR
        if (resolutionRow != null) resolutionRow.SetActive(false);
#else
        PopulateResolutions();
#endif
        _initializing = false;
    }

    private void PopulateResolutions()
    {
        _resolutions.Clear();
        resolutionDropdown.ClearOptions();

        // Collapse duplicate WxH entries (same size at different refresh rates).
        var seen = new HashSet<(int, int)>();
        var labels = new List<string>();
        int currentIndex = 0;

        foreach (var res in Screen.resolutions)
        {
            if (!seen.Add((res.width, res.height))) continue;
            if (res.width == Screen.width && res.height == Screen.height)
                currentIndex = _resolutions.Count;
            _resolutions.Add(res);
            labels.Add($"{res.width} × {res.height}");
        }

        resolutionDropdown.AddOptions(labels);
        int saved = SaveSystem.ResolutionIndex;
        resolutionDropdown.SetValueWithoutNotify(
            saved >= 0 && saved < _resolutions.Count ? saved : currentIndex);
        resolutionDropdown.RefreshShownValue();
    }

    // ---- Change handlers (wired by the scaffold builder) -----------------------

    public void OnMasterChanged(float v)
    {
        if (_initializing) return;
        AudioManager.Instance.SetMasterVolume(v);
        SaveSystem.Save();
    }

    public void OnMusicChanged(float v)
    {
        if (_initializing) return;
        AudioManager.Instance.SetMusicVolume(v);
        SaveSystem.Save();
    }

    public void OnSfxChanged(float v)
    {
        if (_initializing) return;
        AudioManager.Instance.SetSfxVolume(v);
        SaveSystem.Save();
        AudioManager.Instance.PlayUIClick(); // instant feedback at the new volume
    }

    public void OnSensitivityChanged(float v)
    {
        if (_initializing) return;
        SaveSystem.MouseSensitivity = Mathf.Clamp(v, 0.3f, 2.5f);
        SaveSystem.Save();
    }

    public void OnFullscreenChanged(bool on)
    {
        if (_initializing) return;
        SaveSystem.Fullscreen = on;
        Screen.fullScreen = on;
        SaveSystem.Save();
    }

    public void OnResolutionChanged(int index)
    {
        if (_initializing || index < 0 || index >= _resolutions.Count) return;
        SaveSystem.ResolutionIndex = index;
        var res = _resolutions[index];
        Screen.SetResolution(res.width, res.height, SaveSystem.Fullscreen);
        SaveSystem.Save();
    }

    public void OnResetToDefaults()
    {
        SaveSystem.ResetSettingsToDefaults();
        AudioManager.Instance.ApplyVolumes();
        Screen.fullScreen = SaveSystem.Fullscreen;
        AudioManager.Instance.PlayUIConfirm();
        OnEnable(); // re-populate the UI from the fresh values
    }

    public void OnBack()
    {
        AudioManager.Instance.PlayUIBack();
        gameObject.SetActive(false);
    }
}
