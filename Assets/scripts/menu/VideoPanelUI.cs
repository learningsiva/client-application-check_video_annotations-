using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class VideoPanelUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text titleText;
    public TMP_Text subjectText;
    public TMP_Text durationText;
    public TMP_Text userNameText;
    public TMP_Text firstLetterText;

    public RawImage thumbnailDisplay;
    public Button cardButton;
    public Image colorStrip;

    [Header("Placeholder shown while thumbnail generates")]
    [Tooltip("Assign a generic thumbnail sprite in the prefab — shown instantly while real thumbnail loads")]
    public Texture placeholderTexture;

    // --- STATIC THUMBNAIL CACHE ---
    private static Dictionary<string, Texture2D> frameCache = new Dictionary<string, Texture2D>();

    // --- STATIC QUEUE (one VideoPlayer at a time — safe for Android) ---
    private static Queue<VideoPanelUI> thumbnailQueue = new Queue<VideoPanelUI>();
    private static bool isLoaderBusy = false;
    private static MonoBehaviour activeSupervisor = null;

    private VideoItem myData;
    private System.Action<VideoItem> onClickCallback;
    public bool isWorkDone = false;
    private VideoPlayer currentVP;
    private RenderTexture currentRT;

    private Texture2D ownedSnapshot = null;

    // ---------------------------------------------------------------

    void OnEnable()
    {
        if (thumbnailDisplay != null) StartCoroutine(FixLayoutWithDelay());
    }

    void OnDestroy()
    {
        CleanUp();
        if (activeSupervisor == this)
        {
            isLoaderBusy = false;
            activeSupervisor = null;
        }
    }

    public static void ClearCache()
    {
        foreach (var tex in frameCache.Values)
            if (tex != null) Object.Destroy(tex);
        frameCache.Clear();
    }

    public static void ClearQueue()
    {
        thumbnailQueue.Clear();
        isLoaderBusy = false;
        activeSupervisor = null;
    }

    // ✅ ONLY ADDITION — exposes frameCache so VideoReplayManager can show
    // an instant thumbnail on scene open without any other script changes.
    public static Texture2D GetCachedFrame(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl)) return null;
        frameCache.TryGetValue(videoUrl, out Texture2D tex);
        return tex;
    }

    // ---------------------------------------------------------------

    public void Setup(VideoItem data, System.Action<VideoItem> onClick)
    {
        myData = data;
        onClickCallback = onClick;

        if (titleText) titleText.text = data.title ?? "";
        if (subjectText) subjectText.text = data.subject ?? "";
        if (durationText) durationText.text = (data.duration ?? "") + " mins";

        if (data.user_data != null)
        {
            string fName = data.user_data.first_name ?? "";
            string lName = data.user_data.last_name ?? "";

            if (userNameText != null)
                userNameText.text = (fName + " " + lName).Trim();

            if (firstLetterText != null && !string.IsNullOrEmpty(fName))
                firstLetterText.text = fName.Substring(0, 1).ToUpper();
        }

        if (cardButton)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onClickCallback?.Invoke(myData));
        }

        if (thumbnailDisplay)
        {
            string url = data.video_url ?? "";

            if (!string.IsNullOrEmpty(url) && frameCache.ContainsKey(url))
            {
                thumbnailDisplay.texture = frameCache[url];
                thumbnailDisplay.color = Color.white;
                isWorkDone = true;
            }
            else
            {
                thumbnailDisplay.texture = placeholderTexture;
                thumbnailDisplay.color = placeholderTexture != null ? Color.white : new Color(0.15f, 0.15f, 0.15f);

                isWorkDone = false;
                AddToQueue(this);
            }

            if (gameObject.activeInHierarchy)
                StartCoroutine(FixLayoutWithDelay());
        }
    }

    public void SetCardColor(Color c)
    {
        if (colorStrip) colorStrip.color = c;
    }

    // ---------------------------------------------------------------

    private void AddToQueue(VideoPanelUI panel)
    {
        thumbnailQueue.Enqueue(panel);

        if ((!isLoaderBusy || activeSupervisor == null) && this.gameObject.activeInHierarchy)
        {
            activeSupervisor = this;
            StartCoroutine(QueueSupervisor());
        }
    }

    private static IEnumerator QueueSupervisor()
    {
        isLoaderBusy = true;

        while (thumbnailQueue.Count > 0)
        {
            VideoPanelUI panel = thumbnailQueue.Dequeue();

            if (panel == null || panel.gameObject == null
                || !panel.gameObject.activeInHierarchy
                || panel.isWorkDone)
            {
                continue;
            }

            panel.isWorkDone = false;
            panel.StartCoroutine(panel.GenerateFrameZero(panel.myData.video_url));

            float timer = 0f;
            while (!panel.isWorkDone && timer < 5f)
            {
                if (panel == null) break;
                timer += Time.deltaTime;
                yield return null;
            }

            if (panel != null && !panel.isWorkDone)
            {
                Debug.LogWarning($"Thumbnail timeout for: {panel.myData?.video_url}");
                panel.ForceKill();
            }

            yield return new WaitForSeconds(0.1f);
        }

        isLoaderBusy = false;
        activeSupervisor = null;
    }

    // ---------------------------------------------------------------

    IEnumerator GenerateFrameZero(string videoUrl)
    {
        if (string.IsNullOrEmpty(videoUrl))
        {
            ForceKill();
            yield break;
        }

        currentRT = RenderTexture.GetTemporary(320, 180, 16, RenderTextureFormat.ARGB32);

        currentVP = gameObject.AddComponent<VideoPlayer>();
        currentVP.url = videoUrl;
        currentVP.playOnAwake = false;
        currentVP.audioOutputMode = VideoAudioOutputMode.None;
        currentVP.renderMode = VideoRenderMode.RenderTexture;
        currentVP.targetTexture = currentRT;
        currentVP.aspectRatio = VideoAspectRatio.Stretch;

        bool errorOccurred = false;
        currentVP.errorReceived += (s, m) =>
        {
            Debug.LogWarning($"VideoPlayer error [{videoUrl}]: {m}");
            errorOccurred = true;
        };

        currentVP.Prepare();

        while (!currentVP.isPrepared)
        {
            if (isWorkDone || errorOccurred) { ForceKill(); yield break; }
            yield return null;
        }

        currentVP.Play();
        yield return new WaitForSeconds(0.25f);
        currentVP.Pause();
        yield return new WaitForEndOfFrame();

        if (currentRT != null && thumbnailDisplay != null && !isWorkDone)
        {
            Texture2D snapshot = new Texture2D(currentRT.width, currentRT.height, TextureFormat.RGB24, false);
            RenderTexture.active = currentRT;
            snapshot.ReadPixels(new Rect(0, 0, currentRT.width, currentRT.height), 0, 0);
            snapshot.Apply();
            RenderTexture.active = null;

            if (!frameCache.ContainsKey(videoUrl))
                frameCache[videoUrl] = snapshot;
            else
                Destroy(snapshot);

            thumbnailDisplay.texture = frameCache[videoUrl];
            thumbnailDisplay.color = Color.white;
        }

        CleanUp();
        isWorkDone = true;
    }

    public void ForceKill()
    {
        CleanUp();
        if (thumbnailDisplay != null && thumbnailDisplay.texture == null)
        {
            thumbnailDisplay.texture = placeholderTexture;
            thumbnailDisplay.color = placeholderTexture != null ? Color.white : new Color(0.15f, 0.15f, 0.15f);
        }
        isWorkDone = true;
    }

    void CleanUp()
    {
        if (currentVP != null) { Destroy(currentVP); currentVP = null; }
        if (currentRT != null) { RenderTexture.ReleaseTemporary(currentRT); currentRT = null; }
    }

    IEnumerator FixLayoutWithDelay()
    {
        yield return new WaitForEndOfFrame();
        FixThumbnailLayout();
    }

    void FixThumbnailLayout()
    {
        if (thumbnailDisplay == null) return;

        LayoutElement le = thumbnailDisplay.GetComponent<LayoutElement>();
        if (le == null) le = thumbnailDisplay.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        RectTransform rt = thumbnailDisplay.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        if (thumbnailDisplay.transform.parent != null)
        {
            RectTransform parentRect = thumbnailDisplay.transform.parent.GetComponent<RectTransform>();
            if (parentRect != null && parentRect.rect.width > 0)
                rt.sizeDelta = parentRect.rect.size;
        }
    }
}