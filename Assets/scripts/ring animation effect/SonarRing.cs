using UnityEngine;
using UnityEngine.UI;

public class SonarRing : MonoBehaviour
{
    [Header("Settings")]
    public float lifetime = 1.0f;       // How long the ripple lasts
    public float finalScale = 3.0f;     // How big it gets
    public Color fadeColor;             // Target color (usually transparent)

    private float timer = 0f;
    private Vector3 initialScale;
    private Image img;
    private Color initialColor;

    void Awake()
    {
        img = GetComponent<Image>();
        initialScale = transform.localScale;
        initialColor = img.color;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= lifetime)
        {
            Destroy(gameObject); // Cleanup when done
            return;
        }

        // Calculate progress from 0.0 to 1.0
        float progress = timer / lifetime;

        // ANIMATION MATH:
        // We use "Mathf.Sin" here to give it a nice "Ease-Out" feel 
        // (starts fast, slows down at the end) rather than a boring linear expansion.
        float easedProgress = Mathf.Sin(progress * (Mathf.PI * 0.5f));

        // 1. Expand the Scale
        transform.localScale = Vector3.Lerp(initialScale, initialScale * finalScale, easedProgress);

        // 2. Fade the Alpha
        img.color = Color.Lerp(initialColor, fadeColor, easedProgress);
    }
}