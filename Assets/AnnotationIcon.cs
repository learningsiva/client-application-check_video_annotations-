using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using TMPro;

public class AnnotationIcon : MonoBehaviour, IPointerClickHandler
{
    [Header("UI References")]
    public Image iconImage;
    public TMP_Text timestampLabel;

    [Header("Settings")]
    public float displayDuration = 2.0f;

    private AnnotationItem data;
    private Vector2 annotationPos, minimizedPos;

    private System.Action<AnnotationItem, AnnotationIcon> onClick;
    private System.Action<AnnotationItem, AnnotationIcon> onTimeTravel;
    private System.Action<AnnotationIcon> onMinimize;

    private enum State { JustAppeared, Showing, Minimized }
    private State state = State.JustAppeared;

    private bool isPaused = false;
    private float currentTimer = 0f;

    // 🔥 NEW: Flag to track movement state
    private bool isMinimizing = false;

    public void Initialize(AnnotationItem d, Vector2 aPos, Vector2 mPos,
                           System.Action<AnnotationItem, AnnotationIcon> click,
                           System.Action<AnnotationItem, AnnotationIcon> travel,
                           System.Action<AnnotationIcon> minimize,
                           bool forceMinimized)
    {
        data = d;
        annotationPos = aPos;
        minimizedPos = mPos;
        onClick = click;
        onTimeTravel = travel;
        onMinimize = minimize;

        if (timestampLabel) timestampLabel.text = FormatTime(data.timestamp);

        isPaused = false;
        currentTimer = 0f;
        isMinimizing = false; // Reset flag

        if (forceMinimized)
        {
            state = State.Minimized;
            GetComponent<RectTransform>().anchoredPosition = minimizedPos;
            onMinimize?.Invoke(this);
        }
        else
        {
            state = State.Showing;
            GetComponent<RectTransform>().anchoredPosition = annotationPos;
            StartCoroutine(AutoMinimizeRoutine());
        }
    }

    public AnnotationItem GetData() => data;

    public bool IsShowing()
    {
        return state == State.Showing;
    }

    // 🔥 NEW: Helper for VideoReplayManager
    public bool IsMinimizing()
    {
        // We are minimizing if the flag is true OR if we are fully minimized
        return isMinimizing || state == State.Minimized;
    }

    public void ForceMinimizeImmediate()
    {
        if (state == State.Minimized) return;

        state = State.Minimized;
        isMinimizing = true; // Mark as moving
        currentTimer = displayDuration;

        StopAllCoroutines();
        MoveToMinimizedPosition();
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
    }

    public void Restore()
    {
        state = State.Showing;
        isMinimizing = false; // 🔥 Reset flag so Panel knows it's safe to show
        currentTimer = 0f;

        StopAllCoroutines();
        StartCoroutine(MoveTo(annotationPos));
        StartCoroutine(AutoMinimizeRoutine());
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (state == State.Minimized)
            onTimeTravel?.Invoke(data, this);
        else
            onClick?.Invoke(data, this);
    }

    public void MoveToMinimizedPosition()
    {
        state = State.Minimized;
        isMinimizing = true; // 🔥 Mark start of movement
        StartCoroutine(MoveTo(minimizedPos));
        onMinimize?.Invoke(this);
    }

    public void SetShowingAnnotationsState()
    {
        state = State.Showing;
    }

    private IEnumerator AutoMinimizeRoutine()
    {
        while (currentTimer < displayDuration)
        {
            if (!isPaused)
            {
                currentTimer += Time.deltaTime;
            }
            yield return null;
        }
        MoveToMinimizedPosition();
    }

    private IEnumerator MoveTo(Vector2 target)
    {
        RectTransform rt = GetComponent<RectTransform>();

        // Simple easing
        while (Vector2.Distance(rt.anchoredPosition, target) > 1f)
        {
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, Time.deltaTime * 5f);
            yield return null;
        }
        rt.anchoredPosition = target;
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60F);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}