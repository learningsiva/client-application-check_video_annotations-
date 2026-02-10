using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CommentItemUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text userNameText;
    public TMP_Text commentText;

    // 🔥 CHANGED: Use Text for the first letter instead of an Image
    public TMP_Text avatarLetterText;

    // Optional: Drag the circular background Image here to change its color
    public Image avatarBackgroundImage;

    public void Setup(string name, string comment)
    {
        // 1. Set Name and Comment
        if (userNameText) userNameText.text = name;
        if (commentText) commentText.text = comment;

        // 2. 🔥 Set First Letter
        if (avatarLetterText)
        {
            if (!string.IsNullOrEmpty(name))
            {
                // Extract first char, uppercase it
                avatarLetterText.text = name.Substring(0, 1).ToUpper();
            }
            else
            {
                avatarLetterText.text = "U"; // Default 'U' for Unknown
            }
        }

        // 3. (Optional) Set Consistent Color based on Name
        if (avatarBackgroundImage)
        {
            avatarBackgroundImage.color = GetColorForName(name);
        }
    }

    // Helper: Generates a consistent color for the same user every time
    private Color GetColorForName(string name)
    {
        if (string.IsNullOrEmpty(name)) return Color.gray;

        int hash = name.GetHashCode();

        // List of nice pastel UI colors
        Color[] niceColors = new Color[] {
            new Color32(66, 165, 245, 255), // Blue
            new Color32(239, 83, 80, 255),  // Red
            new Color32(171, 71, 188, 255), // Purple
            new Color32(92, 107, 192, 255), // Indigo
            new Color32(38, 166, 154, 255), // Teal
            new Color32(102, 187, 106, 255), // Green
            new Color32(255, 167, 38, 255), // Orange
            new Color32(141, 110, 99, 255)  // Brown
        };

        // Pick a color based on the hash
        return niceColors[Mathf.Abs(hash) % niceColors.Length];
    }
}