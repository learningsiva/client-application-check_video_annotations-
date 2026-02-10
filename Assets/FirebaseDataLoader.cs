using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;


public class FirebaseDataLoader : MonoBehaviour
{
    /*[Header("Firebase Settings")]
    public string firebaseDatabaseURL = "https://videoframesposition-default-rtdb.asia-southeast1.firebasedatabase.app/";

   
    public IEnumerator LoadAnnotationsBySessionId(string sessionId, Action<List<AnnotationData>> onComplete, Action<string> onError)
    {
        Debug.Log($"🔥 Loading annotations for session: {sessionId}");

        string encodedSessionId = UnityWebRequest.EscapeURL($"\"{sessionId}\"");
        string url = $"{firebaseDatabaseURL.TrimEnd('/')}/frames.json?orderBy=\"sessionId\"&equalTo={encodedSessionId}";

        Debug.Log($"🌐 Query URL: {url}");

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 30;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"Failed to load data: {request.error}";
                Debug.LogError($"❌ {error}");
                Debug.LogError($"Response Code: {request.responseCode}");
                onError?.Invoke(error);
                yield break;
            }

            string jsonResponse = request.downloadHandler.text;
            Debug.Log($"📥 Firebase Response ({jsonResponse.Length} chars):");
            Debug.Log(jsonResponse);

            if (string.IsNullOrEmpty(jsonResponse) || jsonResponse == "null")
            {
                Debug.LogWarning("⚠️ No annotations found for this session");
                onComplete?.Invoke(new List<AnnotationData>());
                yield break;
            }

            List<AnnotationData> annotations = ParseFirebaseResponse(jsonResponse);
            annotations.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));

            Debug.Log($"✅ Successfully loaded {annotations.Count} annotations");
            onComplete?.Invoke(annotations);
        }
    }

    private List<AnnotationData> ParseFirebaseResponse(string json)
    {
        List<AnnotationData> result = new List<AnnotationData>();

        try
        {
            // Remove outer braces
            json = json.Trim();
            if (json.StartsWith("{")) json = json.Substring(1);
            if (json.EndsWith("}")) json = json.Substring(0, json.Length - 1);

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("Empty JSON after trimming");
                return result;
            }

            // Split into individual frame entries
            List<string> frameEntries = SplitIntoFrameEntries(json);
            Debug.Log($"📦 Found {frameEntries.Count} frame entries");

            foreach (string entry in frameEntries)
            {
                try
                {
                    AnnotationData annotation = ParseFrameEntry(entry);
                    if (annotation != null)
                    {
                        result.Add(annotation);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"⚠️ Could not parse frame entry: {e.Message}");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error in ParseFirebaseResponse: {e.Message}");
        }

        return result;
    }

    private AnnotationData ParseFrameEntry(string entry)
    {
        try
        {
            // Extract frame ID
            int colonIndex = entry.IndexOf("\":{");
            if (colonIndex == -1)
            {
                Debug.LogWarning("Invalid entry format - no '\":{'");
                return null;
            }

            string frameId = entry.Substring(0, colonIndex).Trim('"');

            // Get the JSON object part
            int objStart = entry.IndexOf("{", colonIndex);
            string frameJson = entry.Substring(objStart);

            // Ensure it ends with }
            if (!frameJson.TrimEnd().EndsWith("}"))
            {
                frameJson = frameJson.TrimEnd().TrimEnd(',') + "}";
            }

            Debug.Log($"🔍 Parsing frame: {frameId}");

            // Create annotation object
            AnnotationData annotation = new AnnotationData();
            annotation.id = frameId;
      // annotation.sessionId = ExtractStringValue(frameJson, "sessionId");
            annotation.timestamp = ExtractFloatValue(frameJson, "timestamp");

            // Parse bounding box
            annotation.boundingBox = new AnnotationData.BoundingBoxData();
            string boundingBoxJson = ExtractObjectValue(frameJson, "boundingBox");
            if (!string.IsNullOrEmpty(boundingBoxJson))
            {
                annotation.boundingBox.normalizedX = ExtractFloatValue(boundingBoxJson, "normalizedX");
                annotation.boundingBox.normalizedY = ExtractFloatValue(boundingBoxJson, "normalizedY");
            }

            // Parse annotations array
            annotation.annotations = ExtractAnnotationsArray(frameJson);

            // Log the result
            Debug.Log($"✅ Frame {frameId} parsed:");
            Debug.Log($"   ⏱️  Timestamp: {annotation.timestamp}s");
            Debug.Log($"   📍 Position: ({annotation.boundingBox.normalizedX:F3}, {annotation.boundingBox.normalizedY:F3})");

            if (annotation.annotations != null && annotation.annotations.Length > 0)
            {
                Debug.Log($"   📝 {annotation.annotations.Length} annotations:");
                for (int i = 0; i < annotation.annotations.Length; i++)
                {
                    Debug.Log($"      {i + 1}. {annotation.annotations[i]}");
                }
            }
            else
            {
                Debug.LogWarning($"   ⚠️ No annotations found");
            }

            return annotation;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error parsing frame entry: {e.Message}");
            return null;
        }
    }

    private string[] ExtractAnnotationsArray(string json)
    {
        List<string> annotations = new List<string>();

        try
        {
            // Find "annotations" key
            int annotationsIndex = json.IndexOf("\"annotations\"");
            if (annotationsIndex == -1)
            {
                Debug.LogWarning("'annotations' key not found");
                return annotations.ToArray();
            }

            // Find the colon after "annotations"
            int colonIndex = json.IndexOf(":", annotationsIndex);
            if (colonIndex == -1) return annotations.ToArray();

            // Skip whitespace
            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
            {
                valueStart++;
            }

            if (valueStart >= json.Length) return annotations.ToArray();

            char firstChar = json[valueStart];

            // Handle array format: ["item1", "item2"]
            if (firstChar == '[')
            {
                int arrayEnd = json.IndexOf("]", valueStart);
                if (arrayEnd == -1)
                {
                    Debug.LogWarning("No closing bracket found for annotations array");
                    return annotations.ToArray();
                }

                string arrayContent = json.Substring(valueStart + 1, arrayEnd - valueStart - 1);
                Debug.Log($"📋 Array content: {arrayContent}");

                // Extract quoted strings from array
                Regex regex = new Regex("\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"");
                MatchCollection matches = regex.Matches(arrayContent);

                foreach (Match match in matches)
                {
                    string value = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        annotations.Add(value);
                    }
                }
            }
            // Handle object format: {"0": "item1", "1": "item2"}
            else if (firstChar == '{')
            {
                int objEnd = FindMatchingBrace(json, valueStart);
                if (objEnd == -1)
                {
                    Debug.LogWarning("No closing brace found for annotations object");
                    return annotations.ToArray();
                }

                string objContent = json.Substring(valueStart + 1, objEnd - valueStart - 1);
                Debug.Log($"📦 Object content: {objContent}");

                // Extract values from object
                Regex regex = new Regex("\"\\d+\"\\s*:\\s*\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"");
                MatchCollection matches = regex.Matches(objContent);

                foreach (Match match in matches)
                {
                    string value = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(value))
                    {
                        annotations.Add(value);
                    }
                }
            }

            Debug.Log($"✅ Extracted {annotations.Count} annotation strings");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error extracting annotations: {e.Message}");
        }

        return annotations.ToArray();
    }

    private List<string> SplitIntoFrameEntries(string json)
    {
        List<string> entries = new List<string>();
        int depth = 0;
        int startIdx = 0;
        bool inString = false;
        bool escapeNext = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
            }

            if (!inString)
            {
                if (c == '{')
                {
                    if (depth == 0)
                    {
                        startIdx = i - 1;
                        // Find the start of the key
                        while (startIdx > 0 && json[startIdx] != '"' && json[startIdx] != ',')
                        {
                            startIdx--;
                        }
                        if (json[startIdx] == ',') startIdx++;
                        while (startIdx < json.Length && char.IsWhiteSpace(json[startIdx]))
                        {
                            startIdx++;
                        }
                    }
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string entry = json.Substring(startIdx, i - startIdx + 1).Trim();
                        if (!string.IsNullOrEmpty(entry))
                        {
                            entries.Add(entry);
                        }
                    }
                }
            }
        }

        return entries;
    }

    private string ExtractStringValue(string json, string key)
    {
        string pattern = $"\"{key}\"\\s*:\\s*\"";
        Match match = Regex.Match(json, pattern);
        if (!match.Success) return "";

        int start = match.Index + match.Length;
        int end = json.IndexOf("\"", start);
        if (end == -1) return "";

        return json.Substring(start, end - start);
    }

    private float ExtractFloatValue(string json, string key)
    {
        string pattern = $"\"{key}\"\\s*:\\s*([0-9.]+)";
        Match match = Regex.Match(json, pattern);
        if (!match.Success) return 0f;

        float value;
        if (float.TryParse(match.Groups[1].Value, out value))
            return value;

        return 0f;
    }

    private string ExtractObjectValue(string json, string key)
    {
        string pattern = $"\"{key}\"\\s*:\\s*{{";
        Match match = Regex.Match(json, pattern);
        if (!match.Success) return "";

        int start = json.IndexOf("{", match.Index + key.Length);
        if (start == -1) return "";

        int end = FindMatchingBrace(json, start);
        if (end == -1) return "";

        return json.Substring(start, end - start + 1);
    }

    private int FindMatchingBrace(string json, int startIndex)
    {
        int depth = 0;
        bool inString = false;
        bool escapeNext = false;

        for (int i = startIndex; i < json.Length; i++)
        {
            char c = json[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
            }

            if (!inString)
            {
                if (c == '{') depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
        }

        return -1;
    }*/
}