using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using TMPro;

#if !UNITY_WEBGL || UNITY_EDITOR
using NativeGalleryNamespace;
#endif

public class VideoReplayManager : MonoBehaviour
{
    [Header("UI References")]
    public RawImage videoDisplay;
    public VideoPlayer videoPlayer;
    public Button backButton;
    public Button shareButton;

    public TMP_Text videoTitleText;
    public GameObject playButtonOverlay;

    public TMP_Text statusText;
    public Slider seekSlider;
    public TMP_Text currentTimeText;
    public TMP_Text totalTimeText;

    [Header("UI References - User Info")]
    public TMP_Text fullNameText;
    public TMP_Text firstLetterText;
    public TMP_Text authorDesignationText;

    [Header("UI Containers")]
    public GameObject timelineMarkerPrefab;
    public Transform timelineMarkerContainer;

    [Header("Popup Panel")]
    public GameObject annotationPanel;
    public TMP_Text headingText;
    public TMP_Text bodyText;
    public Button closeAnnotationButton;
    public Vector2 panelOffset = new Vector2(0, -100f);

    [Header("Icons")]
    public GameObject iconPrefab;
    public Transform iconContainer;

    [Header("Settings")]
    public float minimizedIconLeftOffset = 80f;
    public float minimizedIconTopOffset = -80f;
    public float minimizedIconSpacing = 70f;
    public float iconDisplayDuration = 1.0f;

    // 🔥 NEW: Reference to the separate social manager script
    [Header("Social Manager")]
    public VideoSocialManager socialManager;
    public Button openCommentsButton;

    private const float TIMESTAMP_TOLERANCE = 0.05f;
    private List<AnnotationItem> allAnnotations = new List<AnnotationItem>();

    private Queue<AnnotationIcon> iconPool = new Queue<AnnotationIcon>();
    private List<AnnotationIcon> activeIcons = new List<AnnotationIcon>();
    private HashSet<float> triggeredTimestamps = new HashSet<float>();

    private RectTransform videoDisplayRect;
    private RectTransform iconContainerRect;
    private RectTransform annotationPanelRect;

    private bool isUserDraggingSlider = false;
    private bool hasLoadedAnnotations = false;
    private bool isVideoReady = false;
    private int nextAnnotationIndex = 0;

    private AnnotationIcon currentFocusIcon = null;

    private void Start()
    {
        videoDisplayRect = videoDisplay.GetComponent<RectTransform>();
        iconContainerRect = iconContainer.GetComponent<RectTransform>();
        if (annotationPanel)
        {
            annotationPanelRect = annotationPanel.GetComponent<RectTransform>();
            annotationPanel.SetActive(false);
        }

        if (backButton) backButton.onClick.AddListener(OnBackClicked);
        if (closeAnnotationButton) closeAnnotationButton.onClick.AddListener(HideAnnotationPanel);
        if (shareButton) shareButton.onClick.AddListener(OnShareClicked);

        if (seekSlider != null)
        {
            seekSlider.onValueChanged.AddListener(OnSeekSliderValueChanged);
            SetupSliderEventTriggers();
        }

        SetupVideoClickTrigger();

        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.errorReceived += (vp, msg) => UpdateStatus("Video Error: " + msg);

        InitializeIconPool(20);

        if (openCommentsButton) openCommentsButton.onClick.AddListener(OnOpenCommentsClicked);

        if (AppSession.CurrentVideo != null)
            LoadFromSession(AppSession.CurrentVideo);
        else
            UpdateStatus(" No Video Selected.");
    }

    void OnOpenCommentsClicked()
    {
        // Delegate UI opening to the social manager's controller
        if (socialManager != null && socialManager.commentsController != null)
        {
            socialManager.commentsController.OpenPanel();
        }
    }

