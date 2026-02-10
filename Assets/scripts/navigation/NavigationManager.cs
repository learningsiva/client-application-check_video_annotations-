using UnityEngine;
using UnityEngine.UI;

public class NavigationManager : MonoBehaviour
{
    [Header("Navigation Buttons")]
    public Button homeButton;
    public Button discoveryButton;
    public Button libraryButton;
    public Button profileButton;

    [Header("Panels")]
    public GameObject homePanel;
    public GameObject discoveryPanel;
    public GameObject libraryPanel;
    public GameObject profilePanel;

    [Header("Home Button Sprites")]
    public Sprite homeNormalSprite;
    public Sprite homeHighlightedSprite;
    public Sprite homePressedSprite;

    [Header("Discovery Button Sprites")]
    public Sprite discoveryNormalSprite;
    public Sprite discoveryHighlightedSprite;
    public Sprite discoveryPressedSprite;

    [Header("Library Button Sprites")]
    public Sprite libraryNormalSprite;
    public Sprite libraryHighlightedSprite;
    public Sprite libraryPressedSprite;

    [Header("Profile Button Sprites")]
    public Sprite profileNormalSprite;
    public Sprite profileHighlightedSprite;
    public Sprite profilePressedSprite;

    private Button currentSelectedButton;

    void Start()
    {
        
        SetupButtonSprites(homeButton, homeNormalSprite, homeHighlightedSprite, homePressedSprite);
        SetupButtonSprites(discoveryButton, discoveryNormalSprite, discoveryHighlightedSprite, discoveryPressedSprite);
        SetupButtonSprites(libraryButton, libraryNormalSprite, libraryHighlightedSprite, libraryPressedSprite);
        SetupButtonSprites(profileButton, profileNormalSprite, profileHighlightedSprite, profilePressedSprite);

        // Add listeners to buttons
        homeButton.onClick.AddListener(() => OnNavigationButtonClicked(homeButton, homePanel));
        discoveryButton.onClick.AddListener(() => OnNavigationButtonClicked(discoveryButton, discoveryPanel));
        libraryButton.onClick.AddListener(() => OnNavigationButtonClicked(libraryButton, libraryPanel));
        profileButton.onClick.AddListener(() => OnNavigationButtonClicked(profileButton, profilePanel));

        // Open home panel by default (home button stays in selected/pressed state)
        OnNavigationButtonClicked(homeButton, homePanel);
    }

    void SetupButtonSprites(Button button, Sprite normal, Sprite highlighted, Sprite pressed)
    {
        if (button != null && button.image != null)
        {
            button.image.sprite = normal;
            SpriteState spriteState = new SpriteState
            {
                highlightedSprite = highlighted,
                pressedSprite = pressed,
                selectedSprite = pressed,
                disabledSprite = normal
            };
            button.spriteState = spriteState;
            button.transition = Selectable.Transition.SpriteSwap;
        }
    }

    void OnNavigationButtonClicked(Button clickedButton, GameObject panelToOpen)
    {
        // Reset previous button to normal state
        if (currentSelectedButton != null)
        {
            ResetButtonToNormal(currentSelectedButton);
        }

        // Close all panels
        CloseAllPanels();

        // Open selected panel
        if (panelToOpen != null)
        {
            panelToOpen.SetActive(true);
        }

        // Set clicked button to selected/pressed state
        Sprite pressedSprite = GetPressedSprite(clickedButton);
        SetButtonToSelected(clickedButton, pressedSprite);
        currentSelectedButton = clickedButton;
    }

    Sprite GetPressedSprite(Button button)
    {
        if (button == homeButton) return homePressedSprite;
        if (button == discoveryButton) return discoveryPressedSprite;
        if (button == libraryButton) return libraryPressedSprite;
        if (button == profileButton) return profilePressedSprite;
        return null;
    }

    void ResetButtonToNormal(Button button)
    {
        // Determine which button it is and set its normal sprite
        if (button == homeButton && button.image != null)
            button.image.sprite = homeNormalSprite;
        else if (button == discoveryButton && button.image != null)
            button.image.sprite = discoveryNormalSprite;
        else if (button == libraryButton && button.image != null)
            button.image.sprite = libraryNormalSprite;
        else if (button == profileButton && button.image != null)
            button.image.sprite = profileNormalSprite;
    }

    void SetButtonToSelected(Button button, Sprite selectedSprite)
    {
        if (button != null && button.image != null)
        {
            button.image.sprite = selectedSprite;
        }
    }

    void CloseAllPanels()
    {
        if (homePanel != null) homePanel.SetActive(false);
        if (discoveryPanel != null) discoveryPanel.SetActive(false);
        if (libraryPanel != null) libraryPanel.SetActive(false);
        if (profilePanel != null) profilePanel.SetActive(false);
    }


}