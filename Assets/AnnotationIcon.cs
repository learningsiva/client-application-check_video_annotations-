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
    private bool isMinimizing = false;

    // Cached reference to SonarSpawner on the CenterDot child
    private SonarSpawner sonarSpawner;

    // ---------------------------------------------------------------

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

        // Find SonarSpawner once on the CenterDot child — reused for all state changes
        if (sonarSpawner == null)
        {
            Transform centerDot = transform.Find("CenterDot");
            if (centerDot != null)
                sonarSpawner = centerDot.GetComponent<SonarSpawner>();
            else
                Debug.LogWarning("[AnnotationIcon] 'CenterDot' child not found — sonar control disabled.");
        }

        if (timestampLabel) timestampLabel.text = FormatTime(data.timestamp);

        isPaused = false;
        currentTimer = 0f;
        isMinimizing = false;

        if (forceMinimized)
        {
            state = State.Minimized;
            GetComponent<RectTransform>().anchoredPosition = minimizedPos;
            SetSonar(false); // Already at corner — sonar off immediately
            onMinimize?.Invoke(this);
        }
        else
        {
            state = State.Showing;
            SetSonar(true); // Showing on video — sonar on
            GetComponent<RectTransform>().anchoredPosition = annotationPos;
            StartCoroutine(AutoMinimizeRoutine());
        }
    }

    public AnnotationItem GetData() => data;

    public bool IsShowing() => state == State.Showing;

    public bool IsMinimizing() => isMinimizing || state == State.Minimized;

    // ---------------------------------------------------------------

    public void ForceMinimizeImmediate()
    {
        if (state == State.Minimized) return;

        state = State.Minimized;
        isMinimizing = true;
        currentTimer = displayDuration;

        StopAllCoroutines();
        GetComponent<RectTransform>().anchoredPosition = minimizedPos;

        // Icon snapped instantly to corner — disable sonar right away
        SetSonar(false);

        onMinimize?.Invoke(this);
    }

    public void MoveToMinimizedPosition()
    {
        state = State.Minimized;
        isMinimizing = true;
        StartCoroutine(MoveTo(minimizedPos, onArrival: () => SetSonar(false)));
        onMinimize?.Invoke(this);
    }

    public void Restore()
    {
        state = State.Showing;
        isMinimizing = false;
        currentTimer = 0f;

        // Re-enable sonar as soon as icon starts moving back to video position
        SetSonar(true);

        StopAllCoroutines();
        StartCoroutine(MoveTo(annotationPos));
        StartCoroutine(AutoMinimizeRoutine());
    }

    public void SetPaused(bool paused) => isPaused = paused;

    public void SetShowingAnnotationsState() => state = State.Showing;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (state == State.Minimized)
            onTimeTravel?.Invoke(data, this);
        else
            onClick?.Invoke(data, this);
    }

    // ---------------------------------------------------------------

    private IEnumerator AutoMinimizeRoutine()
    {
        while (currentTimer < displayDuration)
        {
            if (!isPaused) currentTimer += Time.deltaTime;
            yield return null;
        }
        MoveToMinimizedPosition();
    }

    // onArrival callback — fires exactly when icon reaches its target position
    private IEnumerator MoveTo(Vector2 target, System.Action onArrival = null)
    {
        RectTransform rt = GetComponent<RectTransform>();

        while (Vector2.Distance(rt.anchoredPosition, target) > 1f)
        {
            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, target, Time.deltaTime * 5f);
            yield return null;
        }

        rt.anchoredPosition = target;

        // Fire arrival callback now that movement is complete
        onArrival?.Invoke();

        // Clear minimizing flag once we've fully arrived (either direction)
        isMinimizing = false;
    }

    // ---------------------------------------------------------------

    private void SetSonar(bool enabled)
    {
        if (sonarSpawner != null)
            sonarSpawner.enabled = enabled;
    }

    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60F);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60F);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}