using UnityEngine;
using System;

/*[Serializable]
public class AnnotationData
{
    public float timestamp;
    public BoundingBoxData boundingBox;
    public AnnotationContent content;

    [Serializable]
    public class BoundingBoxData
    {
        public float normalizedX;
        public float normalizedY;
    }

    // Helper to calculate screen position
    public Vector2 GetScreenPosition(RectTransform videoDisplayRect)
    {
        if (videoDisplayRect == null || boundingBox == null) return Vector2.zero;

        Rect rect = videoDisplayRect.rect;
        float rectX = boundingBox.normalizedX * rect.width;
        float rectY = boundingBox.normalizedY * rect.height;
        float localX = rectX - (rect.width / 2f);
        float localY = rectY - (rect.height / 2f);

        return new Vector2(localX, localY);
    }

    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(timestamp / 60f);
        int seconds = Mathf.FloorToInt(timestamp % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}

[Serializable]
public class AnnotationContent
{
    public string heading;
    public string body;
}

// Helpers for API Parsing
[Serializable]
public class OuterApiResponse { public string message; public string response; }
[Serializable]
public class InnerDataResponse { public string annotations_json; }

public static class JsonHelper
{
    public static T[] FromJson<T>(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        if (json.StartsWith("[")) json = "{\"Items\":" + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
        return wrapper.Items;
    }
    [Serializable] private class Wrapper<T> { public T[] Items; }
}*/