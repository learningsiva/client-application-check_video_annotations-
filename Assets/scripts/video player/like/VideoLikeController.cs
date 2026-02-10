using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class VideoLikeController : MonoBehaviour
{
    [Header("UI References")]
    public Button likeButton;
    public TMP_Text likeCountText;

    [Header("Icons")]
    public Sprite likedSprite;   // Filled Heart
    public Sprite unlikedSprite; // Empty Heart

    [Header("Settings")]
    public string likeApiUrl = "https://botclub.conbig.com/api/v1/like_video";

    private int currentVideoId;
    private bool isLikedLocally = false;
    private int currentLikeCount = 0;
    private Image buttonImage;

    void Awake()
    {
        if (likeButton != null) buttonImage = likeButton.GetComponent<Image>();
    }

    void Start()
    {
        if (likeButton)
        {
            likeButton.onClick.RemoveAllListeners();
            likeButton.onClick.AddListener(OnLikeClicked);
        }
    }

    public void Initialize(int videoId, bool initialLikedState, int initialCount)
    {
        currentVideoId = videoId;
        isLikedLocally = initialLikedState;
        currentLikeCount = initialCount;

        UpdateVisuals(isLikedLocally);
        UpdateCountText();
    }

    void OnLikeClicked()
    {
        
        bool previousState = isLikedLocally; // Save state in case we need to revert
        isLikedLocally = !isLikedLocally;

        // 2. Update Count
        if (isLikedLocally)
        {
            currentLikeCount++; // User just Liked it
        }
        else
        {
            currentLikeCount--; // User just Unliked it
            if (currentLikeCount < 0) currentLikeCount = 0; // Safety check
        }

        // 3. Update UI Immediately
        UpdateVisuals(isLikedLocally);
        UpdateCountText();

        // 4. Call API
        StartCoroutine(LikeVideoAPI(previousState));
    }

    void UpdateVisuals(bool isLiked)
    {
        if (buttonImage != null)
        {
            buttonImage.sprite = isLiked ? likedSprite : unlikedSprite;
        }
    }

    void UpdateCountText()
    {
        if (likeCountText)
            likeCountText.text = currentLikeCount.ToString();
    }

    IEnumerator LikeVideoAPI(bool previousState)
    {
        string token = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");
        if (string.IsNullOrEmpty(token)) yield break;

        WWWForm form = new WWWForm();
        form.AddField("video_id", currentVideoId);

        using (UnityWebRequest request = UnityWebRequest.Post(likeApiUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + token);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Toggle Like Failed: {request.downloadHandler.text}");

                // 🔥 REVERT LOGIC: If API fails, undo the changes
                isLikedLocally = previousState; // Go back to old state

                if (isLikedLocally) currentLikeCount++;
                else currentLikeCount--;

                UpdateVisuals(isLikedLocally);
                UpdateCountText();
            }
            else
            {
                Debug.Log("Like/Unlike Success: " + request.downloadHandler.text);
            }
        }
    }
}