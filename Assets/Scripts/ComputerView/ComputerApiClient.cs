using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

public sealed class ComputerApiException : Exception
{
    public long StatusCode { get; }

    public ComputerApiException(long statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public sealed class ComputerApiClient
{
    private readonly string baseUrl;

    public string Token { get; set; }

    public ComputerApiClient(string baseUrl, string token)
    {
        this.baseUrl = (string.IsNullOrWhiteSpace(baseUrl) ? "http://127.0.0.1:8765" : baseUrl).TrimEnd('/');
        Token = token;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            await SendAsync<JObject>("/health", UnityWebRequest.kHttpVerbGET, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<ComputerAuthResponse> RegisterAsync(string name, string email, string password)
    {
        return SendAsync<ComputerAuthResponse>(
            "/api/auth/register",
            UnityWebRequest.kHttpVerbPOST,
            new { name, email, password });
    }

    public Task<ComputerAuthResponse> LoginAsync(string email, string password)
    {
        return SendAsync<ComputerAuthResponse>(
            "/api/auth/login",
            UnityWebRequest.kHttpVerbPOST,
            new { email, password });
    }

    public Task<ComputerMeResponse> MeAsync()
    {
        return SendAsync<ComputerMeResponse>("/api/me", UnityWebRequest.kHttpVerbGET, null);
    }

    public Task<ComputerGameResponse> GenerateGameAsync()
    {
        return SendAsync<ComputerGameResponse>("/api/game/generate", UnityWebRequest.kHttpVerbPOST, new { });
    }

    public Task<ComputerGameResponse> GetGameAsync(string gameId)
    {
        return SendAsync<ComputerGameResponse>($"/api/game/{UnityWebRequest.EscapeURL(gameId)}", UnityWebRequest.kHttpVerbGET, null);
    }

    public Task<ComputerGameResponse> SendActionAsync(string gameId, string surface, string itemId, string choice, string customText = null)
    {
        return SendAsync<ComputerGameResponse>(
            $"/api/game/{UnityWebRequest.EscapeURL(gameId)}/action",
            UnityWebRequest.kHttpVerbPOST,
            new
            {
                surface,
                item_id = itemId,
                choice,
                custom_text = customText
            });
    }

    public Task<ComputerGameResponse> TickAsync(string gameId)
    {
        return SendAsync<ComputerGameResponse>($"/api/game/{UnityWebRequest.EscapeURL(gameId)}/tick", UnityWebRequest.kHttpVerbPOST, new { });
    }

    private async Task<T> SendAsync<T>(string path, string method, object body)
    {
        using (var request = new UnityWebRequest($"{baseUrl}{path}", method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Accept", "application/json");

            if (!string.IsNullOrWhiteSpace(Token))
            {
                request.SetRequestHeader("Authorization", $"Bearer {Token}");
            }

            if (body != null)
            {
                string json = JsonConvert.SerializeObject(body);
                byte[] payload = Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(payload);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            string responseText = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
            if (request.result != UnityWebRequest.Result.Success)
            {
                throw new ComputerApiException(request.responseCode, ExtractErrorMessage(responseText, request.error));
            }

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return default;
            }

            return JsonConvert.DeserializeObject<T>(responseText);
        }
    }

    private static string ExtractErrorMessage(string responseText, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(responseText))
        {
            try
            {
                JObject data = JObject.Parse(responseText);
                JToken detail = data["detail"];
                if (detail != null)
                {
                    return detail.Type == JTokenType.String ? detail.Value<string>() : detail.ToString(Formatting.None);
                }
            }
            catch
            {
                return responseText;
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Request failed" : fallback;
    }
}
