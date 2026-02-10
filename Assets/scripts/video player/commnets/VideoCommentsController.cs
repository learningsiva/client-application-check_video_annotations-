using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class VideoCommentsController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject panelRoot; // The Comment Panel itself
    public Transform commentsContainer; // The Content object of ScrollView
    public GameObject commentItemPrefab;
    public TMP_Text headerCountText;
    public GameObject socialMediaPanel; // Panel containing Like, Share, Back buttons

    [Header("Loading UI")]
    public GameObject loadingSpinner; // Optional: Show while fetching comments

    [Header("Input References")]
    public TMP_InputField commentInput;
    public Button sendButton;
    public Button closeButton;

    [Header("API Settings")]
    public string addCommentUrl = "https://botclub.conbig.com/api/v1/add_comment";
    public string getCommentsUrl = "https://botclub.conbig.com/api/v1/get_video_comments";

    private int currentVideoId;
    private int currentCommentCount = 0;

    void Start()
    {
        if (panelRoot) panelRoot.SetActive(false); // Hide by default
        if (sendButton) sendButton.onClick.AddListener(OnSendClicked);
        if (closeButton) closeButton.onClick.AddListener(ClosePanel);
        if (loadingSpinner) loadingSpinner.SetActive(false);
    }

    public void Initialize(int videoId, int totalCommentCount)
    {
        currentVideoId = videoId;
        currentCommentCount = totalCommentCount;
        UpdateCountText(totalCommentCount);
    }

    /// <summary>
    /// Opens the comment panel and fetches existing comments
    /// </summary>
    public void OpenPanel()
    {
        if (panelRoot) panelRoot.SetActive(true);
        if (socialMediaPanel) socialMediaPanel.SetActive(false); // Hide Social UI

        // 🔥 Fetch comments when panel opens
        ClearCommentsUI();
        StartCoroutine(FetchCommentsAPI());
    }

    public void ClosePanel()
    {
        if (panelRoot) panelRoot.SetActive(false);
        if (socialMediaPanel) socialMediaPanel.SetActive(true); // Restore Social UI
    }

    void OnSendClicked()
    {
        string text = commentInput.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        // 1. Get user name from PlayerPrefs (or use "Me" as fallback)
        string firstName = PlayerPrefs.GetString("first_name", "");
        string lastName = PlayerPrefs.GetString("last_name", "");
        string userName = string.IsNullOrEmpty(firstName) ? "Me" : $"{firstName} {lastName}".Trim();

        // 2. Optimistic UI: Add comment immediately
        AddCommentToUI(userName, text);

        // 3. Update count optimistically
        currentCommentCount++;
        UpdateCountText(currentCommentCount);

        // 4. Clear Input
        commentInput.text = "";

        // 5. Send API
        StartCoroutine(PostCommentAPI(text));
    }

    /// <summary>
    /// Fetches all comments for the current video
    /// </summary>
    IEnumerator FetchCommentsAPI()
    {
        if (loadingSpinner) loadingSpinner.SetActive(true);

        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");

        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("❌ No token found!");
            if (loadingSpinner) loadingSpinner.SetActive(false);
            yield break;
        }

        string url = $"{getCommentsUrl}?video_id={currentVideoId}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (loadingSpinner) loadingSpinner.SetActive(false);

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ Comments Fetched: " + request.downloadHandler.text);

                try
                {
                    CommentsResponse response = JsonUtility.FromJson<CommentsResponse>(request.downloadHandler.text);

                    if (response != null && response.data != null)
                    {
                        // Update the count with actual server data
                        currentCommentCount = response.data.Count;
                        UpdateCountText(currentCommentCount);

                        // Populate UI with fetched comments
                        foreach (CommentData comment in response.data)
                        {
                            string fullName = $"{comment.first_name} {comment.last_name}".Trim();
                            if (string.IsNullOrEmpty(fullName)) fullName = "Anonymous";

                            AddCommentToUI(fullName, comment.comment);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("❌ JSON Parse Error: " + e.Message);
                }
            }
            else
            {
                Debug.LogError($"❌ Fetch Comments Error: {request.responseCode}");
                Debug.LogError($"❌ Server Message: {request.downloadHandler.text}");
            }
        }
    }

    /// <summary>
    /// Posts a new comment to the server
    /// </summary>
    IEnumerator PostCommentAPI(string commentText)
    {
        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");

        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("❌ No token found!");
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddField("video_id", currentVideoId);
        form.AddField("comment", commentText);

        using (UnityWebRequest request = UnityWebRequest.Post(addCommentUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ Comment Posted: " + request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"❌ Comment Failed: {request.responseCode}");
                Debug.LogError($"❌ Server Message: {request.downloadHandler.text}");

                // Rollback optimistic update on failure
                currentCommentCount--;
                UpdateCountText(currentCommentCount);
            }
        }
    }

    /// <summary>
    /// Adds a comment to the UI
    /// </summary>
    void AddCommentToUI(string name, string comment)
    {
        GameObject obj = Instantiate(commentItemPrefab, commentsContainer);
        obj.transform.SetAsLastSibling();

        CommentItemUI ui = obj.GetComponent<CommentItemUI>();
        if (ui) ui.Setup(name, comment);
    }

    /// <summary>
    /// Clears all comments from the UI
    /// </summary>
    void ClearCommentsUI()
    {
        foreach (Transform child in commentsContainer)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>
    /// Updates the comment count display
    /// </summary>
    void UpdateCountText(int count)
    {
        if (headerCountText) headerCountText.text = $"{count}";
    }
}

// ============ DATA CLASSES FOR JSON PARSING ============

[System.Serializable]
public class CommentsResponse
{
    public string message;
    public List<CommentData> data;
}

[System.Serializable]
public class CommentData
{
    public int comment_id;
    public string comment;
    public string created_at;
    public string first_name;
    public string last_name;
}