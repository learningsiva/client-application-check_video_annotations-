using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class VideoSocialManager : MonoBehaviour
{
    [Header("External Controllers")]
    public VideoLikeController likeController;
    public VideoCommentsController commentsController;
    public VideoSaveController saveController;

    [Header("API Settings")]
    public string videoDetailsApiUrl = "https://botclub.conbig.com/api/v1/get_video_details";

    /// <summary>
    /// Called by VideoReplayManager when a video starts loading.
    /// Sets up optimistic defaults and fetches fresh data from the server.
    /// </summary>
    public void Initialize(int videoId, bool initialLiked, int initialLikesCount, bool initialSaved)
    {
        // 1. Set optimistic defaults (data passed from the Dashboard list)
        if (likeController)
            likeController.Initialize(videoId, initialLiked, initialLikesCount);

        if (saveController)
            saveController.Initialize(videoId, initialSaved);

        if (commentsController)
            commentsController.Initialize(videoId, 0); // Default 0 until API returns

        // 2. Fetch fresh data (Realtime Likes, Comments, Saved status)
        StopAllCoroutines(); // Stop any previous fetch if switching videos rapidly
        StartCoroutine(FetchVideoDetails(videoId));
    }

    IEnumerator FetchVideoDetails(int videoId)
    {
        string url = $"{videoDetailsApiUrl}?video_id={videoId}";
        string token = PlayerPrefs.GetString("access_token", "");

        
        token = token.Trim().Replace("\"", "");

        int userId = PlayerPrefs.GetInt("user_id", -1);

        // 🔥 CRITICAL DEBUG LOGS
        Debug.Log("================ TOKEN DEBUG ================");
        Debug.Log($"URL: {url}");
        Debug.Log($"Token: '{token}'");
        Debug.Log($"Token Length: {token.Length}");
        Debug.Log($"User ID: {userId}");
        Debug.Log("=============================================");

        if (string.IsNullOrEmpty(token))
        {
            Debug.LogError("❌ FAILURE: Token is empty! Please log in again.");
            yield break;
        }

        if (userId == -1 || userId == 0)
        {
            Debug.LogWarning("⚠️ WARNING: User ID not found! This may cause authentication issues.");
        }

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("✅ SUCCESS! Social Data: " + request.downloadHandler.text);
                VideoDetailsResponse response = JsonUtility.FromJson<VideoDetailsResponse>(request.downloadHandler.text);
                if (response != null && response.data != null)
                {
                    UpdateSocialUI(videoId, response.data);
                }
            }
            else
            {
                Debug.LogError($"❌ API Error: {request.responseCode}");
                Debug.LogError($"❌ Server Message: {request.downloadHandler.text}");
                Debug.LogError($"❌ Request URL: {url}");
                Debug.LogError($"❌ Token Used: {token.Substring(0, Mathf.Min(10, token.Length))}...");
            }
        }
    }

    void UpdateSocialUI(int videoId, VideoDetailsData data)
    {
        int myUserId = PlayerPrefs.GetInt("user_id", -1);

        // 1. Check Likes
        bool amILiked = false;
        if (data.liked_users_ids != null)
        {
            foreach (int id in data.liked_users_ids) { if (id == myUserId) amILiked = true; }
        }

        // 2. Check Saved
        bool amISaved = false;
        if (data.saved_users_ids != null)
        {
            foreach (int id in data.saved_users_ids) { if (id == myUserId) amISaved = true; }
        }

        // 3. Update Controllers with FRESH data
        if (likeController)
            likeController.Initialize(videoId, amILiked, data.likes_count);

        if (saveController)
            saveController.Initialize(videoId, amISaved);

        if (commentsController)
            commentsController.Initialize(videoId, data.comments_count);
    }
}

// --- DATA CLASSES FOR JSON PARSING ---
[System.Serializable]
public class VideoDetailsResponse
{
    public string message;
    public VideoDetailsData data;
}

[System.Serializable]
public class VideoDetailsData
{
    public int likes_count;
    public int comments_count;
    public List<int> liked_users_ids;
    public List<int> saved_users_ids;
}