using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class ProfilePanelManager : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject mainProfilePanel;
    public GameObject editProfilePanel;
    public GameObject homePanel;

    [Header("Profile Header UI")]
    public TMP_Text userNameText;
    public TMP_Text designationText;
    public TMP_Text tagLineText;
    public RawImage profileImageDisplay;

    [Header("Buttons")]
    public Button editProfileButton;
    public Button closeProfileButton;

    [Header("Profile Tabs")]
    public Button btnUploads;
    public Button btnAnalytics;
    public Button btnDrafts;

    [Header("Content Areas")]
    public Transform profileVideoContainer;
    public GameObject videoCardPrefab;
    public GameObject analyticsContentPanel;
    public TMP_Text totalVideosText;

    [Header("Visual Settings")]
    private Color colorSelectedBG = Color.white;
    private Color colorUnselectedBG = new Color32(255, 255, 255, 255);
    private Color colorSelectedText = Color.white;
    private Color colorUnselectedText = new Color32(135, 135, 135, 255);

    private enum Tab { Uploads, Analytics, Drafts }

    private UserData currentUserData;

    void OnEnable()
    {
        SwitchTab(Tab.Uploads);
    }

    void Start()
    {
        if (btnUploads) btnUploads.onClick.AddListener(() => SwitchTab(Tab.Uploads));
        if (btnAnalytics) btnAnalytics.onClick.AddListener(() => SwitchTab(Tab.Analytics));
        if (btnDrafts) btnDrafts.onClick.AddListener(() => SwitchTab(Tab.Drafts));

        if (editProfileButton) editProfileButton.onClick.AddListener(OnEditClicked);
        if (closeProfileButton) closeProfileButton.onClick.AddListener(OnCloseClicked);
    }

    // --- 🔥 CORE FIX IS HERE ---
    void OnEditClicked()
    {
        Debug.Log("🖱️ Edit Button Clicked");

        // 1. Activate the Edit Panel FIRST
        // This ensures the GameObject is active so Coroutines can start.
        if (editProfilePanel != null)
        {
            editProfilePanel.SetActive(true);

            // 2. NOW pass the data
            EditProfile editScript = editProfilePanel.GetComponent<EditProfile>();

            if (editScript != null && currentUserData != null)
            {
                editScript.SetupInputs(currentUserData);
                Debug.Log("📤 Data sent to EditProfile script.");
            }
        }

        // 3. Close this panel (Optional: You can keep it open in BG if you want)
        if (mainProfilePanel != null) mainProfilePanel.SetActive(false);
    }

    void OnCloseClicked()
    {
        if (mainProfilePanel != null) mainProfilePanel.SetActive(false);
        if (homePanel != null) homePanel.SetActive(true);
    }

    // --- Tab & Grid Logic (Unchanged) ---
    void SwitchTab(Tab tab)
    {
        UpdateTabVisual(btnUploads, tab == Tab.Uploads);
        UpdateTabVisual(btnAnalytics, tab == Tab.Analytics);
        UpdateTabVisual(btnDrafts, tab == Tab.Drafts);

        if (tab == Tab.Uploads)
        {
            if (profileVideoContainer && profileVideoContainer.parent && profileVideoContainer.parent.parent)
                profileVideoContainer.parent.parent.gameObject.SetActive(true);
            if (analyticsContentPanel) analyticsContentPanel.SetActive(false);
            PopulateGrid();
        }
        else
        {
            if (profileVideoContainer && profileVideoContainer.parent && profileVideoContainer.parent.parent)
                profileVideoContainer.parent.parent.gameObject.SetActive(false);
            if (analyticsContentPanel) analyticsContentPanel.SetActive(false);
        }
    }

    void UpdateTabVisual(Button btn, bool isSelected)
    {
        if (btn == null) return;
        if (btn.image != null) btn.image.color = isSelected ? colorSelectedBG : colorUnselectedBG;
        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.color = isSelected ? colorSelectedText : colorUnselectedText;
        Transform imageObj = btn.transform.Find("myImage");
        if (imageObj != null) imageObj.gameObject.SetActive(isSelected);
        else
        {
            Image[] images = btn.GetComponentsInChildren<Image>();
            foreach (var img in images) { if (img.gameObject != btn.gameObject) { img.gameObject.SetActive(isSelected); break; } }
        }
    }

    void PopulateGrid()
    {
        VideoPanelUI.ClearQueue();
        foreach (Transform child in profileVideoContainer) Destroy(child.gameObject);

        List<VideoItem> videos = null;
        if (ClientDashboardManager.Instance != null)
        {
            videos = ClientDashboardManager.Instance.CachedVideos;
        }

        if (totalVideosText != null)
        {
            int count = (videos != null) ? videos.Count : 0;
            totalVideosText.text = count + "";
        }

        // Capture Data
        if (videos != null && videos.Count > 0 && videos[0].user_data != null)
        {
            currentUserData = videos[0].user_data;
            UpdateProfileHeader(currentUserData);
        }

        if (videos != null)
        {
            foreach (VideoItem vid in videos)
            {
                GameObject obj = Instantiate(videoCardPrefab, profileVideoContainer);
                obj.SetActive(true);
                VideoPanelUI ui = obj.GetComponent<VideoPanelUI>();
                if (ui != null)
                {
                    ui.Setup(vid, (v) => {
                        AppSession.CurrentVideo = v;
                        UnityEngine.SceneManagement.SceneManager.LoadScene("VideoPlayerScene");
                    });
                    ui.SetCardColor(GetSubjectColor(vid.subject));
                }
            }
        }
    }

    void UpdateProfileHeader(UserData data)
    {
        if (userNameText) userNameText.text = data.first_name + " " + data.last_name;
        if (designationText) designationText.text = data.designation;
        if (tagLineText) tagLineText.text = data.tag_line;

        if (profileImageDisplay != null && !string.IsNullOrEmpty(data.profile_pic))
        {
            StartCoroutine(LoadProfileImage(data.profile_pic));
        }
    }

    IEnumerator LoadProfileImage(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (profileImageDisplay != null)
                {
                    profileImageDisplay.texture = texture;
                    profileImageDisplay.color = Color.white;
                }
            }
        }
    }

    Color GetSubjectColor(string subject)
    {
        switch (subject.ToLower())
        {
            case "physics": return new Color(0.2f, 0.4f, 0.9f);
            case "biology": return new Color(0.1f, 0.7f, 0.4f);
            case "chemistry": return new Color(0.9f, 0.4f, 0.1f);
            case "tech": return new Color(0.2f, 0.6f, 0.8f);
            default: return new Color(0.2f, 0.2f, 0.2f);
        }
    }
}