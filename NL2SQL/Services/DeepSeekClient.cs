using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NL2SQL.Services;

/// <summary>
/// DeepSeek API 客户端
/// </summary>
public class DeepSeekClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public DeepSeekClient(string apiKey, string baseUrl = "https://api.deepseek.com", string model = "deepseek-chat")
    {
        _model = model;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    /// <summary>
    /// 单轮对话
    /// </summary>
    public async Task<string> ChatAsync(string systemPrompt, string userMessage)
    {
        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage }
        };
        return await SendChatAsync(messages);
    }

    /// <summary>
    /// 多轮对话
    /// </summary>
    public async Task<string> ChatAsync(string systemPrompt, List<(string Role, string Content)> history)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };
        foreach (var (role, content) in history)
        {
            messages.Add(new { role, content });
        }
        return await SendChatAsync(messages.ToArray());
    }

    private async Task<string> SendChatAsync(object[] messages)
    {
        var request = new
        {
            model = _model,
            messages,
            temperature = 0.1
        };

        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, JsonOptions);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
            ?? throw new InvalidOperationException("API 返回为空");
    }

    #region API 响应模型

    private record ChatResponse(
        [property: JsonPropertyName("choices")] List<Choice>? Choices
    );

    private record Choice(
        [property: JsonPropertyName("message")] Message? Message
    );

    private record Message(
        [property: JsonPropertyName("content")] string? Content
    );

    #endregion
}
