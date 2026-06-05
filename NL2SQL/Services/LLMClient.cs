using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NL2SQL.Services;

/// <summary>
/// LLM 客户端接口
/// </summary>
public interface ILLMClient
{
    Task<string> ChatAsync(string systemPrompt, string userMessage);
    Task<string> ChatAsync(string systemPrompt, List<(string Role, string Content)> history);
}

/// <summary>
/// LLM 客户端工厂
/// </summary>
public static class LLMClientFactory
{
    public static ILLMClient Create(string apiType, string apiKey, string baseUrl, string model)
    {
        return apiType.ToLower() switch
        {
            "anthropic" => new AnthropicClient(apiKey, baseUrl, model),
            _ => new OpenAIClient(apiKey, baseUrl, model)
        };
    }
}

/// <summary>
/// OpenAI 兼容客户端（支持 DeepSeek、GPT、Moonshot 等）
/// </summary>
public class OpenAIClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public OpenAIClient(string apiKey, string baseUrl, string model)
    {
        _model = model;
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage)
    {
        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage }
        };
        return await SendRequestAsync(messages);
    }

    public async Task<string> ChatAsync(string systemPrompt, List<(string Role, string Content)> history)
    {
        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        foreach (var (role, content) in history)
            messages.Add(new { role, content });
        return await SendRequestAsync(messages.ToArray());
    }

    private async Task<string> SendRequestAsync(object[] messages)
    {
        var request = new { model = _model, messages, temperature = 0.1 };
        var response = await _httpClient.PostAsJsonAsync("/v1/chat/completions", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(JsonOptions);
        return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
            ?? throw new InvalidOperationException("API 返回为空");
    }

    private record ChatResponse(List<Choice>? Choices);
    private record Choice(Message? Message);
    private record Message(string? Content);
}

/// <summary>
/// Anthropic Claude 客户端
/// </summary>
public class AnthropicClient : ILLMClient
{
    private readonly HttpClient _httpClient;
    private readonly string _model;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AnthropicClient(string apiKey, string baseUrl, string model)
    {
        _model = model;
        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> ChatAsync(string systemPrompt, string userMessage)
    {
        var request = new
        {
            model = _model,
            max_tokens = 4096,
            system = systemPrompt,
            messages = new[] { new { role = "user", content = userMessage } }
        };
        return await SendRequestAsync(request);
    }

    public async Task<string> ChatAsync(string systemPrompt, List<(string Role, string Content)> history)
    {
        var messages = history.Select(h => new { role = h.Role, content = h.Content }).ToArray();
        var request = new
        {
            model = _model,
            max_tokens = 4096,
            system = systemPrompt,
            messages
        };
        return await SendRequestAsync(request);
    }

    private async Task<string> SendRequestAsync(object request)
    {
        var response = await _httpClient.PostAsJsonAsync("/v1/messages", request, JsonOptions);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AnthropicResponse>(JsonOptions);

        // 提取文本内容
        var textBlock = result?.Content?.FirstOrDefault(c => c.Type == "text");
        return textBlock?.Text?.Trim()
            ?? throw new InvalidOperationException("API 返回为空");
    }

    private record AnthropicResponse(List<ContentBlock>? Content);
    private record ContentBlock(string? Type, string? Text);
}
