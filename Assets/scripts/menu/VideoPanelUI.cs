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

    // Existing Full Name Text
    public TMP_Text userNameText;

    // 🔥 NEW: Drag the Text object for the First Letter (e.g., 'P') here
    public TMP_Text firstLetterText;

    public RawImage thumbnailDisplay;
    public Button cardButton;
    public Image colorStrip;

    // --- STATIC QUEUE ---
    private static Queue<VideoPanelUI> thumbnailQueue = new Queue<VideoPanelUI>();
    private static bool isLoaderBusy = false;
    private static MonoBehaviour activeSupervisor = null;

    private VideoItem myData;
    private System.Action<VideoItem> onClickCallback;
    public bool isWorkDone = false;
    private VideoPlayer currentVP;
    private RenderTexture currentRT;

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

    public void Setup(VideoItem data, System.Action<VideoItem> onClick)
    {
        myData = data;
        onClickCallback = onClick;

        if (titleText) titleText.text = data.title;
        if (subjectText) subjectText.text = data.subject;
        if (durationText) durationText.text = data.duration + " mins";

        // 🔥 Set User Data (Name & First Letter)
        if (data.user_data != null)
        {
            string fName = data.user_data.first_name ?? "";
            string lName = data.user_data.last_name ?? "";

            // Set Full Name
            if (userNameText != null)
            {
                userNameText.text = fName + " " + lName;
            }

            // 🔥 NEW: Set First Letter
            if (firstLetterText != null && !string.IsNullOrEmpty(fName))
            {
                firstLetterText.text = fName.Substring(0, 1).ToUpper();
            }
        }

        if (cardButton)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(() => onClickCallback?.Invoke(myData));
        }

        if (thumbnailDisplay)
        {
            thumbnailDisplay.texture = null;
            thumbnailDisplay.color = Color.gray;

            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(FixLayoutWithDelay());
            }

            AddToQueue(this);
        }
    }

    public void SetCardColor(Color c)
    {
        if (colorStrip) colorStrip.color = c;
    }

    // --- QUEUE SYSTEM (Unchanged) ---
    public static void ClearQueue()
    {
        thumbnailQueue.Clear();
        isLoaderBusy = false;
        activeSupervisor = null;
    }

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
            VideoPanelUI currentPanel = thumbnailQueue.Dequeue();

            if (currentPanel == null || currentPanel.gameObject == null || !currentPanel.gameObject.activeInHierarchy)
            {
                continue;
            }

            currentPanel.isWorkDone = false;
            currentPanel.StartCoroutine(currentPanel.GenerateFrameZero(currentPanel.myData.video_url));

            float timer = 0f;
            while (!currentPanel.isWorkDone && timer < 3.5f)
            {
                if (currentPanel == null) break;
                timer += Time.deltaTime;
                yield return null;
            }

            if (currentPanel != null && !currentPanel.isWorkDone)
            {
                currentPanel.ForceKill();
            }
        }

        isLoaderBusy = false;
        activeSupervisor = null;
    }

    // --- WORKER LOGIC (Unchanged) ---
    IEnumerator GenerateFrameZero(string videoUrl)
    {
        currentRT = RenderTexture.GetTemporary(320, 180, 16, RenderTextureFormat.ARGB32);

        currentVP = gameObject.AddComponent<VideoPlayer>();
        currentVP.url = videoUrl;
        currentVP.playOnAwake = false;
        currentVP.audioOutputMode = VideoAudioOutputMode.None;
        currentVP.renderMode = VideoRenderMode.RenderTexture;
        currentVP.targetTexture = currentRT;
        currentVP.aspectRatio = VideoAspectRatio.Stretch;

        currentVP.errorReceived += (s, m) => ForceKill();
        currentVP.Prepare();

        while (!currentVP.isPrepared)
        {
            if (isWorkDone) yield break;
            yield return null;
        }

        currentVP.Play();
        yield return new WaitForSeconds(0.25f);
        currentVP.Pause();
        yield return new WaitForEndOfFrame();

        if (currentRT != null && thumbnailDisplay != null)
        {
            Texture2D snapshot = new Texture2D(currentRT.width, currentRT.height, TextureFormat.RGB24, false);
            RenderTexture.active = currentRT;
            snapshot.ReadPixels(new Rect(0, 0, currentRT.width, currentRT.height), 0, 0);
            snapshot.Apply();
            RenderTexture.active = null;

            thumbnailDisplay.texture = snapshot;
            thumbnailDisplay.color = Color.white;
        }
        CleanUp();
        isWorkDone = true;
    }

    public void ForceKill()
    {
        CleanUp();
        if (thumbnailDisplay != null && thumbnailDisplay.texture == null) thumbnailDisplay.color = Color.black;
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
            if (parentRect != null && parentRect.rect.width > 0) rt.sizeDelta = parentRect.rect.size;
        }
    }
}