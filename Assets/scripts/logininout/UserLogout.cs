using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class UserLogout : MonoBehaviour
{
    [Header("UI Elements")]
    public Button exitButton;

    [Header("Optional UI for Feedback")]
    public TMP_Text statusMessage; // Optional: to show logout status

    private string updateLogoutTimeApiUrl = "https://botclub.conbig.com/api/v1/update_logout_time";
    private bool isLoggingOut = false; // Prevent multiple logout calls

    // Start is called before the first frame update
    void Start()
    {
        if (exitButton == null)
        {
            Debug.LogError("Exit button reference is not assigned in the inspector!");
            return;
        }

        exitButton.onClick.AddListener(HandleLogout);
    }

    /// <summary>
    /// Handles the logout process - calls API then exits application.
    /// </summary>
    void HandleLogout()
    {
        if (isLoggingOut) return; // Prevent multiple calls

        isLoggingOut = true;

        // Get stored access token
        string accessToken = PlayerPrefs.GetString("access_token", "");

        if (string.IsNullOrEmpty(accessToken))
        {
            Debug.LogWarning("No access token found. Proceeding with direct exit.");
            ExitApplication();
            return;
        }

        // Show optional status message
        if (statusMessage != null)
        {
            statusMessage.text = "Logging out...";
            statusMessage.gameObject.SetActive(true);
        }

        StartCoroutine(UpdateLogoutTime(accessToken));
    }

    /// <summary>
    /// Calls the update logout time API.
    /// </summary>
    /// <param name="accessToken">The stored access token.</param>
    IEnumerator UpdateLogoutTime(string accessToken)
    {
        // Create JSON payload with session_type parameter set to 2
        string jsonPayload = JsonUtility.ToJson(new UpdateLogoutTimeRequest { session_type = 2 });

        using (UnityWebRequest request = new UnityWebRequest(updateLogoutTimeApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);

            // Set a timeout for the request (5 seconds)
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    UpdateLogoutTimeResponse logoutResponse = JsonUtility.FromJson<UpdateLogoutTimeResponse>(request.downloadHandler.text);
                    Debug.Log("Logout time updated: " + logoutResponse.message);

                    // Show success message briefly if UI is available
                    if (statusMessage != null)
                    {
                        statusMessage.text = "Logout successful!";
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Could not parse logout response: " + e.Message);
                }
            }
            else
            {
                Debug.LogWarning("Failed to update logout time: " + request.error + " - Response: " + request.downloadHandler.text);

                // Show error message briefly if UI is available
                if (statusMessage != null)
                {
                    statusMessage.text = "Logout tracking failed, but exiting...";
                }
            }

            // Wait a moment for user to see the message, then exit
            yield return new WaitForSeconds(1f);
            ExitApplication();
        }
    }
    void OnApplicationQuit()
    {
        string accessToken = PlayerPrefs.GetString("access_token", "");

        if (!string.IsNullOrEmpty(accessToken))
        {
            StartCoroutine(QuickLogoutUpdate(accessToken));
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            string accessToken = PlayerPrefs.GetString("access_token", "");

            if (!string.IsNullOrEmpty(accessToken))
            {
                StartCoroutine(QuickLogoutUpdate(accessToken));
            }
        }
    }

    IEnumerator QuickLogoutUpdate(string accessToken)
    {
        string jsonPayload = JsonUtility.ToJson(new UpdateLogoutTimeRequest { session_type = 2 }); // Use your session_type

        using (UnityWebRequest request = new UnityWebRequest(updateLogoutTimeApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.timeout = 2;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Quick logout time updated on app exit");
            }
        }
    }

    /// <summary>
    /// Cleans up stored data and exits the application.
    /// </summary>
    void ExitApplication()
    {
        // Clear stored access token
        PlayerPrefs.DeleteKey("access_token");
        PlayerPrefs.Save();

        Debug.Log("Application exiting...");
        Application.Quit();

        // For editor testing
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    /// <summary>
    /// Alternative method to go back to login scene instead of quitting.
    /// Call this method if you want to return to login instead of exiting.
    /// </summary>
    public void LogoutToLoginScene(string loginSceneName)
    {
        if (isLoggingOut) return;

        isLoggingOut = true;

        string accessToken = PlayerPrefs.GetString("access_token", "");

        if (string.IsNullOrEmpty(accessToken))
        {
            GoToLoginScene(loginSceneName);
            return;
        }

        StartCoroutine(UpdateLogoutTimeAndGoToLogin(accessToken, loginSceneName));
    }

    /// <summary>
    /// Updates logout time and navigates to login scene.
    /// </summary>
    IEnumerator UpdateLogoutTimeAndGoToLogin(string accessToken, string loginSceneName)
    {
        string jsonPayload = JsonUtility.ToJson(new UpdateLogoutTimeRequest { session_type = 2 });

        using (UnityWebRequest request = new UnityWebRequest(updateLogoutTimeApiUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + accessToken);
            request.timeout = 5;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Logout time updated successfully");
            }
            else
            {
                Debug.LogWarning("Failed to update logout time: " + request.error);
            }

            GoToLoginScene(loginSceneName);
        }
    }

    /// <summary>
    /// Navigates to the login scene.
    /// </summary>
    void GoToLoginScene(string loginSceneName)
    {
        // Clear stored access token
        PlayerPrefs.DeleteKey("access_token");
        PlayerPrefs.Save();

        SceneManager.LoadScene(loginSceneName);
    }
}

/// <summary>
/// Represents the request payload for update logout time API.
/// </summary>
[Serializable]
public class UpdateLogoutTimeRequest
{
    public int session_type;
}

/// <summary>
/// Represents the response from the update logout time API.
/// </summary>
[Serializable]
public class UpdateLogoutTimeResponse
{
    public string message;
}