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

    // Static — persists across instance destroy/disable, any script can read it
    public static List<VideoItem> GlobalCache = new List<VideoItem>();

    private List<VideoItem> allVideos = new List<VideoItem>();
    private string currentCategory = "All";
    private bool needsRefresh = false;
    private List<GameObject> cardPool = new List<GameObject>();

    void Awake()
    {
        // FIX: Singleton guard — prevents duplicate instances on scene reload
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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

        // FIX: Activate the default tab visually on start
        ResetAllTabs();
        SetTabActive(tabForYou, true);

        RefreshData();
    }

    void OnEnable()
    {
        if (needsRefresh)
        {
            needsRefresh = false;
            RefreshData();
        }
    }

    void OnDestroy()
    {
        // FIX: Clear dangling Instance reference so other scripts don't crash after destroy
        if (Instance == this) Instance = null;
    }

    public void RefreshData()
    {
        if (this.gameObject.activeInHierarchy)
        {
            StopAllCoroutines();
            StartCoroutine(FetchDashboardData());
        }
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
            Debug.LogError("No Token Found!");
            if (loadingSpinner) loadingSpinner.SetActive(false);
            yield break;
        }

        token = token.Trim().Replace("\"", "");

        // FIX: yield cannot be inside a try/catch block in C#.
        // Solution: do the web request outside try/catch, then parse inside it.
        UnityWebRequest request = UnityWebRequest.Get(apiUrl);
        request.SetRequestHeader("Authorization", "Bearer " + token);
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string json = request.downloadHandler.text;
            Debug.Log("RAW DASHBOARD DATA: " + json);

            // Only the non-yielding parse work goes inside try/catch
            try
            {
                APIResponseRoot root = JsonUtility.FromJson<APIResponseRoot>(json);

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
                        Debug.LogWarning("⚠️ User ID is 0 in Dashboard Data.");
                    }

                    if (!string.IsNullOrEmpty(root.data.videos))
                    {
                        string cleanJson = root.data.videos;
                        if (cleanJson.StartsWith("\""))
                            cleanJson = cleanJson.Trim('"').Replace("\\\"", "\"");

                        allVideos = JsonHelper.FromJson<VideoItem>(cleanJson);
                        GlobalCache = allVideos;

                        // FIX: JsonHelper.FromJson can return null on malformed JSON
                        if (allVideos == null) allVideos = new List<VideoItem>();
                        if (GlobalCache == null) GlobalCache = new List<VideoItem>();

                        if (allVideos.Count > 0 && allVideos[0].user_data != null)
                            UpdateProfileUI(allVideos[0].user_data);
                    }
                    else
                    {
                        allVideos = new List<VideoItem>();
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("JSON Parse Error: " + e.Message);
                allVideos = new List<VideoItem>();
            }

            // ApplyFilters is called outside the try/catch — safe, no yielding needed
            ApplyFilters();
        }
        else
        {
            Debug.LogError($"API Error {request.responseCode}: {request.downloadHandler.text}");
        }

        request.Dispose();

        if (loadingSpinner) loadingSpinner.SetActive(false);
    }

    void UpdateProfileUI(UserData data)
    {
        // FIX: first_name / last_name can be null from API — produces "null null" on screen
        string firstName = data.first_name ?? "";
        string lastName = data.last_name ?? "";

        if (profileNameText) profileNameText.text = (firstName + " " + lastName).Trim();
        if (designationText) designationText.text = data.designation ?? "";
        if (taglineText) taglineText.text = data.tag_line ?? "";

        if (profileImageDisplay != null && !string.IsNullOrEmpty(data.profile_pic))
            StartCoroutine(LoadProfileImage(data.profile_pic));
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

    // --- FILTERING ---

    void SetCategory(string subject, Button activeBtn)
    {
        currentCategory = subject;
        ResetAllTabs();
        SetTabActive(activeBtn, true);
        ApplyFilters();
    }

    void OnSearchQueryChanged(string query) => ApplyFilters();

    void ApplyFilters()
    {
        string searchQuery = searchInput != null ? searchInput.text.ToLower().Trim() : "";
        List<VideoItem> filtered = new List<VideoItem>();

        foreach (var video in allVideos)
        {
            // FIX: subject / title can be null — .ToLower() and .Equals() on null throw NullReferenceException
            string subject = video.subject ?? "";
            string title = video.title ?? "";

            bool matchesCategory = (currentCategory == "All") ||
                                   subject.Equals(currentCategory, System.StringComparison.OrdinalIgnoreCase);

            bool matchesSearch = string.IsNullOrEmpty(searchQuery) ||
                                 title.ToLower().Contains(searchQuery) ||
                                 subject.ToLower().Contains(searchQuery);

            if (matchesCategory && matchesSearch)
                filtered.Add(video);
        }

        PopulateUI(filtered);
    }

    void PopulateUI(List<VideoItem> videosToShow)
    {
        VideoPanelUI.ClearQueue();

        // Pool existing cards
        foreach (Transform child in videoListContainer)
        {
            child.gameObject.SetActive(false);
            cardPool.Add(child.gameObject);
        }

        foreach (VideoItem vid in videosToShow)
        {
            GameObject obj;
            if (cardPool.Count > 0)
            {
                obj = cardPool[cardPool.Count - 1];
                cardPool.RemoveAt(cardPool.Count - 1);
                obj.transform.SetParent(videoListContainer);
                obj.SetActive(true);
            }
            else
            {
                obj = Instantiate(videoCardPrefab, videoListContainer);
            }

            VideoPanelUI ui = obj.GetComponent<VideoPanelUI>();
            if (ui != null)
            {
                ui.Setup(vid, OnVideoClicked);
                ui.SetCardColor(GetColorForSubject(vid.subject ?? ""));
            }
        }

        // FIX: Destroy leftover pooled cards that weren't reused.
        // Without this the pool grows unbounded with every filter/search change.
        foreach (GameObject stale in cardPool)
        {
            if (stale != null) Destroy(stale);
        }
        cardPool.Clear();
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
        if (btn != null && btn.image != null)
            btn.image.sprite = isActive ? activeTabSprite : inactiveTabSprite;
    }
}