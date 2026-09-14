using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A text-only menu entry, the way real games do it (Hollow Knight, Celeste,
/// Hades): no button box — just type. Selection is shown by the label turning
/// amber, sliding right a few pixels, and a small triangle marker fading in.
///
/// Sits on a GameObject with a Button (transition: None) that supplies onClick
/// and keyboard/gamepad navigation; an invisible full-rect Image provides the
/// mouse hit area. The scaffold builder assembles all of that.
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuItem : MonoBehaviour,
    ISelectHandler, IDeselectHandler, IPointerEnterHandler, ISubmitHandler, IPointerClickHandler
{
    [Header("Wiring (set by the scaffold builder)")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private Graphic marker;   // small triangle left of the label

    [Header("Feel")]
    [SerializeField] private float slideDistance = 14f;
    [SerializeField] private float animSeconds = 0.08f;

    private Button _button;
    private Vector2 _labelBasePos;
    private Coroutine _anim;
    private bool _selected;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (label != null) _labelBasePos = label.rectTransform.anchoredPosition;
        ApplyInstant(false);
    }

    private void OnDisable()
    {
        _selected = false;
        ApplyInstant(false);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (_button.interactable && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject); // hover selects — mouse and gamepad never fight
    }

    public void OnSelect(BaseEventData e)
    {
        if (!_button.interactable) return;
        _selected = true;
        AudioManager.Instance?.PlayUIHover();
        Animate(true);
    }

    public void OnDeselect(BaseEventData e)
    {
        _selected = false;
        Animate(false);
    }

    public void OnSubmit(BaseEventData e) => Click();
    public void OnPointerClick(PointerEventData e) => Click();

    private void Click()
    {
        if (_button.interactable) AudioManager.Instance?.PlayUIClick();
    }

    private void Animate(bool selected)
    {
        if (!isActiveAndEnabled) { ApplyInstant(selected); return; }
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(AnimRoutine(selected));
    }

    private void ApplyInstant(bool selected)
    {
        if (label != null)
        {
            label.color = selected ? Theme.Accent : Theme.Text;
            label.rectTransform.anchoredPosition = _labelBasePos + (selected ? Vector2.right * slideDistance : Vector2.zero);
        }
        if (marker != null)
        {
            var c = marker.color; c.a = selected ? 1f : 0f; marker.color = c;
        }
    }

    private IEnumerator AnimRoutine(bool selected)
    {
        float t = 0f;
        Color fromCol = label.color;
        Color toCol = selected ? Theme.Accent : Theme.Text;
        Vector2 fromPos = label.rectTransform.anchoredPosition;
        Vector2 toPos = _labelBasePos + (selected ? Vector2.right * slideDistance : Vector2.zero);
        float fromA = marker != null ? marker.color.a : 0f;
        float toA = selected ? 1f : 0f;

        while (t < animSeconds)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / animSeconds);
            label.color = Color.Lerp(fromCol, toCol, k);
            label.rectTransform.anchoredPosition = Vector2.Lerp(fromPos, toPos, k);
            if (marker != null)
            {
                var c = marker.color; c.a = Mathf.Lerp(fromA, toA, k); marker.color = c;
            }
            yield return null;
        }
        ApplyInstant(selected);
        // Keep whatever state we're actually in (covers select-during-anim races).
        if (_selected != selected) ApplyInstant(_selected);
    }
}
