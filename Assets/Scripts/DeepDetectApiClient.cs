using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class DeepDetectUser
{
    public int id;
    public string name;
    public string email;
}

[Serializable]
public class DeepDetectAuthResponse
{
    public string token;
    public DeepDetectUser user;
}

[Serializable]
public class DeepDetectGameResponse
{
    public DeepDetectGameState game;
}

[Serializable]
public class DeepDetectGamesResponse
{
    public DeepDetectGameSummary[] games;
}

[Serializable]
public class DeepDetectGameSummary
{
    public string id;
    public string title;
    public int score;
    public bool complete;
    public int world_tick;
    public string agent_model;
    public DeepDetectProgress progress;
}

[Serializable]
public class DeepDetectProgress
{
    public DeepDetectProgressPart news;
    public DeepDetectProgressPart email;
    public DeepDetectProgressPart telegram;
}

[Serializable]
public class DeepDetectProgressPart
{
    public int done;
    public int total;
}

[Serializable]
public class DeepDetectGameState
{
    public string id;
    public string title;
    public int score;
    public bool complete;
    public int world_tick;
    public string agent_mode;
    public string agent_model;
    public DeepDetectGoal[] goals;
    public DeepDetectQuest[] quests;
    public DeepDetectNewsItem[] news_items;
    public DeepDetectThreadItem[] emails;
    public DeepDetectThreadItem[] telegram_threads;
    public string[] world_feed;
    public string[] action_log;
}

[Serializable]
public class DeepDetectGoal
{
    public string id;
    public string title;
    public int target;
    public int current;
    public bool complete;
}

[Serializable]
public class DeepDetectQuest
{
    public string id;
    public string type;
    public string title;
    public string description;
    public int target;
    public int current;
    public string reward;
    public bool complete;
}

[Serializable]
public class DeepDetectNewsItem
{
    public string id;
    public string title;
    public string summary;
    public string source;
    public string public_pressure;
    public string editor_note;
    public string truth_label;
    public string decision;
    public bool correct;
    public int points;
    public string agent_response;
    public string agent_reason;
}

[Serializable]
public class DeepDetectThreadItem
{
    public string id;
    public string from_name;
    public string from_email;
    public string subject;
    public string body;
    public string contact;
    public string relationship;
    public string selected;
    public bool resolved;
    public bool correct;
    public int chat_turns;
    public int min_turns;
    public int max_turns;
    public string agent_response;
    public string agent_reason;
    public DeepDetectMessage[] messages;
    public DeepDetectOption[] options;
}

[Serializable]
public class DeepDetectMessage
{
    public string sender;
    public string text;
    public string role;
    public string at;
}

[Serializable]
public class DeepDetectOption
{
    public string id;
    public string label;
    public string rationale;
}

public class DeepDetectApiClient : MonoBehaviour
{
    [SerializeField] private string baseUrl = "http://127.0.0.1:8765";

    public string BaseUrl
    {
        get => baseUrl.TrimEnd('/');
        set => baseUrl = string.IsNullOrWhiteSpace(value) ? "http://127.0.0.1:8765" : value.TrimEnd('/');
    }

    public string Token { get; private set; }

    public IEnumerator Health(Action<string> onSuccess, Action<string> onError)
    {
        yield return SendRaw("/health", "GET", null, null, onSuccess, onError);
    }

    public IEnumerator Register(string playerName, string email, string password, Action<DeepDetectAuthResponse> onSuccess, Action<string> onError)
    {
        string body = "{\"name\":\"" + Escape(playerName) + "\",\"email\":\"" + Escape(email) + "\",\"password\":\"" + Escape(password) + "\"}";
        yield return SendJson<DeepDetectAuthResponse>("/api/auth/register", "POST", body, null, response =>
        {
            Token = response.token;
            onSuccess?.Invoke(response);
        }, onError);
    }

    public IEnumerator Login(string email, string password, Action<DeepDetectAuthResponse> onSuccess, Action<string> onError)
    {
        string body = "{\"email\":\"" + Escape(email) + "\",\"password\":\"" + Escape(password) + "\"}";
        yield return SendJson<DeepDetectAuthResponse>("/api/auth/login", "POST", body, null, response =>
        {
            Token = response.token;
            onSuccess?.Invoke(response);
        }, onError);
    }

    public IEnumerator LoadGames(Action<DeepDetectGamesResponse> onSuccess, Action<string> onError)
    {
        yield return SendJson("/api/games", "GET", null, Token, onSuccess, onError);
    }

    public IEnumerator LoadGame(string gameId, Action<DeepDetectGameResponse> onSuccess, Action<string> onError)
    {
        yield return SendJson("/api/game/" + UnityWebRequest.EscapeURL(gameId), "GET", null, Token, onSuccess, onError);
    }

    public IEnumerator GenerateGame(Action<DeepDetectGameResponse> onSuccess, Action<string> onError)
    {
        yield return SendJson("/api/game/generate", "POST", "{}", Token, onSuccess, onError);
    }

    public IEnumerator AdvanceWorld(string gameId, Action<DeepDetectGameResponse> onSuccess, Action<string> onError)
    {
        yield return SendJson("/api/game/" + UnityWebRequest.EscapeURL(gameId) + "/tick", "POST", "{}", Token, onSuccess, onError);
    }

    public IEnumerator SubmitAction(string gameId, string surface, string itemId, string choice, string customText, Action<DeepDetectGameResponse> onSuccess, Action<string> onError)
    {
        string body = "{\"surface\":\"" + Escape(surface) + "\",\"item_id\":\"" + Escape(itemId) + "\",\"choice\":\"" + Escape(choice) + "\"";
        if (!string.IsNullOrWhiteSpace(customText))
            body += ",\"custom_text\":\"" + Escape(customText) + "\"";
        body += "}";
        yield return SendJson("/api/game/" + UnityWebRequest.EscapeURL(gameId) + "/action", "POST", body, Token, onSuccess, onError);
    }

    private IEnumerator SendJson<T>(string path, string method, string body, string token, Action<T> onSuccess, Action<string> onError)
    {
        yield return SendRaw(path, method, body, token, json =>
        {
            try
            {
                onSuccess?.Invoke(JsonUtility.FromJson<T>(json));
            }
            catch (Exception ex)
            {
                onError?.Invoke("Could not parse backend response: " + ex.Message);
            }
        }, onError);
    }

    private IEnumerator SendRaw(string path, string method, string body, string token, Action<string> onSuccess, Action<string> onError)
    {
        using UnityWebRequest request = new UnityWebRequest(BaseUrl + path, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Accept", "application/json");
        if (!string.IsNullOrEmpty(token))
            request.SetRequestHeader("Authorization", "Bearer " + token);

        if (!string.IsNullOrEmpty(body))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            request.uploadHandler = new UploadHandlerRaw(bytes);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        yield return request.SendWebRequest();

        string text = request.downloadHandler != null ? request.downloadHandler.text : "";
        if (request.result == UnityWebRequest.Result.Success)
        {
            onSuccess?.Invoke(text);
            yield break;
        }

        onError?.Invoke("HTTP " + request.responseCode + ": " + (string.IsNullOrWhiteSpace(text) ? request.error : text));
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}
