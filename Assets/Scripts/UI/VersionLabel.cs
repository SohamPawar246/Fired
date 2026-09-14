using TMPro;
using UnityEngine;

/// <summary>
/// Stamps "v{Application.version}" into a corner label so you always know which
/// build a judge/teammate is looking at. Change the version in
/// Project Settings > Player > Version (currently 0.1.0) before each upload.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class VersionLabel : MonoBehaviour
{
    private void Start()
    {
        GetComponent<TMP_Text>().text = $"v{Application.version}";
    }
}
