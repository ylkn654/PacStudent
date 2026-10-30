using UnityEngine;
using UnityEngine.EventSystems;

// button feedback: grows on hover, shrinks on press, plays sounds
public class ButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public AudioSource audioSource;
    public AudioClip hoverClip;
    public AudioClip clickClip;
    public float hoverScale = 1.1f;
    public float pressScale = 0.92f;
    public float animSpeed = 12f;

    Vector3 s0;
    private float tgt = 1f;
    bool h;

    private void Awake() { s0 = transform.localScale; }

    void OnDisable()
    {
        h = false;
        tgt = 1f;
        transform.localScale = s0;
    }

    private void Update() {
        // ease towards target scale every frame
        transform.localScale = Vector3.Lerp(transform.localScale, s0 * tgt, 1f - Mathf.Exp(-animSpeed * Time.unscaledDeltaTime));
    }

    // pointer enter / exit / down / up
    public void OnPointerEnter(PointerEventData e)
    {
        var sel = GetComponent<UnityEngine.UI.Selectable>();
        if (sel != null && sel.IsInteractable() == false) return;
        h = true;
        tgt = hoverScale;
        if (audioSource != null && hoverClip != null) audioSource.PlayOneShot(hoverClip);
    }

    public void OnPointerExit(PointerEventData e)
    {
        h = false;
        tgt = 1f;
    }

    public void OnPointerDown(PointerEventData e)
    {
        var s = GetComponent<UnityEngine.UI.Selectable>();
        if (!(s == null || s.IsInteractable())) return;
        tgt = pressScale;
        if (audioSource != null)
            if (clickClip != null)
                audioSource.PlayOneShot(clickClip);
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (h == true) tgt = hoverScale;
        else tgt = 1f;
    }
}
