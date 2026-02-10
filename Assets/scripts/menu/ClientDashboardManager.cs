using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;

public class ClientDashboardManager : MonoBehaviour
{
    public static ClientDashboardManager Instance;
    public List<VideoItem> CachedVideos { get { return allVideos; } }

    [Header("API Settings")]
    public string apiUrl = "https://botclub.conbig.com/api/v1/get_professors_annotations_details";
    public string playerSceneName;

    [Header("UI References - Video List")]
    public Transform videoListContainer;
    public GameObject videoCardPrefab;
    public GameObject loadingSpinner;
    public TMP_InputField searchInput;

    [Header("UI References - User Profile")]
    public TMP_Text profileNameText;
    public TMP_Text designationText;
    public TMP_Text taglineText;
    public RawImage profileImageDisplay;

    [Header("Category Tabs")]
    public Button tabForYou;
    public Button tabPhysics;
    public Button tabBiology;
    public Button tabChemistry;
    public Button tabTech;

    [Header("Tab Visuals")]
    public Sprite activeTabSprite;
    public Sprite inactiveTabSprite;

    private List<VideoItem> allVideos = new List<VideoItem>();
    private string currentCategory = "All";

    // 🔥 FIX: Track if we need to update when we wake up
    private bool needsRefresh = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (tabForYou) tabForYou.onClick.AddListener(() => SetCategory("All", tabForYou));
        if (tabPhysics) tabPhysics.onClick.AddListener(() => SetCategory("Physics", tabPhysics));
        if (tabBiology) tabBiology.onClick.AddListener(() => SetCategory("Biology", tabBiology));
        if (tabChemistry) tabChemistry.onClick.AddListener(() => SetCategory("Chemistry", tabChemistry));
        if (tabTech) tabTech.onClick.AddListener(() => SetCategory("Tech", tabTech));

        if (searchInput) searchInput.onValueChanged.AddListener(OnSearchQueryChanged);

        // Fetch Data on Start
        RefreshData();
    }

    // 🔥 FIX: Using OnEnable to catch the refresh request
    void OnEnable()
    {
        // If a refresh was requested while this panel was hidden, do it now!
        if (needsRefresh)
        {
            RefreshData();
            needsRefresh = false; // Reset flag
        }
    }

    // 🔥 UPDATED: Safe Refresh Function
    public void RefreshData()
    {
        // 1. If active, run immediately
        if (this.gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
            StartCoroutine(FetchDashboardData());
        }
        // 2. If inactive, mark it for later (When OnEnable runs)
        else
        {
            Debug.Log("⚠️ Dashboard is inactive. Queuing refresh for next OnEnable.");
            needsRefresh = true;
        }
    }

    IEnumerator FetchDashboardData()
    {
        if (loadingSpinner) loadingSpinner.SetActive(true);

        string token = PlayerPrefs.GetString("access_token", "");

        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError(" No Token Found!");
            if (loadingSpinner) loadingSpinner.SetActive(false);
            yield break;
        }

        token = token.Trim().Replace("\"", "");

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string json = request.downloadHandler.text;

                // 🔥 DEBUG: Check if user_id is here
                Debug.Log("RAW DASHBOARD DATA: " + json);

                try
                {
                    APIResponseRoot root = JsonUtility.FromJson<APIResponseRoot>(json);

                    // 🔥 NEW: Check and Save User ID from Dashboard Response
                    if (root != null && root.data != null)
                    {
                        if (root.data.user_id != 0)
                        {
                            PlayerPrefs.SetInt("user_id", root.data.user_id);
                            PlayerPrefs.Save();
                            Debug.Log("✅ User ID Saved from Dashboard: " + root.data.user_id);
                        }
                        else
                        {
                            Debug.LogWarning("⚠️ User ID is still 0 in Dashboard Data.");
                        }

                        // Parse Videos
                        if (!string.IsNullOrEmpty(root.data.videos))
                        {
                            string cleanJson = root.data.videos;
                            if (cleanJson.StartsWith("\"")) cleanJson = cleanJson.Trim('"').Replace("\\\"", "\"");

                            allVideos = JsonHelper.FromJson<VideoItem>(cleanJson);

                            if (allVideos.Count > 0 && allVideos[0].user_data != null)
                            {
                                UpdateProfileUI(allVideos[0].user_data);
                            }

                            ApplyFilters();
                        }
                    }
                }
                catch (System.Exception e) { Debug.LogError("JSON Error: " + e.Message); }
            }
            else
            {
                Debug.LogError($"API Error {request.responseCode}: {request.downloadHandler.text}");
            }
        }

        if (loadingSpinner) loadingSpinner.SetActive(false);
    }

    void UpdateProfileUI(UserData data)
    {
        if (profileNameText) profileNameText.text = data.first_name + " " + data.last_name;
        if (designationText) designationText.text = data.designation;
        if (taglineText) taglineText.text = data.tag_line;

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

    // --- FILTERING LOGIC ---
    void SetCategory(string subject, Button activeBtn)
    {
        currentCategory = subject;
        ResetAllTabs();
        SetTabActive(activeBtn, true);
        ApplyFilters();
    }

    void OnSearchQueryChanged(string query)
    {
        ApplyFilters();
    }

    void ApplyFilters()
    {
        string searchQuery = searchInput != null ? searchInput.text.ToLower().Trim() : "";
        List<VideoItem> filtered = new List<VideoItem>();

        foreach (var video in allVideos)
        {
            bool matchesCategory = (currentCategory == "All") ||
                                   (video.subject.Equals(currentCategory, System.StringComparison.OrdinalIgnoreCase));

            bool matchesSearch = string.IsNullOrEmpty(searchQuery) ||
                                 video.title.ToLower().Contains(searchQuery) ||
                                 video.subject.ToLower().Contains(searchQuery);

            if (matchesCategory && matchesSearch)
            {
                filtered.Add(video);
            }
        }

        PopulateUI(filtered);
    }

    void PopulateUI(List<VideoItem> videosToShow)
    {
        VideoPanelUI.ClearQueue();
        foreach (Transform child in videoListContainer) Destroy(child.gameObject);

        foreach (VideoItem vid in videosToShow)
        {
            GameObject obj = Instantiate(videoCardPrefab, videoListContainer);
            VideoPanelUI ui = obj.GetComponent<VideoPanelUI>();
            if (ui != null)
            {
                ui.Setup(vid, OnVideoClicked);
                ui.SetCardColor(GetColorForSubject(vid.subject));
            }
        }
    }

    void OnVideoClicked(VideoItem selectedVideo)
    {
        AppSession.CurrentVideo = selectedVideo;
        SceneManager.LoadScene(playerSceneName);
    }

    Color GetColorForSubject(string subject)
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

    void ResetAllTabs()
    {
        SetTabActive(tabForYou, false);
        SetTabActive(tabPhysics, false);
        SetTabActive(tabBiology, false);
        SetTabActive(tabChemistry, false);
        SetTabActive(tabTech, false);
    }

    void SetTabActive(Button btn, bool isActive)
    {
        if (btn != null && btn.image != null) btn.image.sprite = isActive ? activeTabSprite : inactiveTabSprite;
    }
}