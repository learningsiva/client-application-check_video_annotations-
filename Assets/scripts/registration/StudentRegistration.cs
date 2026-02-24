using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class StudentRegistration : MonoBehaviour
{
    [Header("UI Elements - Personal Info")]
    public TMP_InputField firstNameInput;
    public TMP_InputField lastNameInput;
    public TMP_InputField emailInput;
    public TMP_InputField mobileNumberInput;
    public TMP_InputField passwordInput;
    public TMP_InputField confirmPasswordInput;

    [Header("UI Elements - Profile Info")]
    public TMP_InputField tagLineInput;
    public TMP_InputField studentTokenInput;   // Optional — leave blank if not needed

    [Header("UI Elements - Class")]
    public TMP_Dropdown classDropdown;

    [Header("UI Elements - Photo")]
    public Button uploadPhotoButton;           // Button user clicks to pick a photo
    public RawImage profilePicPreview;         // RawImage that shows the picked photo

    [Header("UI Buttons")]
    public Button registerButton;

    [Header("UI Feedback")]
    public TMP_Text messageBox;

    [Header("Settings")]
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    public string loginSceneName = "LoginScene";

    // API endpoint
    private const string RegisterApiUrl = "https://botclub.conbig.com/api/v1/student_register";

    // Internal photo state
    private byte[] profileImageData = null;

    // ── CLASS OPTIONS ──────────────────────────────────────────────────────────
    // Each entry maps a display label → the real category ID from the `categories`
    // table in the database (used as class_id in the API).
    // Ask your backend dev for the correct IDs and update this list accordingly.
    // ──────────────────────────────────────────────────────────────────────────
    [System.Serializable]
    public class ClassOption
    {
        public string label;   // Shown in the dropdown
        public int classId;    // Sent to API as class_id
    }

    // Edit these IDs to match your categories table!
    private readonly ClassOption[] classOptions = new ClassOption[]
    {
        new ClassOption { label = "Select Class", classId = -1  },  // placeholder
        new ClassOption { label = "I",      classId = 1   },
        new ClassOption { label = "II",      classId = 2   },
        new ClassOption { label = "III",      classId = 3   },
        new ClassOption { label = "IV",      classId = 4   },
        new ClassOption { label = "V",      classId = 5   },
        new ClassOption { label = "VI",      classId = 6   },
        new ClassOption { label = "VII",      classId = 7   },
        new ClassOption { label = "VIII",      classId = 8   },
        new ClassOption { label = "IX",      classId = 9   },
        new ClassOption { label = "X",     classId = 10  },
        new ClassOption { label = "XI",     classId = 11  },
        new ClassOption { label = "XII",     classId = 12  },
    };

    // ─────────────────────────────────────────────
    //  UNITY LIFECYCLE
    // ─────────────────────────────────────────────

    void Start()
    {
        ValidateUIReferences();
        SetupClassDropdown();
        SetupButtonListeners();
        messageBox.gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────
    //  SETUP
    // ─────────────────────────────────────────────

    void ValidateUIReferences()
    {
        bool hasError = false;

        if (firstNameInput == null) { Debug.LogError("[StudentRegistration] firstNameInput is not assigned!"); hasError = true; }
        if (lastNameInput == null) { Debug.LogError("[StudentRegistration] lastNameInput is not assigned!"); hasError = true; }
        if (emailInput == null) { Debug.LogError("[StudentRegistration] emailInput is not assigned!"); hasError = true; }
        if (mobileNumberInput == null) { Debug.LogError("[StudentRegistration] mobileNumberInput is not assigned!"); hasError = true; }
        if (passwordInput == null) { Debug.LogError("[StudentRegistration] passwordInput is not assigned!"); hasError = true; }
        if (confirmPasswordInput == null) { Debug.LogError("[StudentRegistration] confirmPasswordInput is not assigned!"); hasError = true; }
        if (tagLineInput == null) { Debug.LogError("[StudentRegistration] tagLineInput is not assigned!"); hasError = true; }
        if (classDropdown == null) { Debug.LogError("[StudentRegistration] classDropdown is not assigned!"); hasError = true; }
        if (registerButton == null) { Debug.LogError("[StudentRegistration] registerButton is not assigned!"); hasError = true; }
        if (messageBox == null) { Debug.LogError("[StudentRegistration] messageBox is not assigned!"); hasError = true; }

        // Optional references — warn but don't block
        if (studentTokenInput == null) Debug.LogWarning("[StudentRegistration] studentTokenInput not assigned — student_token will not be sent.");
        if (uploadPhotoButton == null) Debug.LogWarning("[StudentRegistration] uploadPhotoButton not assigned — photo upload disabled.");
        if (profilePicPreview == null) Debug.LogWarning("[StudentRegistration] profilePicPreview not assigned — photo preview disabled.");

        if (hasError)
            Debug.LogError("[StudentRegistration] One or more required UI references are missing. Please assign them in the Inspector.");
    }

    void SetupClassDropdown()
    {
        if (classDropdown == null) return;
        classDropdown.ClearOptions();

        var labels = new System.Collections.Generic.List<string>();
        foreach (var option in classOptions)
            labels.Add(option.label);

        classDropdown.AddOptions(labels);
    }

    void SetupButtonListeners()
    {
        if (registerButton != null)
        {
            registerButton.onClick.RemoveAllListeners();
            registerButton.onClick.AddListener(ValidateAndRegister);
        }

        if (uploadPhotoButton != null)
        {
            uploadPhotoButton.onClick.RemoveAllListeners();
            uploadPhotoButton.onClick.AddListener(OpenPhotoPicker);
        }
    }

    // ─────────────────────────────────────────────
    //  PHOTO PICK & PREVIEW
    // ─────────────────────────────────────────────

    void OpenPhotoPicker()
    {
#if UNITY_EDITOR
        string path = UnityEditor.EditorUtility.OpenFilePanel("Select Profile Picture", "", "png,jpg,jpeg");
        if (!string.IsNullOrEmpty(path))
            StartCoroutine(LoadLocalImage(path));

#elif UNITY_ANDROID || UNITY_IOS
        // Requires NativeGallery plugin: https://github.com/yasirkula/UnityNativeGallery
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (!string.IsNullOrEmpty(path))
                StartCoroutine(LoadLocalImage(path));
        }, "Select a Profile Picture", "image/png,image/jpeg");

#else
        Debug.LogWarning("[StudentRegistration] Photo picking is not supported on this platform.");
#endif
    }

    IEnumerator LoadLocalImage(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning("[StudentRegistration] Selected file does not exist: " + path);
            yield break;
        }

        profileImageData = File.ReadAllBytes(path);

        Texture2D texture = new Texture2D(2, 2);
        if (texture.LoadImage(profileImageData))
        {
            if (profilePicPreview != null)
                profilePicPreview.texture = texture;

            Debug.Log("[StudentRegistration] 📷 Photo loaded: " + path + " (" + profileImageData.Length + " bytes)");
        }
        else
        {
            Debug.LogWarning("[StudentRegistration] Could not decode image from: " + path);
            profileImageData = null;
        }

        yield return null;
    }

    // ─────────────────────────────────────────────
    //  VALIDATION
    // ─────────────────────────────────────────────

    void ValidateAndRegister()
    {
        string errorMessage = "";

        if (string.IsNullOrWhiteSpace(firstNameInput.text))
            errorMessage += "• First name is required.\n";

        if (string.IsNullOrWhiteSpace(lastNameInput.text))
            errorMessage += "• Last name is required.\n";

        if (string.IsNullOrWhiteSpace(emailInput.text))
            errorMessage += "• Email is required.\n";
        else if (!IsValidEmail(emailInput.text.Trim()))
            errorMessage += "• Please enter a valid email address.\n";

        if (string.IsNullOrWhiteSpace(mobileNumberInput.text))
            errorMessage += "• Mobile number is required.\n";

        if (string.IsNullOrWhiteSpace(passwordInput.text))
            errorMessage += "• Password is required.\n";
        else if (passwordInput.text.Length < 6)
            errorMessage += "• Password must be at least 6 characters.\n";

        if (string.IsNullOrWhiteSpace(confirmPasswordInput.text))
            errorMessage += "• Please confirm your password.\n";
        else if (passwordInput.text != confirmPasswordInput.text)
            errorMessage += "• Passwords do not match.\n";

        // classOptions[0] is the "Select Class" placeholder (classId = -1)
        if (classDropdown.value == 0)
            errorMessage += "• Please select a class.\n";

        // student_token is NOT validated — it is optional

        if (!string.IsNullOrEmpty(errorMessage))
        {
            ShowMessage(errorMessage.TrimEnd(), errorColor);
            return;
        }

        StartCoroutine(RegisterStudent());
    }

    bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    // ─────────────────────────────────────────────
    //  API CALL
    // ─────────────────────────────────────────────

    IEnumerator RegisterStudent()
    {
        registerButton.interactable = false;
        ShowMessage("Registering, please wait...", Color.white);

        // Resolve the selected class_id from the dropdown index
        int selectedClassId = classOptions[classDropdown.value].classId;

        WWWForm form = new WWWForm();
        form.AddField("first_name", firstNameInput.text.Trim());
        form.AddField("last_name", lastNameInput.text.Trim());
        form.AddField("email", emailInput.text.Trim());
        form.AddField("mobile_number", mobileNumberInput.text.Trim());
        form.AddField("password", passwordInput.text);
        form.AddField("tag_line", tagLineInput.text.Trim());
        form.AddField("class_id", selectedClassId);   // ← integer FK, not a string

        // school_id temporarily hardcoded — replace when backend confirms the correct ID
        form.AddField("school_id", 0);

        // student_token is optional — only send if the field exists and has a value
        if (studentTokenInput != null && !string.IsNullOrWhiteSpace(studentTokenInput.text))
            form.AddField("student_token", studentTokenInput.text.Trim());

        // Photo is optional — only attach if the user picked one
        if (profileImageData != null && profileImageData.Length > 0)
            form.AddBinaryData("photo", profileImageData, "profile.png", "image/png");

        Debug.Log($"[StudentRegistration] Sending request — class_id: {selectedClassId}");

        using (UnityWebRequest request = UnityWebRequest.Post(RegisterApiUrl, form))
        {
            yield return request.SendWebRequest();

            Debug.Log("[StudentRegistration] RAW RESPONSE: " + request.downloadHandler.text);
            Debug.Log("[StudentRegistration] Response Code: " + request.responseCode);

            if (request.result == UnityWebRequest.Result.Success)
                HandleRegistrationSuccess(request.downloadHandler.text);
            else
                HandleRegistrationError(request);
        }

        registerButton.interactable = true;
    }

    // ─────────────────────────────────────────────
    //  RESPONSE HANDLING
    // ─────────────────────────────────────────────

    void HandleRegistrationSuccess(string jsonResponse)
    {
        try
        {
            RegistrationResponse response = JsonUtility.FromJson<RegistrationResponse>(jsonResponse);
            string msg = response.message ?? "";

            if (!string.IsNullOrEmpty(msg))
                Debug.Log("[StudentRegistration] ✅ Registration successful: " + msg);
            else
                Debug.LogWarning("[StudentRegistration] HTTP 200 but no message field in response.");
        }
        catch (Exception e)
        {
            Debug.LogError("[StudentRegistration] Error parsing success response: " + e.Message);
        }

        // Always redirect on HTTP 200
        ShowMessage("Registration Successful! Redirecting to login...", successColor);
        Invoke(nameof(GoToLogin), 2f);
    }

    void HandleRegistrationError(UnityWebRequest request)
    {
        string userFriendlyMessage;

        switch (request.responseCode)
        {
            case 400:
                userFriendlyMessage = "Invalid registration details. Please check all fields and try again.";
                break;
            case 409:
                userFriendlyMessage = "An account with this email already exists. Please use a different email or log in.";
                break;
            case 422:
                userFriendlyMessage = "Some fields are invalid. Please review your details and try again.";
                break;
            case 429:
                userFriendlyMessage = "Too many requests. Please wait a moment and try again.";
                break;
            case 500:
            case 502:
            case 503:
                userFriendlyMessage = "Server is currently unavailable. Please try again later.";
                break;
            default:
                if (request.result == UnityWebRequest.Result.ConnectionError)
                    userFriendlyMessage = "No internet connection. Please check your network and try again.";
                else if (request.result == UnityWebRequest.Result.DataProcessingError)
                    userFriendlyMessage = "There was an error processing the request. Please try again.";
                else
                    userFriendlyMessage = "Registration failed. Please check your connection and try again.";
                break;
        }

        // Try to extract a more specific message from the server JSON
        if (!string.IsNullOrEmpty(request.downloadHandler.text))
        {
            try
            {
                RegistrationErrorResponse errorResponse = JsonUtility.FromJson<RegistrationErrorResponse>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(errorResponse.message))
                    userFriendlyMessage = errorResponse.message;
                else if (!string.IsNullOrEmpty(errorResponse.error))
                    userFriendlyMessage = errorResponse.error;
            }
            catch (Exception)
            {
                Debug.LogWarning("[StudentRegistration] Could not parse error response: " + request.downloadHandler.text);
            }
        }

        Debug.LogWarning("[StudentRegistration] ❌ Registration error: " + userFriendlyMessage);
        ShowMessage(userFriendlyMessage, errorColor);
    }

    // ─────────────────────────────────────────────
    //  NAVIGATION & UI HELPERS
    // ─────────────────────────────────────────────

    void GoToLogin()
    {
        if (!string.IsNullOrEmpty(loginSceneName))
            SceneManager.LoadScene(loginSceneName);
        else
            Debug.LogError("[StudentRegistration] loginSceneName is not set in the Inspector!");
    }

    void ShowMessage(string text, Color color)
    {
        CancelInvoke(nameof(HideMessage));
        messageBox.text = text;
        messageBox.color = color;
        messageBox.gameObject.SetActive(true);

        if (color != successColor && color != Color.white)
            Invoke(nameof(HideMessage), 6f);
    }

    void HideMessage()
    {
        messageBox.gameObject.SetActive(false);
    }
}

// ─────────────────────────────────────────────
//  DATA CLASSES
// ─────────────────────────────────────────────

[Serializable]
public class RegistrationResponse
{
    public string message;
    public string status;
    public int user_id;
    public string access_token;
}

[Serializable]
public class RegistrationErrorResponse
{
    public string message;
    public string error;
    public string detail;
}