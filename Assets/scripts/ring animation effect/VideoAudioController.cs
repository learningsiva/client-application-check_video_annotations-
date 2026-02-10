using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class VideoAudioController : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public Button toggleAudioButton;
    public Image buttonIcon; 

    [Header("Icons (Optional)")]
    public Sprite soundOnSprite;
    public Sprite soundOffSprite;

    private bool isMuted = false;

    private void Start()
    {
        if (toggleAudioButton != null)
        {
            toggleAudioButton.onClick.AddListener(OnToggleAudioClicked);
        }

        // Initialize state (Unmuted by default)
        UpdateAudioState();
        UpdateUI();
    }

    private void OnToggleAudioClicked()
    {
        isMuted = !isMuted;
        UpdateAudioState();
        UpdateUI();
    }

    private void UpdateAudioState()
    {
        if (videoPlayer == null) return;

        // METHOD 1: If VideoPlayer is sending audio directly to hardware (Default)
        if (videoPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
        {
            // Track 0 is usually the main audio track
            for (ushort i = 0; i < videoPlayer.audioTrackCount; i++)
            {
                videoPlayer.SetDirectAudioMute(i, isMuted);
            }
        }
        // METHOD 2: If VideoPlayer is sending audio to an AudioSource component
        else if (videoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource)
        {
            AudioSource source = videoPlayer.GetTargetAudioSource(0);
            if (source != null)
            {
                source.mute = isMuted;
            }
        }
    }

    private void UpdateUI()
    {
        // Only try to swap sprites if they are assigned
        if (buttonIcon != null && soundOnSprite != null && soundOffSprite != null)
        {
            buttonIcon.sprite = isMuted ? soundOffSprite : soundOnSprite;
        }
    }
}