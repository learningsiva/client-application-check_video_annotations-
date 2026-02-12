using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class UserLogin : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    // public Button registerButton;
    public TMP_Text messageBox;
    //public Button exitButton;

    [Header("Settings")]
    public Color successColor = Color.green;
    public Color errorColor = Color.red;
    public string dashboardSceneName;
    // public string registerSceneName;      
    private string loginApiUrl = "https://botclub.conbig.com/api/v1/authenticate";
    private string updateLoginTimeApiUrl = "https://botclub.conbig.com/api/v1/update_login_time";

    void Start()
    {
        // Validate UI references
        if (loginButton == null || emailInput == null || passwordInput == null || messageBox == null)
        {
            Debug.LogError("One or more UI references are not assigned in the inspector!");
            return;
        }

        loginButton.onClick.AddListener(ValidateAndLogin);
        // registerButton.onClick.AddListener(GoToRegisterScreen);
        messageBox.gameObject.SetActive(false);
        //exitButton.onClick.AddListener(exit);
    }

    /// <summary>
    /// Validates that email and password fields are not empty.
    /// </summary>
    void ValidateAndLogin()
    {
        string errorMessage = "";
        if (string.IsNullOrWhiteSpace(emailInput.text))
            errorMessage += "• Email is required.\n";
        if (string.IsNullOrWhiteSpace(passwordInput.text))
            errorMessage += "• Password is required.\n";

        if (!string.IsNullOrEmpty(errorMessage))
        {
            ShowMessage(errorMessage, errorColor);
            return;
        }

        StartCoroutine(LoginUser());
    }

    /// <summary>
    /// Sends a login request with the provided email and password.
    /// </summary>
    IEnumerator LoginUser()
    {
        WWWForm form = new WWWForm();
        form.AddField("email", emailInput.text.Trim());
        form.AddField("password", passwordInput.text);

        using (UnityWebRequest request = UnityWebRequest.Post(loginApiUrl, form))
        {
            yield return request.SendWebRequest();


            Debug.Log("RAW LOGIN RESPONSE: " + request.downloadHandler.text);

            if (request.result == UnityWebRequest.Result.Success)
            {
                ProcessResponse(request.downloadHandler.text);
            }
            else
            {
                HandleLoginError(request);
            }
        }
    }

    /// <summary>
    /// Handles different types of login errors and shows user-friendly messages.
    /// </summary>
    /// <param name="request">The failed UnityWebRequest</param>
    void HandleLoginError(UnityWebRequest request)
    {
        string userFriendlyMessage = "";

        switch (request.responseCode)
        {
            case 401: // Unauthorized
                userFriendlyMessage = "Invalid email or password. Please check your credentials and try again.";
                break;
            case 400: // Bad Request
                userFriendlyMessage = "Please check your email format and ensure all fields are filled correctly.";
                break;
            case 404: // Not Found
                userFriendlyMessage = "Login service is currently unavailable. Please try again later.";
                break;
            case 429: // Too Many Requests
                userFriendlyMessage = "Too many login attempts. Please wait a moment and try again.";
                break;
            case 500: // Internal Server Error
            case 502: // Bad Gateway
            case 503: // Service Unavailable
                userFriendlyMessage = "Server is currently unavailable. Please try again later.";
                break;
            default:
                // Check if there's a network error
                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    userFriendlyMessage = "No internet connection. Please check your network and try again.";
                }
                else if (request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    userFriendlyMessage = "There was an error processing your request. Please try again.";
                }
                else
                {
                    userFriendlyMessage = "Login failed. Please check your internet connection and try again.";
                }
                break;
        }

        
        if (!string.IsNullOrEmpty(request.downloadHandler.text))
        {
            try
            {
                ErrorResponse errorResponse = JsonUtility.FromJson<ErrorResponse>(request.downloadHandler.text);
                if (!string.IsNullOrEmpty(errorResponse.message))
                {
                    userFriendlyMessage = errorResponse.message;
                }
                else if (!string.IsNullOrEmpty(errorResponse.error))
                {
                    userFriendlyMessage = errorResponse.error;
                }
            }
            catch (Exception)
            {
                
                Debug.LogWarning("Could not parse error response: " + request.downloadHandler.text);
            }
        }

        ShowMessage(userFriendlyMessage, errorColor);
    }

    /// <summary>
    /// Processes successful login response.
    /// </summary>
    /// <param name="jsonResponse">JSON string response.</param>
    void ProcessResponse(string jsonResponse)
    {
        try
        {
            LoginResponse response = JsonUtility.FromJson<LoginResponse>(jsonResponse);

            if (!string.IsNullOrEmpty(response.access_token))
            {
                // 1. Store Token (Cleaned)
                string cleanToken = response.access_token.Trim().Replace("\"", "");
                PlayerPrefs.SetString("access_token", cleanToken);

                // 2. Store User ID
                if (response.user_id != 0)
                {
                    PlayerPrefs.SetInt("user_id", response.user_id);
                    Debug.Log("✅ User ID Saved: " + response.user_id);
                }
                else
                {
                    Debug.LogWarning("⚠️ Login successful, but user_id was 0 or missing in JSON.");
                }

                // 3. 🔥 NEW: Store User Role
                if (!string.IsNullOrEmpty(response.user_role))
                {
                    PlayerPrefs.SetString("user_role", response.user_role.ToLower()); // Save as lowercase (e.g., "professor")
                    Debug.Log("✅ User Role Saved: " + response.user_role);
                }
                else
                {
                    // Fallback: If no role is sent, assume Professor (for backward compatibility)
                    PlayerPrefs.SetString("user_role", "professor");
                    Debug.LogWarning("⚠️ No user_role found in response. Defaulting to 'professor'.");
                }

                PlayerPrefs.Save();

                ShowMessage("Login Successful!", successColor);
                StartCoroutine(UpdateLoginTime(cleanToken));
            }
            else
            {
                ShowMessage("Login failed: Invalid response", errorColor);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing login response: " + e.Message);
            ShowMessage("Login failed: Parsing Error", errorColor);
        }
    }

    /// <summary>
    /// Calls the update login time API after successful login.
    /// </summary>
    /// <param name="accessToken">The access token received from login.</param>
    IEnumerator UpdateLoginTime(string accessToken)
    {
        // Create JSON payload with session_type parameter set to 2
        string jsonPayload = JsonUtility.ToJson(new UpdateLoginTimeRequest { session_type = 2 });

        using (UnityWebRequest request = new UnityWebRequest(updateLoginTimeApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    UpdateLoginTimeResponse updateResponse = JsonUtility.FromJson<UpdateLoginTimeResponse>(request.downloadHandler.text);
                    Debug.Log("Login time updated: " + updateResponse.message);

                    // Navigate to dashboard after successful update
                    Invoke(nameof(GoToDashboard), 1f);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Could not parse update login time response: " + e.Message);
                    // Still navigate to dashboard even if parsing fails
                    Invoke(nameof(GoToDashboard), 1f);
                }
            }
            else
            {
                Debug.LogWarning("Failed to update login time: " + request.error + " - Response: " + request.downloadHandler.text);
                // Still navigate to dashboard even if update fails
                Invoke(nameof(GoToDashboard), 1f);
            }
        }
    }

    /// <summary>
    /// Navigates to the dashboard scene.
    /// </summary>
    void GoToDashboard()
    {
        SceneManager.LoadScene(dashboardSceneName);
    }

    /// <summary>
    /// Displays a temporary message on the UI.
    /// </summary>
    /// <param name="text">Message to display.</param>
    /// <param name="color">Color of the text.</param>
    void ShowMessage(string text, Color color)
    {
        messageBox.text = text;
        messageBox.color = color;
        messageBox.gameObject.SetActive(true);
        Invoke(nameof(HideMessage), 5f); // Increased to 5 seconds for better readability
    }

    void HideMessage()
    {
        messageBox.gameObject.SetActive(false);
    }
}

// --- DATA CLASSES ---

[Serializable]
public class LoginResponse
{
    public string message;
    public string access_token;
    public string redirect_to;
    public int user_id;
    // 🔥 NEW: Add this field to capture the role
    public string user_role;
}

[Serializable]
public class ErrorResponse
{
    public string message;
    public string error;
    public string detail;
}

[Serializable]
public class UpdateLoginTimeRequest
{
    public int session_type;
}

[Serializable]
public class UpdateLoginTimeResponse
{
    public string message;
}