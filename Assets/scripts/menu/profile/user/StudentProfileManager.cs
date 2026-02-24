using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class StudentProfileManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  PANELS
    // ─────────────────────────────────────────────

    [Header("Main Panels")]
    public GameObject mainProfilePanel;
    public GameObject homePanel;
    public GameObject editProfilePanel;

    // ─────────────────────────────────────────────
    //  PROFILE HEADER
    // ─────────────────────────────────────────────

    [Header("Profile Header UI")]
    public RawImage profileImageDisplay;
    public TMP_Text userNameText;
    public TMP_Text taglineText;
    public TMP_Text bioText;

    // ─────────────────────────────────────────────
    //  ACTION BUTTONS
    // ─────────────────────────────────────────────

    [Header("Action Buttons")]
    public Button closeProfileButton;

    // ─────────────────────────────────────────────
    //  TAB BUTTONS
    // ─────────────────────────────────────────────

    [Header("Tab Buttons")]
    public Button btnHistory;
    public Button btnWatchLater;
    public Button btnEditProfile;

    // ─────────────────────────────────────────────
    //  VIDEO GRID
    // ─────────────────────────────────────────────

    [Header("Video Grid")]
    public Transform videoGridContainer;
    public GameObject videoCardPrefab;
    public GameObject loadingSpinner;
    public TMP_Text emptyStateText;

    [Header("Scene Settings")]
    public string videoPlayerSceneName = "VideoPlayerScene";

    // ─────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────

    private const string ProfileApiUrl = "https://botclub.conbig.com/api/v1/get_user_profile";

    private enum Tab { History, WatchLater, EditProfile }
    private Tab _currentTab = Tab.History;

    private List<WatchHistoryItem> _cachedHistory = new List<WatchHistoryItem>();
    private List<WatchLaterItem> _cachedWatchLater = new List<WatchLaterItem>();
    private bool _dataLoaded = false;

    // Stored after API parse — passed to edit panel the same way
    // ProfilePanelManager passes currentUserData to EditProfile
    private ProfileData _currentProfileData = null;

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Start()
    {
        if (btnHistory) btnHistory.onClick.AddListener(() => SwitchTab(Tab.History));
        if (btnWatchLater) btnWatchLater.onClick.AddListener(() => SwitchTab(Tab.WatchLater));
        if (btnEditProfile) btnEditProfile.onClick.AddListener(OnEditProfileClicked);

        if (closeProfileButton) closeProfileButton.onClick.AddListener(OnCloseClicked);
    }

    void OnEnable()
    {
        _dataLoaded = false;
        VideoPanelUI.ClearQueue();
        StartCoroutine(FetchAndDisplayProfile());
    }

    void OnDisable()
    {
        VideoPanelUI.ClearQueue();
    }

    // ─────────────────────────────────────────────
    //  API  —  GET /get_user_profile
    // ─────────────────────────────────────────────

    IEnumerator FetchAndDisplayProfile()
    {
        ShowLoading(true);
        ClearGrid();

        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");

        using (UnityWebRequest request = UnityWebRequest.Get(ProfileApiUrl))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            Debug.Log("[StudentProfile] Response Code : " + request.responseCode);
            Debug.Log("[StudentProfile] RAW RESPONSE  : " + request.downloadHandler.text);

            if (request.result == UnityWebRequest.Result.Success)
                ParseAndCacheProfile(request.downloadHandler.text);
            else
            {
                Debug.LogError("[StudentProfile] Fetch failed: " + request.error);
                ShowEmptyState("Could not load profile. Please try again.");
            }
        }

        ShowLoading(false);
    }

    void ParseAndCacheProfile(string json)
    {
        try
        {
            StudentProfileResponse response = JsonUtility.FromJson<StudentProfileResponse>(json);

            if (response == null || response.profile_data == null)
            {
                ShowEmptyState("No profile data found.");
                return;
            }

            ProfileData data = response.profile_data;
            _currentProfileData = data; // Store for edit panel

            // ── Full name
            if (userNameText)
            {
                string firstName = data.first_name ?? "";
                string lastName = data.last_name ?? "";
                string fullName = (firstName + " " + lastName).Trim();
                userNameText.text = string.IsNullOrEmpty(fullName) ? "Student" : fullName;
            }

            // ── Tag line
            if (taglineText)
                taglineText.text = data.tag_line ?? "";

            // ── Bio
            if (bioText)
                bioText.text = data.bio ?? "";

            // ── Profile picture
            if (profileImageDisplay != null && !string.IsNullOrEmpty(data.profile_pic))
                StartCoroutine(LoadProfileImage(data.profile_pic));

            // ── Cache lists
            _cachedHistory = data.watch_history ?? new List<WatchHistoryItem>();
            _cachedWatchLater = data.watch_later ?? new List<WatchLaterItem>();
            _dataLoaded = true;

            Debug.Log($"[StudentProfile] Loaded — History: {_cachedHistory.Count} | WatchLater: {_cachedWatchLater.Count}");

            SwitchTab(_currentTab);
        }
        catch (Exception e)
        {
            Debug.LogError("[StudentProfile] Parse error: " + e.Message);
            ShowEmptyState("Error loading profile.");
        }
    }

    // ─────────────────────────────────────────────
    //  PROFILE IMAGE LOADER
    // ─────────────────────────────────────────────

    IEnumerator LoadProfileImage(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(request);
                if (profileImageDisplay != null)
                {
                    profileImageDisplay.texture = tex;
                    profileImageDisplay.color = Color.white;
                }
                Debug.Log("[StudentProfile] ✅ Profile picture loaded.");
            }
            else
            {
                Debug.LogWarning("[StudentProfile] Could not load profile picture: " + request.error);
            }
        }
    }

    // ─────────────────────────────────────────────
    //  TAB SWITCHING
    // ─────────────────────────────────────────────

    void SwitchTab(Tab tab)
    {
        _currentTab = tab;

        // Highlight selected tab white, other gray
        SetTabHighlight(btnHistory, tab == Tab.History);
        SetTabHighlight(btnWatchLater, tab == Tab.WatchLater);

        // EditProfile tab is handled by OnEditProfileClicked directly — not here

        SetVideoAreaVisible(true);

        if (!_dataLoaded) return;

        VideoPanelUI.ClearQueue();
        ClearGrid();

        if (tab == Tab.History)
            PopulateHistory();
        else
            PopulateWatchLater();
    }

    // White = selected, Gray = unselected
    void SetTabHighlight(Button btn, bool isSelected)
    {
        if (btn == null) return;

        // Button background color
        if (btn.image != null)
            btn.image.color = isSelected ? Color.gray : Color.white;

        // Button label color — flip so text is readable on both backgrounds
        TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
        if (label != null)
            label.color = isSelected ? Color.white : Color.gray;
    }

    // ─────────────────────────────────────────────
    //  WATCH HISTORY TAB
    // ─────────────────────────────────────────────

    void PopulateHistory()
    {
        if (_cachedHistory == null || _cachedHistory.Count == 0)
        {
            ShowEmptyState("No watch history yet.");
            return;
        }

        HideEmptyState();

        foreach (WatchHistoryItem item in _cachedHistory)
        {
            VideoItem vi = BuildVideoItem(
                taskId: item.video_id,
                videoName: item.video_name,
                videoUrl: item.video_url,
                subtitle: FormatDate(item.visited_at),
                subject: GetSubjectFromCache(item.video_url)
            );
            SpawnCard(vi);
        }
    }

    // ─────────────────────────────────────────────
    //  WATCH LATER TAB
    // ─────────────────────────────────────────────

    void PopulateWatchLater()
    {
        if (_cachedWatchLater == null || _cachedWatchLater.Count == 0)
        {
            ShowEmptyState("No watch later videos yet.");
            return;
        }

        HideEmptyState();

        foreach (WatchLaterItem item in _cachedWatchLater)
        {
            VideoItem vi = BuildVideoItem(
                taskId: item.video_id,
                videoName: item.video_name,
                videoUrl: item.video_url,
                subtitle: "",
                subject: GetSubjectFromCache(item.video_url)
            );
            SpawnCard(vi);
        }
    }

    // ─────────────────────────────────────────────
    //  SHARED CARD BUILDER
    // ─────────────────────────────────────────────

    VideoItem BuildVideoItem(int taskId, string videoName, string videoUrl, string subtitle, string subject = "")
    {
        return new VideoItem
        {
            task_id = taskId,
            title = videoName ?? "Untitled",
            video_url = videoUrl ?? "",
            subject = subject ?? "",
            duration = subtitle ?? "",
            user_data = null
        };
    }

    // Looks up subject from dashboard cache by video_url
    string GetSubjectFromCache(string videoUrl)
    {
        List<VideoItem> cached = ClientDashboardManager.GlobalCache;
        if (cached == null) return "";
        VideoItem found = cached.Find(v => v.video_url == videoUrl);
        return found?.subject ?? "";
    }

    void SpawnCard(VideoItem vi)
    {
        if (videoCardPrefab == null || videoGridContainer == null) return;

        GameObject card = Instantiate(videoCardPrefab, videoGridContainer);
        VideoPanelUI ui = card.GetComponent<VideoPanelUI>();

        if (ui != null)
            ui.Setup(vi, OnVideoCardClicked);
        else
            Debug.LogWarning("[StudentProfile] videoCardPrefab is missing a VideoPanelUI component!");
    }

    void OnVideoCardClicked(VideoItem clickedVideo)
    {
        // FIX: Annotations are NOT in the profile API response — they only exist
        // in the dashboard cache. Look up the full VideoItem by task_id so
        // VideoReplayManager gets complete annotation data.
        VideoItem fullVideo = FindInDashboardCache(clickedVideo.video_url);

        if (fullVideo != null)
        {
            Debug.Log($"[StudentProfile] ✅ Found full video data in dashboard cache for task_id {clickedVideo.task_id} — annotations included.");
            AppSession.CurrentVideo = fullVideo;
        }
        else
        {
            // Not in dashboard cache (user came directly to profile without browsing dashboard).
            // Annotations will be empty — video still plays, just no annotation overlays.
            Debug.LogWarning($"[StudentProfile] ⚠️ task_id {clickedVideo.task_id} not in dashboard cache — annotations unavailable.");
            AppSession.CurrentVideo = clickedVideo;
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(videoPlayerSceneName);
    }

    /// <summary>
    /// Searches ClientDashboardManager's cached video list for a matching task_id.
    /// Returns the full VideoItem (with annotations) if found, null otherwise.
    /// </summary>
    // Match by video_url — both profile API and dashboard API return identical URLs
    // for the same video, making it the only reliable shared key between the two APIs.
    // task_id vs video_id are different numbering systems and cannot be compared.
    VideoItem FindInDashboardCache(string videoUrl)
    {
        List<VideoItem> cached = ClientDashboardManager.GlobalCache;

        if (cached == null || cached.Count == 0)
        {
            Debug.LogWarning("[StudentProfile] Dashboard cache is empty — annotations unavailable.");
            return null;
        }

        VideoItem found = cached.Find(v => v.video_url == videoUrl);

        if (found == null)
            Debug.LogWarning($"[StudentProfile] video_url not found in dashboard cache ({cached.Count} videos cached). Annotations unavailable.");
        else
            Debug.Log($"[StudentProfile] ✅ Found by video_url in cache — {found.annotations?.Count ?? 0} annotations loaded.");

        return found;
    }

    // ─────────────────────────────────────────────
    //  EDIT PROFILE PANEL
    // ─────────────────────────────────────────────

    void OnEditProfileClicked()
    {
        Debug.Log("[StudentProfile] Edit Profile clicked.");

        if (editProfilePanel == null)
        {
            Debug.LogWarning("[StudentProfile] editProfilePanel is not assigned in the Inspector.");
            return;
        }

        // Step 1: Activate panel FIRST so its Coroutines and components are live
        editProfilePanel.SetActive(true);

        // Step 2: Pass current profile data to the edit script
        StudentEditProfile editScript = editProfilePanel.GetComponent<StudentEditProfile>();
        if (editScript != null && _currentProfileData != null)
        {
            editScript.SetupInputs(_currentProfileData);
            Debug.Log("[StudentProfile] 📤 Profile data sent to StudentEditProfile.");
        }
        else
        {
            if (editScript == null)
                Debug.LogWarning("[StudentProfile] StudentEditProfile component not found on editProfilePanel.");
            if (_currentProfileData == null)
                Debug.LogWarning("[StudentProfile] _currentProfileData is null — profile not yet loaded.");
        }

        // Step 3: Hide this panel
        if (mainProfilePanel != null) mainProfilePanel.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  ACTION BUTTONS
    // ─────────────────────────────────────────────

    void OnCloseClicked()
    {
        if (mainProfilePanel) mainProfilePanel.SetActive(false);
        if (homePanel) homePanel.SetActive(true);
    }

    // ─────────────────────────────────────────────
    //  UI HELPERS
    // ─────────────────────────────────────────────

    void ClearGrid()
    {
        if (videoGridContainer == null) return;
        foreach (Transform child in videoGridContainer)
            Destroy(child.gameObject);
    }

    void SetVideoAreaVisible(bool visible)
    {
        if (videoGridContainer) videoGridContainer.gameObject.SetActive(visible);
        if (!visible) HideEmptyState();
    }

    void ShowLoading(bool show)
    {
        if (loadingSpinner) loadingSpinner.SetActive(show);
    }

    void ShowEmptyState(string message)
    {
        if (emptyStateText)
        {
            emptyStateText.text = message;
            emptyStateText.gameObject.SetActive(true);
        }
    }

    void HideEmptyState()
    {
        if (emptyStateText)
            emptyStateText.gameObject.SetActive(false);
    }

    string FormatDate(string iso)
    {
        if (string.IsNullOrEmpty(iso)) return "";
        try
        {
            DateTime dt = DateTime.Parse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind);
            return dt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
        }
        catch { return iso; }
    }
}

// ─────────────────────────────────────────────
//  API RESPONSE DATA CLASSES
// ─────────────────────────────────────────────

[Serializable]
public class StudentProfileResponse
{
    public string message;
    public ProfileData profile_data;
}

[Serializable]
public class ProfileData
{
    public string first_name;
    public string last_name;
    public string user_name;
    public string email;
    public string phone;
    public string profile_pic;
    public string tag_line;
    public string designation;
    public string subject;
    public string bio;
    public List<WatchLaterItem> watch_later;
    public List<WatchHistoryItem> watch_history;
}

[Serializable]
public class WatchLaterItem
{
    public int video_id;
    public string video_name;
    public string video_url;
}

[Serializable]
public class WatchHistoryItem
{
    public int video_id;
    public string video_name;
    public string video_url;
    public string visited_at;
}