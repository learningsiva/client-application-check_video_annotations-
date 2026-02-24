using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class StudentEditProfile : MonoBehaviour
{
    [Header("Input Fields")]
    public TMP_InputField firstNameInput;
    public TMP_InputField lastNameInput;
    public TMP_InputField designationInput;
    public TMP_InputField tagLineInput;

    [Header("UI Buttons")]
    public Button saveButton;
    public Button selectProfilePicButton;
    public Button cancelButton;

    [Header("UI Feedback")]
    public TMP_Text messageBox;

    [Header("Navigation Fallbacks")]
    public GameObject homePanel;
    public GameObject editProfilePanel;

    [Header("Settings")]
    private string updateProfileApiUrl = "https://botclub.conbig.com/api/v1/update_user_profile";
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    public float circleRadius = 100f;

    // Data
    private byte[] profileImageData;
    private string profilePicPath = "";
    public RawImage currentProfilePicPreview;
    private bool isSaving = false;

    void Start()
    {
        if (messageBox) messageBox.gameObject.SetActive(false);
        if (saveButton) { saveButton.onClick.RemoveAllListeners(); saveButton.onClick.AddListener(SaveProfileChanges); }
        if (selectProfilePicButton) { selectProfilePicButton.onClick.RemoveAllListeners(); selectProfilePicButton.onClick.AddListener(OpenFileBrowser); }
        if (cancelButton) { cancelButton.onClick.RemoveAllListeners(); cancelButton.onClick.AddListener(OnCancelClicked); }
    }

    // ── Accepts ProfileData — matches what StudentProfileManager passes
    public void SetupInputs(ProfileData data)
    {
        if (data == null) return;

        if (firstNameInput) firstNameInput.text = data.first_name ?? "";
        if (lastNameInput) lastNameInput.text = data.last_name ?? "";
        if (designationInput) designationInput.text = data.designation ?? "";
        if (tagLineInput) tagLineInput.text = data.tag_line ?? "";

        profileImageData = null;
        if (currentProfilePicPreview != null && !string.IsNullOrEmpty(data.profile_pic))
            StartCoroutine(LoadExistingProfileImage(data.profile_pic));

        isSaving = false;
        if (saveButton) saveButton.interactable = true;
    }

    // ─────────────────────────────────────────────
    //  PROFILE IMAGE
    // ─────────────────────────────────────────────

    IEnumerator LoadExistingProfileImage(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                if (currentProfilePicPreview != null)
                    currentProfilePicPreview.texture = CreateRoundedImage(texture);
            }
        }
    }

    void OpenFileBrowser()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Select Profile Picture", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path)) { profilePicPath = path; StartCoroutine(LoadLocalImage(path)); }
#elif UNITY_ANDROID
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (!string.IsNullOrEmpty(path)) { profilePicPath = path; StartCoroutine(LoadLocalImage(path)); }
        });
#endif
    }

    IEnumerator LoadLocalImage(string path)
    {
        if (!File.Exists(path)) yield break;
        profileImageData = File.ReadAllBytes(path);
        Texture2D uploadedPhoto = new Texture2D(2, 2);
        if (uploadedPhoto.LoadImage(profileImageData))
        {
            if (currentProfilePicPreview != null)
                currentProfilePicPreview.texture = CreateRoundedImage(uploadedPhoto);
        }
        yield return null;
    }

    Texture2D CreateRoundedImage(Texture2D sourceTexture)
    {
        if (sourceTexture.width == 0 || sourceTexture.height == 0) return sourceTexture;
        int size = (int)circleRadius * 2;
        Texture2D roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        int minSide = Mathf.Min(sourceTexture.width, sourceTexture.height);
        int offsetX = (sourceTexture.width - minSide) / 2;
        int offsetY = (sourceTexture.height - minSide) / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f));
                if (dist < size / 2f)
                {
                    int srcX = offsetX + (int)((float)x / size * minSide);
                    int srcY = offsetY + (int)((float)y / size * minSide);
                    roundedTexture.SetPixel(x, y, sourceTexture.GetPixel(srcX, srcY));
                }
                else
                {
                    roundedTexture.SetPixel(x, y, Color.clear);
                }
            }
        }
        roundedTexture.Apply();
        return roundedTexture;
    }

    // ─────────────────────────────────────────────
    //  SAVE
    // ─────────────────────────────────────────────

    void SaveProfileChanges()
    {
        if (isSaving) return;
        isSaving = true;
        if (saveButton) saveButton.interactable = false;
        StartCoroutine(UpdateUserProfile());
    }

    IEnumerator UpdateUserProfile()
    {
        string accessToken = PlayerPrefs.GetString("access_token", "").Trim().Replace("\"", "");
        if (string.IsNullOrEmpty(accessToken))
        {
            ShowMessage("Access token missing.", errorColor);
            ResetSaveState();
            yield break;
        }

        WWWForm form = new WWWForm();
        if (firstNameInput) form.AddField("first_name", firstNameInput.text.Trim());
        if (lastNameInput) form.AddField("last_name", lastNameInput.text.Trim());
        if (designationInput) form.AddField("designation", designationInput.text.Trim());
        if (tagLineInput) form.AddField("tag_line", tagLineInput.text.Trim());

        if (profileImageData != null)
            form.AddBinaryData("profile_pic", profileImageData, "profile.png", "image/png");

        using (UnityWebRequest request = UnityWebRequest.Post(updateProfileApiUrl, form))
        {
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.SetRequestHeader("Accept", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                ShowMessage("Updated!", successColor);

                // Refresh dashboard cache so updated name/pic reflects everywhere
                if (ClientDashboardManager.Instance != null)
                    ClientDashboardManager.Instance.RefreshData();

                yield return new WaitForSeconds(1.0f);

                // Close this edit panel
                this.gameObject.SetActive(false);

                // Navigate home via NavigationManager, fallback to homePanel
                NavigationManager nav = FindObjectOfType<NavigationManager>();
                if (nav != null && nav.homeButton != null)
                    nav.homeButton.onClick.Invoke();
                else if (homePanel)
                    homePanel.SetActive(true);
            }
            else
            {
                Debug.LogError($"[StudentEditProfile] Failed: {request.responseCode} - {request.downloadHandler.text}");
                ShowMessage(request.responseCode == 401 ? "Session Expired." : "Update Failed.", errorColor);
            }
        }

        ResetSaveState();
    }

    // ─────────────────────────────────────────────
    //  CANCEL
    // ─────────────────────────────────────────────

    void OnCancelClicked()
    {
        this.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    void ResetSaveState()
    {
        isSaving = false;
        if (saveButton) saveButton.interactable = true;
    }

    void ShowMessage(string text, Color color)
    {
        if (messageBox)
        {
            messageBox.text = text;
            messageBox.color = color;
            messageBox.gameObject.SetActive(true);
            Invoke(nameof(HideMessage), 4f);
        }
    }

    void HideMessage()
    {
        if (messageBox) messageBox.gameObject.SetActive(false);
    }
}