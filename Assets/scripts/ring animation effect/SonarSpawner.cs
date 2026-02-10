using UnityEngine;

public class SonarSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject ringPrefab;  // Drag your SonarRing prefab here

    [Header("Timing")]
    public float spawnRate = 1.0f; // How often to pulse (seconds)

    private float timer;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnRate)
        {
            SpawnPulse();
            timer = 0f;
        }
    }

    public void SpawnPulse()
    {
        // Instantiate the ring as a child of this object so it stays centered
        GameObject newRing = Instantiate(ringPrefab, transform.position, Quaternion.identity, transform);

        // Ensure it's behind the center dot (if the dot is also a child, use SetSiblingIndex)
        newRing.transform.SetAsFirstSibling();
    }
}