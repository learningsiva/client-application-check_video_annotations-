using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class VideoSaveController : MonoBehaviour
{
    [Header("UI References")]
    public Button saveButton;
    public Image targetImage;

    [Header("Icons")]
    public Sprite savedSprite;
    public Sprite unsavedSprite;

    [Header("Settings")]
    public string saveApiUrl = "https://botclub.conbig.com/api/v1/add_watch_later";

    private int currentVideoId;
    private bool isSavedLocally = false;

    void Start()
    {
        if (saveButton)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(OnSaveClicked);
        }
    }

    public void Initialize(int videoId, bool initialSavedState)
    {
        currentVideoId = videoId;
        isSavedLocally = initialSavedState;
        UpdateVisuals(isSavedLocally);
    }

    void OnSaveClicked()
    {
        bool previousState = isSavedLocally;
        isSavedLocally = !isSavedLocally;
        UpdateVisuals(isSavedLocally);
        StartCoroutine(SaveVideoAPI(previousState));
    }

    void UpdateVisuals(bool isSaved)
    {
        if (targetImage != null)
            targetImage.sprite = isSaved ? savedSprite : unsavedSprite;
    }

    IEnumerator SaveVideoAPI(bool previousState)
    {
        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");
        if (string.IsNullOrEmpty(token)) yield break;

        // FIX: API requires Content-Type: application/json — WWWForm sends wrong format
        string jsonBody = $"{{\"video_id\":{currentVideoId}}}";
        Debug.Log($"[SaveController] Sending video_id: {currentVideoId}");
        Debug.Log($"[SaveController] Token: '{token}'");
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(saveApiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Save Toggle Failed: {request.downloadHandler.text}");
                // Revert UI if API fails
                isSavedLocally = previousState;
                UpdateVisuals(isSavedLocally);
            }
            else
            {
                Debug.Log("Watch Later Toggle Success: " + request.downloadHandler.text);
            }
        }
    }
}