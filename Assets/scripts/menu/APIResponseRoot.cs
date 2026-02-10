using System;
using System.Collections.Generic;

[Serializable]
public class APIResponseRoot
{
    public string message;
    public ResponseData data;
}

[Serializable]
public class ResponseData
{
    public int user_id;
    public string videos; 
}


[Serializable]
public class VideoItem
{
    public int task_id;
    public string title;
    public string subject;
    public string duration;
    public string video_url;
    public List<AnnotationItem> annotations;
    public UserData user_data;

    // 🔥 NEW: Add these two lines
    public bool is_liked;
    public int likes_count;
    public bool is_saved;
}

// 🔥 NEW: Define the User Data Structure
[Serializable]
public class UserData
{
    public string first_name;
    public string last_name;
    public string phone_number;
    public string email;
    public string designation;
    public string tag_line;
    public string profile_pic;
}

[Serializable]
public class AnnotationItem
{
    public float timestamp;
    public float bbox_x;
    public float bbox_y;
    public AnnotationContent content;
}

[Serializable]
public class AnnotationContent
{
    public string heading;
    public string body;
}

public static class JsonHelper
{
    public static List<T> FromJson<T>(string json)
    {
        Wrapper<T> wrapper = UnityEngine.JsonUtility.FromJson<Wrapper<T>>("{\"Items\":" + json + "}");
        return wrapper.Items;
    }

    [Serializable]
    private class Wrapper<T>
    {
        public List<T> Items;
    }
}