using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover/press juice for any Selectable: a small scale pop plus procedural
/// hover/click sounds. Works for mouse (pointer events) AND gamepad/keyboard
/// (select/submit events). Uses unscaled time so it works in the pause menu.
/// No tweening library — one tiny coroutine.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class UIButtonFeedback : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler, ISubmitHandler, IPointerClickHandler
{
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float pressScale = 0.95f;
    [SerializeField] private float animSeconds = 0.08f;

    private Selectable _selectable;
    private Coroutine _anim;
    private Vector3 _baseScale;
    private Image _glow; // optional child named "Glow" — accent ring shown while selected

    private void Awake()
    {
        _selectable = GetComponent<Selectable>();
        _baseScale = transform.localScale;
        var glowT = transform.Find("Glow");
        if (glowT != null) _glow = glowT.GetComponent<Image>();
        SetGlow(false);
    }

    private void OnDisable()
    {
        transform.localScale = _baseScale; // never get stuck mid-pop
        SetGlow(false);
    }

    private void SetGlow(bool on)
    {
        if (_glow != null) _glow.enabled = on;
    }

    // Mouse hover selects the button too, so mouse and gamepad highlight never fight.
    public void OnPointerEnter(PointerEventData e)
    {
        if (!_selectable.interactable) return;
        EventSystem.current?.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData e) => ScaleTo(_baseScale);

    public void OnSelect(BaseEventData e)
    {
        if (!_selectable.interactable) return;
        AudioManager.Instance?.PlayUIHover();
        SetGlow(true);
        ScaleTo(_baseScale * hoverScale);
    }

    public void OnDeselect(BaseEventData e)
    {
        SetGlow(false);
        ScaleTo(_baseScale);
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (_selectable.interactable) ScaleTo(_baseScale * pressScale);
    }

    public void OnPointerUp(PointerEventData e) => ScaleTo(_baseScale * hoverScale);

    public void OnPointerClick(PointerEventData e) => PlayClick();
    public void OnSubmit(BaseEventData e) => PlayClick();

    private void PlayClick()
    {
        if (_selectable.interactable) AudioManager.Instance?.PlayUIClick();
    }

    private void ScaleTo(Vector3 target)
    {
        if (!isActiveAndEnabled) return;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(ScaleRoutine(target));
    }

    private IEnumerator ScaleRoutine(Vector3 target)
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        while (t < animSeconds)
        {
            t += Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(t / animSeconds));
            yield return null;
        }
        transform.localScale = target;
    }
}
