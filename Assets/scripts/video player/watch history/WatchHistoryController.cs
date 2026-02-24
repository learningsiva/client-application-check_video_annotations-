using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class WatchHistoryController : MonoBehaviour
{
    [Header("Settings")]
    public string apiUrl = "https://botclub.conbig.com/api/v1/add_to_watch_history";

    void Start()
    {
        if (AppSession.CurrentVideo == null)
        {
            Debug.LogWarning("[WatchHistory] No current video in session — skipping.");
            return;
        }

        StartCoroutine(AddToWatchHistory(AppSession.CurrentVideo.task_id));
    }

    IEnumerator AddToWatchHistory(int videoId)
    {
        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");

        if (string.IsNullOrEmpty(token))
        {
            Debug.LogWarning("[WatchHistory] No token found — skipping.");
            yield break;
        }

        // API expects JSON body: { "video_id": 4 }
        string jsonBody = $"{{\"video_id\":{videoId}}}";
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
                Debug.Log($"[WatchHistory] ✅ Video {videoId} added. Response: {request.downloadHandler.text}");
            else
                Debug.LogWarning($"[WatchHistory] ❌ Failed for video {videoId}: {request.downloadHandler.text}");
        }
    }
}