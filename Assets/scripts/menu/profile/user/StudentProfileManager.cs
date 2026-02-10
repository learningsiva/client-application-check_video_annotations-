using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class StudentProfileManager : MonoBehaviour
{
    [Header("Main Panels")]
    public GameObject mainProfilePanel;
    public GameObject homePanel;

    [Header("Profile Header UI")]
    public TMP_Text userNameText;
    public TMP_Text designationText; // Students might not have this, can hide or use for "Student" title
    public TMP_Text tagLineText;     // Optional for students
    public RawImage profileImageDisplay;

    [Header("Buttons")]
    public Button closeProfileButton;
    public Button logoutButton; // Students definitely need a logout

    [Header("Profile Tabs")]
    public Button btnHistory;
    public Button btnWatchLater;
    public Button btnLiked;

    [Header("Tab Visuals")]
    public Color colorSelectedBG = Color.white;
    public Color colorUnselectedBG = new Color32(255, 255, 255, 100);
    public Color colorSelectedText = Color.black; // Adjust based on your UI theme
    public Color colorUnselectedText = Color.gray;

    [Header("Content Areas")]
    public Transform videoGridContainer;
    public GameObject videoCardPrefab;
    public GameObject loadingSpinner;
    public TMP_Text emptyStateText; // Text to show if list is empty (e.g., "No liked videos yet")

    // API Endpoints (You might need to ask your backend dev for these specific endpoints)
    // For now, I will use placeholders.
    private string apiHistory = "https://botclub.conbig.com/api/v1/get_watch_history";
    private string apiWatchLater = "https://botclub.conbig.com/api/v1/get_watch_later_videos";
    private string apiLiked = "https://botclub.conbig.com/api/v1/get_liked_videos";

    private enum StudentTab { History, WatchLater, Liked }
    private StudentTab currentTab = StudentTab.History;

    void OnEnable()
    {
        // Load default tab when panel opens
        SwitchTab(StudentTab.History);
        LoadUserProfile();
    }

    void Start()
    {
        if (btnHistory) btnHistory.onClick.AddListener(() => SwitchTab(StudentTab.History));
        if (btnWatchLater) btnWatchLater.onClick.AddListener(() => SwitchTab(StudentTab.WatchLater));
        if (btnLiked) btnLiked.onClick.AddListener(() => SwitchTab(StudentTab.Liked));

        if (closeProfileButton) closeProfileButton.onClick.AddListener(OnCloseClicked);
        if (logoutButton) logoutButton.onClick.AddListener(OnLogoutClicked);
    }

    void OnCloseClicked()
    {
        if (mainProfilePanel != null) mainProfilePanel.SetActive(false);
        if (homePanel != null) homePanel.SetActive(true);
    }

    void OnLogoutClicked()
    {
        PlayerPrefs.DeleteAll();
        UnityEngine.SceneManagement.SceneManager.LoadScene("LoginScene"); // Replace with your actual Login scene name
    }

    // --- Tab Logic ---
    void SwitchTab(StudentTab tab)
    {
        currentTab = tab;

        // Update Visuals
        UpdateTabVisual(btnHistory, tab == StudentTab.History);
        UpdateTabVisual(btnWatchLater, tab == StudentTab.WatchLater);
        UpdateTabVisual(btnLiked, tab == StudentTab.Liked);

        // Fetch Data
        string url = "";
        switch (tab)
        {
            case StudentTab.History: url = apiHistory; break;
            case StudentTab.WatchLater: url = apiWatchLater; break;
            case StudentTab.Liked: url = apiLiked; break;
        }

        StartCoroutine(FetchVideos(url));
    }

    void UpdateTabVisual(Button btn, bool isSelected)
    {
        if (btn == null) return;
        if (btn.image != null) btn.image.color = isSelected ? colorSelectedBG : colorUnselectedBG;

        TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.color = isSelected ? colorSelectedText : colorUnselectedText;
    }

    // --- Data Fetching ---
    IEnumerator FetchVideos(string url)
    {
        if (loadingSpinner) loadingSpinner.SetActive(true);
        if (emptyStateText) emptyStateText.gameObject.SetActive(false);

        // Clear existing items
        foreach (Transform child in videoGridContainer) Destroy(child.gameObject);

        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // Note: Assuming the response format is similar to the Dashboard (Root -> Data -> Videos String)
                // If the API structure is different for these endpoints, we might need to adjust parsing.
                ProcessVideoResponse(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"Failed to fetch {currentTab}: {request.error}");
                if (emptyStateText)
                {
                    emptyStateText.text = "Could not load videos.";
                    emptyStateText.gameObject.SetActive(true);
                }
            }
        }

        if (loadingSpinner) loadingSpinner.SetActive(false);
    }

    void ProcessVideoResponse(string json)
    {
        try
        {
            // Reuse your existing JsonHelper logic
            // Note: This parsing depends heavily on how your backend sends the list. 
            // If it sends a direct array [ ... ], use JsonHelper.
            // If it sends { "data": [ ... ] }, parse root first.

            // Assuming standard format for now:
            APIResponseRoot root = JsonUtility.FromJson<APIResponseRoot>(json);

            List<VideoItem> videos = new List<VideoItem>();

            if (root != null && root.data != null && !string.IsNullOrEmpty(root.data.videos))
            {
                string cleanJson = root.data.videos;
                if (cleanJson.StartsWith("\"")) cleanJson = cleanJson.Trim('"').Replace("\\\"", "\"");
                videos = JsonHelper.FromJson<VideoItem>(cleanJson);
            }

            if (videos == null || videos.Count == 0)
            {
                if (emptyStateText)
                {
                    emptyStateText.text = "No videos found.";
                    emptyStateText.gameObject.SetActive(true);
                }
                return;
            }

            foreach (VideoItem vid in videos)
            {
                GameObject obj = Instantiate(videoCardPrefab, videoGridContainer);
                VideoPanelUI ui = obj.GetComponent<VideoPanelUI>();
                if (ui != null)
                {
                    ui.Setup(vid, (v) => {
                        AppSession.CurrentVideo = v;
                        UnityEngine.SceneManagement.SceneManager.LoadScene("VideoPlayerScene");
                    });
                    // ui.SetCardColor(...) // Optional if you want colors
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error parsing student videos: " + e.Message);
        }
    }

    void LoadUserProfile()
    {
        // For Students, we might just use the cached data from the Dashboard 
        // OR fetch a specific profile endpoint. 
        // For now, let's look for cached data in ClientDashboardManager if available.

        if (ClientDashboardManager.Instance != null && ClientDashboardManager.Instance.CachedVideos.Count > 0)
        {
            // Just grab the first video's user data as a placeholder? 
            // NO, that would show the Professor's face!
            // We need a specific "Get My Profile" endpoint for students.
            // Since we don't have that yet, I will leave this blank or load a default.
        }

        // Placeholder text
        if (userNameText) userNameText.text = "Student";
    }
}