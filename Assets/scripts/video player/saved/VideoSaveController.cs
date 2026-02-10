using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;

public class VideoSaveController : MonoBehaviour
{
    [Header("UI References")]
    public Button saveButton;
    public Image targetImage; // The Image component on the button to swap

    [Header("Icons")]
    public Sprite savedSprite;   // Drag "Filled" bookmark icon here
    public Sprite unsavedSprite; // Drag "Empty" bookmark icon here

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

    // 🔥 FIX: Now accepts 2 arguments (ID + Initial State)
    public void Initialize(int videoId, bool initialSavedState)
    {
        currentVideoId = videoId;
        isSavedLocally = initialSavedState;

        // Update UI immediately based on server data
        UpdateVisuals(isSavedLocally);
    }

    void OnSaveClicked()
    {
        // 1. 🔥 TOGGLE LOGIC (Flip the state)
        bool previousState = isSavedLocally; // Save state to revert if API fails
        isSavedLocally = !isSavedLocally;

        // 2. Update Visuals Instantly
        UpdateVisuals(isSavedLocally);

        // 3. Call API
        StartCoroutine(SaveVideoAPI(previousState));
    }

    void UpdateVisuals(bool isSaved)
    {
        if (targetImage != null)
        {
            targetImage.sprite = isSaved ? savedSprite : unsavedSprite;
            // targetImage.SetNativeSize(); // Optional
        }
    }

    IEnumerator SaveVideoAPI(bool previousState)
    {
        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");
        if (string.IsNullOrEmpty(token)) yield break;

        WWWForm form = new WWWForm();
        form.AddField("video_id", currentVideoId);

        using (UnityWebRequest request = UnityWebRequest.Post(saveApiUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Save Toggle Failed: {request.downloadHandler.text}");

                // 🔥 REVERT LOGIC: If API fails, undo the change
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