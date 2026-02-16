using System.Net.Http.Headers;
using System.Net.Http.Json;
using HotShallot.Shared.DTOs;
using HotShallot.Shared.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace HotShallot.Cli.Services;

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
        _http = new HttpClient { BaseAddress = new Uri(configData.ServerUrl) };
        
        var tokens = _tokenStore.LoadTokens();
        if (tokens != null)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }
    }

    public async Task<T> GetAsync<T>(string path)
    {
        var response = await _http.GetAsync(path);
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
        var response = await _http.PostAsync(path, jsonContent);
        
        await EnsureSuccess(response);
        var typeInfo = (JsonTypeInfo<T>)AppJsonContext.Default.GetTypeInfo(typeof(T))!;
        return (await response.Content.ReadFromJsonAsync(typeInfo))!;
    }

    public async Task PostAsync(string path, object? body = null)
    {
        var content = body ?? new { };
        var bodyTypeInfo = AppJsonContext.Default.GetTypeInfo(content.GetType());
        if (bodyTypeInfo == null) 
        {
             throw new InvalidOperationException($"Type {content.GetType()} is not registered in AppJsonContext.");
        }
        
        // Use JsonContent.Create which supports JsonTypeInfo (base class)
        using var jsonContent = JsonContent.Create(content, bodyTypeInfo);
        var response = await _http.PostAsync(path, jsonContent);
        await EnsureSuccess(response);
    }

    public async Task PatchAsync(string path, object body)
    {
        var bodyTypeInfo = AppJsonContext.Default.GetTypeInfo(body.GetType());
        if (bodyTypeInfo == null) 
        {
             throw new InvalidOperationException($"Type {body.GetType()} is not registered in AppJsonContext.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Patch, path);
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
