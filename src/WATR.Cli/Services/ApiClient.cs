using System.Net.Http.Headers;
using System.Net.Http.Json;
using WATR.Shared.DTOs;
using WATR.Shared.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace WATR.Cli.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly ConfigService _config;
    private readonly TokenStore _tokenStore;

    public ApiClient(ConfigService config, TokenStore tokenStore)
    {
        _config = config;
        _tokenStore = tokenStore;
        
        var configData = _config.Load();
        var serverUrl = configData.ServerUrl;

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            // Allow fallback to global config only if local is missing?
            // User requested: "remove the code that defaults the server url to localhost... check for the settings file ... fail with a message"
            // So we strictly enforce local or explicit config.
            
            // However, existing global config might be used as a fallback for users who haven't migrated?
            // "do not use the one from the user's profile anymore" - explicit instruction.
            
            throw new Exception("Server URL not configured. Please run 'watr init <SERVER_URL>'.");
        }

        if (!serverUrl.EndsWith("/"))
        {
            serverUrl += "/";
        }

        _http = new HttpClient { BaseAddress = new Uri(serverUrl) };
        
        var tokens = _tokenStore.LoadTokens();
        if (tokens != null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }
    }

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path.TrimStart('/'));
        await EnsureSuccess(response);
        var typeInfo = (JsonTypeInfo<T>)AppJsonContext.Default.GetTypeInfo(typeof(T))!;
        return (await response.Content.ReadFromJsonAsync(typeInfo))!;
    }

    public async Task<T> PostAsync<T>(string path, object body)
    {
        var bodyTypeInfo = AppJsonContext.Default.GetTypeInfo(body.GetType());
        if (bodyTypeInfo == null)
        {
             throw new InvalidOperationException($"Type {body.GetType()} is not registered in AppJsonContext.");
        }

        using var jsonContent = JsonContent.Create(body, bodyTypeInfo);
        var response = await _http.PostAsync(path.TrimStart('/'), jsonContent);
        
        await EnsureSuccess(response);
        var typeInfo = (JsonTypeInfo<T>)AppJsonContext.Default.GetTypeInfo(typeof(T))!;
        return (await response.Content.ReadFromJsonAsync(typeInfo))!;
    }

    public async Task PostAsync(string path, object? body = null)
    {
        HttpContent? content = null;
        if (body != null)
        {
            var bodyTypeInfo = AppJsonContext.Default.GetTypeInfo(body.GetType());
            if (bodyTypeInfo == null) 
            {
                 throw new InvalidOperationException($"Type {body.GetType()} is not registered in AppJsonContext.");
            }
            content = JsonContent.Create(body, bodyTypeInfo);
        }
        
        var response = await _http.PostAsync(path.TrimStart('/'), content);
        content?.Dispose();
        await EnsureSuccess(response);
    }

    public async Task PatchAsync(string path, object body)
    {
        var bodyTypeInfo = AppJsonContext.Default.GetTypeInfo(body.GetType());
        if (bodyTypeInfo == null) 
        {
             throw new InvalidOperationException($"Type {body.GetType()} is not registered in AppJsonContext.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Patch, path.TrimStart('/'));
        request.Content = JsonContent.Create(body, bodyTypeInfo);
        var response = await _http.SendAsync(request);
        await EnsureSuccess(response);
    }

    private async Task EnsureSuccess(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var typeInfo = (JsonTypeInfo<ErrorResponse>)AppJsonContext.Default.GetTypeInfo(typeof(ErrorResponse))!;
                var error = await response.Content.ReadFromJsonAsync(typeInfo);
                if (error != null)
                {
                    throw new Exception($"{error.Error}: {error.Message}");
                }
            }
            catch (JsonException) { }

            throw new Exception($"Request failed with status code {response.StatusCode}");
        }
    }
}
