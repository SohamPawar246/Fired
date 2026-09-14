using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// When this object becomes active, gives the EventSystem selection to a target
/// (e.g. the first slider in the Settings panel). This is what keeps
/// gamepad/keyboard navigation working when panels open on top of a menu.
/// </summary>
public class SelectOnEnable : MonoBehaviour
{
    [SerializeField] private GameObject target;

    private void OnEnable()
    {
        if (target != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(target);
    }
}