    private void LoadFromSession(VideoItem videoData)
    {
        Debug.Log($"Loading Video: {videoData.title} (ID: {videoData.task_id})");

        // 1. Set Title & User Info
        if (videoTitleText) videoTitleText.text = videoData.title;

        if (videoData.user_data != null)
        {
            string fName = videoData.user_data.first_name ?? "";
            string lName = videoData.user_data.last_name ?? "";

            if (fullNameText) fullNameText.text = $"{fName} {lName}";
            if (firstLetterText && !string.IsNullOrEmpty(fName))
                firstLetterText.text = fName.Substring(0, 1).ToUpper();

            if (authorDesignationText) authorDesignationText.text = videoData.user_data.designation;
        }

        // 2. 🔥 DELEGATE SOCIAL LOGIC TO NEW MANAGER
        if (socialManager != null)
        {
            socialManager.Initialize(
                videoData.task_id,
                videoData.is_liked,
                videoData.likes_count,
                videoData.is_saved
            );
        }

        // 3. Prepare Video
        videoPlayer.url = videoData.video_url;
        videoPlayer.Prepare();

        // 4. Load Annotations
        allAnnotations = videoData.annotations;
        if (allAnnotations == null) allAnnotations = new List<AnnotationItem>();
        allAnnotations.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

        hasLoadedAnnotations = true;
    }

    // --- (The rest of the script remains EXACTLY the same as your "Working Fine" version) ---

    public void OnShareClicked()
    {
        if (AppSession.CurrentVideo == null) return;
        string title = AppSession.CurrentVideo.title;
        string url = AppSession.CurrentVideo.video_url;
        string shareMessage = $"Check out this video: {title}\n{url}";
        StartCoroutine(ShareTextNative(title, shareMessage));
    }

    System.Collections.IEnumerator ShareTextNative(string subject, string body)
    {
        yield return new WaitForEndOfFrame();
#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");
        AndroidJavaObject intentObject = new AndroidJavaObject("android.content.Intent");
        intentObject.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND"));
        intentObject.Call<AndroidJavaObject>("setType", "text/plain");
        intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), subject);
        intentObject.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), body);
        AndroidJavaClass unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unity.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intentObject, "Share Video via");
        currentActivity.Call("startActivity", chooser);
#else
        Debug.Log($"[Mock Share] Subject: {subject}\nBody: {body}");
