using UnityEngine;

public class ProfileRoleManager : MonoBehaviour
{
    [Header("Child Panels")]
    public GameObject professorPanel; // Drag 'Panel_ProfessorProfile' here
    public GameObject studentPanel;   // Drag 'Panel_StudentProfile' here

    // This runs every time the Main Profile Panel is opened by NavigationManager
    void OnEnable()
    {
        // 1. Get the Role (Defaults to 'professor' if not found)
        string role = PlayerPrefs.GetString("user_role", "professor").ToLower().Trim();

        Debug.Log($" Profile Tab Opened. Detected Role: '{role}'");

        // 2. Activate the correct panel based on role
        if (role == "student")
        {
            // Turn ON Student, Turn OFF Professor
            if (studentPanel) studentPanel.SetActive(true);
            if (professorPanel) professorPanel.SetActive(false);
        }
        else
        {
            // Turn ON Professor, Turn OFF Student
            if (professorPanel) professorPanel.SetActive(true);
            if (studentPanel) studentPanel.SetActive(false);
        }
    }
}