#endif
    }

    void InitializeIconPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = Instantiate(iconPrefab, iconContainer);
            obj.SetActive(false);
            AnnotationIcon icon = obj.GetComponent<AnnotationIcon>();
            iconPool.Enqueue(icon);
        }
    }

    AnnotationIcon GetIconFromPool()
    {
        if (iconPool.Count > 0)
        {
            AnnotationIcon icon = iconPool.Dequeue();
            icon.gameObject.SetActive(true);
            return icon;
        }
        else
        {
            GameObject obj = Instantiate(iconPrefab, iconContainer);
            return obj.GetComponent<AnnotationIcon>();
        }
    }

    void ReturnIconToPool(AnnotationIcon icon)
    {
        if (currentFocusIcon == icon) HideAnnotationPanel();
        icon.gameObject.SetActive(false);
        iconPool.Enqueue(icon);
    }

    private void Update()
    {
        if (isVideoReady && !isUserDraggingSlider)
        {
            if (seekSlider) seekSlider.value = (float)videoPlayer.time;
            if (currentTimeText) currentTimeText.text = FormatTime(videoPlayer.time);
        }

        if (videoPlayer.isPlaying)
        {
            CheckForAnnotationsOptimized();
            UpdatePlayStateVisuals(true);
        }
        else
        {
            UpdatePlayStateVisuals(false);
        }

        if (currentFocusIcon != null && annotationPanel.activeSelf)
        {
            if (currentFocusIcon.IsMinimizing())
            {
                HideAnnotationPanel();
            }
            else
            {
                SyncPanelToIcon(currentFocusIcon);
            }
        }
    }

    void SyncPanelToIcon(AnnotationIcon icon)
    {
        if (annotationPanelRect != null)
        {
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            annotationPanelRect.anchoredPosition = iconRect.anchoredPosition + panelOffset;
        }
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        isVideoReady = true;
        videoDisplay.texture = videoPlayer.texture;
        videoPlayer.playbackSpeed = 1.0f;

        if (seekSlider) { seekSlider.minValue = 0; seekSlider.maxValue = (float)vp.length; }
        if (totalTimeText) totalTimeText.text = FormatTime(vp.length);

        SpawnTimelineMarkers();

        videoPlayer.time = 0;
        videoPlayer.Pause();
        UpdatePlayStateVisuals(false);
        SetAllIconsPaused(true);
    }

    public void OnVideoDisplayClicked()
    {
        if (!isVideoReady) return;
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            SetAllIconsPaused(true);
        }
        else
        {
            videoPlayer.Play();
            SetAllIconsPaused(false);
        }
    }

    void UpdatePlayStateVisuals(bool isPlaying)
    {
        if (playButtonOverlay) playButtonOverlay.SetActive(!isPlaying);
    }

    private void SetupVideoClickTrigger()
    {
        Button btn = videoDisplay.GetComponent<Button>();
        if (btn == null) btn = videoDisplay.gameObject.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.onClick.AddListener(OnVideoDisplayClicked);
    }

    private void CheckForAnnotationsOptimized()
    {
        float currentTime = (float)videoPlayer.time;

        while (nextAnnotationIndex < allAnnotations.Count)
        {
            AnnotationItem ann = allAnnotations[nextAnnotationIndex];

            if (currentTime > (ann.timestamp + TIMESTAMP_TOLERANCE))
            {
                nextAnnotationIndex++;
                continue;
            }

            if (currentTime >= (ann.timestamp - TIMESTAMP_TOLERANCE))
            {
                if (!triggeredTimestamps.Contains(ann.timestamp))
                {
                    triggeredTimestamps.Add(ann.timestamp);
                    SpawnAnnotationIcon(ann, false);
                }
                nextAnnotationIndex++;
            }
            else
            {
                break;
            }
        }
    }

    private void SpawnAnnotationIcon(AnnotationItem ann, bool forceMinimized)
    {
        if (!forceMinimized)
        {
            foreach (var existingIcon in activeIcons)
            {
                if (existingIcon.IsShowing()) existingIcon.ForceMinimizeImmediate();
            }
        }

        AnnotationIcon icon = GetIconFromPool();
        icon.displayDuration = iconDisplayDuration;

        float xPos = (ann.bbox_x - 0.5f) * videoDisplayRect.rect.width;
        float yPos = (ann.bbox_y - 0.5f) * videoDisplayRect.rect.height;
        Vector2 screenPos = new Vector2(xPos, yPos);
        Vector2 miniPos = GetMinimizedPosition(activeIcons.Count);

        icon.Initialize(ann, screenPos, miniPos, OnIconClick, OnIconTimeTravel, OnIconMinimize, forceMinimized);

        icon.SetPaused(!videoPlayer.isPlaying);

        activeIcons.Add(icon);

        if (!forceMinimized)
        {
            currentFocusIcon = icon;
            UpdateAnnotationPanel(ann.content.heading, ann.content.body);
            SyncPanelToIcon(icon);

            videoPlayer.Pause();
            UpdatePlayStateVisuals(false);
            SetAllIconsPaused(true);
        }
    }

    private void OnIconMinimize(AnnotationIcon icon)
    {
        if (currentFocusIcon == icon)
        {
            HideAnnotationPanel();
            currentFocusIcon = null;
        }
    }

    private void OnIconTimeTravel(AnnotationItem data, AnnotationIcon clickedIcon)
    {
        videoPlayer.time = data.timestamp;
        videoPlayer.Pause();
        UpdatePlayStateVisuals(false);
        SetAllIconsPaused(true);

        currentFocusIcon = clickedIcon;
        UpdateAnnotationPanel(data.content.heading, data.content.body);

        clickedIcon.Restore();
        ResetAnnotationIndex(data.timestamp);
        ClearFutureIcons(data.timestamp + 0.1f);
    }

    private void OnIconClick(AnnotationItem data, AnnotationIcon icon)
    {
        videoPlayer.Pause();
        SetAllIconsPaused(true);

        currentFocusIcon = icon;
        UpdateAnnotationPanel(data.content.heading, data.content.body);
        SyncPanelToIcon(icon);

        icon.SetShowingAnnotationsState();
    }

    private void SyncStateToTime(float targetTime)
    {
        ClearFutureIcons(targetTime);
        ResetAnnotationIndex(targetTime);
        HideAnnotationPanel();

        foreach (var ann in allAnnotations)
        {
            if (ann.timestamp < targetTime && !triggeredTimestamps.Contains(ann.timestamp))
            {
                triggeredTimestamps.Add(ann.timestamp);
                SpawnAnnotationIcon(ann, true);
            }
        }
        SetAllIconsPaused(!videoPlayer.isPlaying);
    }

    void ResetAnnotationIndex(float time)
    {
        nextAnnotationIndex = 0;
        for (int i = 0; i < allAnnotations.Count; i++)
        {
            if (allAnnotations[i].timestamp >= time) { nextAnnotationIndex = i; break; }
        }
        if (allAnnotations.Count > 0 && allAnnotations[allAnnotations.Count - 1].timestamp < time)
            nextAnnotationIndex = allAnnotations.Count;
    }

    void ClearFutureIcons(float timeThreshold)
    {
        for (int i = activeIcons.Count - 1; i >= 0; i--)
        {
            AnnotationIcon iconToCheck = activeIcons[i];
            if (iconToCheck.GetData().timestamp > timeThreshold)
            {
                triggeredTimestamps.Remove(iconToCheck.GetData().timestamp);
                ReturnIconToPool(iconToCheck);
                activeIcons.RemoveAt(i);
            }
        }
    }

    void SetAllIconsPaused(bool paused)
    {
        foreach (var icon in activeIcons) if (icon != null) icon.SetPaused(paused);
    }

    void UpdateAnnotationPanel(string title, string body)
    {
        annotationPanel.SetActive(true);
        if (headingText) headingText.text = title;
        if (bodyText) bodyText.text = body;

        if (annotationPanelRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(annotationPanelRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    void HideAnnotationPanel()
    {
        if (annotationPanel) annotationPanel.SetActive(false);
        currentFocusIcon = null;
    }

    void OnBackClicked() { SceneManager.LoadScene("menu"); }

    private Vector2 GetMinimizedPosition(int index)
    {
        float x = -(iconContainerRect.rect.width / 2f) + minimizedIconLeftOffset;
        float y = (iconContainerRect.rect.height / 2f) + minimizedIconTopOffset - (index * minimizedIconSpacing);
        return new Vector2(x, y);
    }

    private string FormatTime(double s) => $"{Mathf.FloorToInt((float)s / 60)}:{Mathf.FloorToInt((float)s % 60):00}";
    private void UpdateStatus(string msg) { if (statusText) statusText.text = msg; Debug.Log(msg); }

    private void SpawnTimelineMarkers()
    {
        if (timelineMarkerContainer == null || timelineMarkerPrefab == null || videoPlayer.length <= 0) return;
        foreach (Transform t in timelineMarkerContainer) Destroy(t.gameObject);
        foreach (var ann in allAnnotations)
        {
            GameObject m = Instantiate(timelineMarkerPrefab, timelineMarkerContainer);
            float norm = ann.timestamp / (float)videoPlayer.length;
            RectTransform rt = m.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(norm, 0);
            rt.anchorMax = new Vector2(norm, 1);
            rt.anchoredPosition = Vector2.zero;
            rt.offsetMin = new Vector2(rt.offsetMin.x, 0);
            rt.offsetMax = new Vector2(rt.offsetMax.x, 0);
        }
    }

    private void SetupSliderEventTriggers()
    {
        EventTrigger trigger = seekSlider.gameObject.GetComponent<EventTrigger>();
        if (!trigger) trigger = seekSlider.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry entryDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        entryDown.callback.AddListener((data) => isUserDraggingSlider = true);
        trigger.triggers.Add(entryDown);
        EventTrigger.Entry entryUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        entryUp.callback.AddListener((data) => {
            isUserDraggingSlider = false;
            videoPlayer.time = seekSlider.value;
            SyncStateToTime(seekSlider.value);
        });
        trigger.triggers.Add(entryUp);
    }

    private void OnSeekSliderValueChanged(float val) { if (isUserDraggingSlider) currentTimeText.text = FormatTime(val); }
}