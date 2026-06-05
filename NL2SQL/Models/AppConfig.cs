using System.Text.Json;

namespace NL2SQL.Models;

/// <summary>
/// 应用配置
/// </summary>
public class AppConfig
{
    public List<ModelConfig> Models { get; set; } = new();
    public string ActiveModel { get; set; } = "";
    public List<ConnectionConfig> Connections { get; set; } = new();

    /// <summary>
    /// 获取当前激活的模型配置
    /// </summary>
    public ModelConfig? GetActiveModel()
        => Models.FirstOrDefault(m => m.Name == ActiveModel) ?? Models.FirstOrDefault();

    /// <summary>
    /// 从配置文件加载
    /// </summary>
    public static AppConfig Load(string path = "appsettings.json")
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"配置文件不存在: {path}", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("配置文件解析失败");
    }

    /// <summary>
    /// 保存配置
    /// </summary>
    public void Save(string path = "appsettings.json")
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 获取指定名称的连接配置
    /// </summary>
    public ConnectionConfig? GetConnection(string name)
        => Connections.FirstOrDefault(c => c.Name == name);
}

/// <summary>
/// 模型配置
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// 模型名称（如 "DeepSeek"、"GPT-4o"）
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// API 类型：OpenAI、Anthropic
    /// </summary>
    public string ApiType { get; set; } = "OpenAI";

    /// <summary>
    /// API Key
    /// </summary>
    public string ApiKey { get; set; } = "";

    /// <summary>
    /// API 地址
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    /// <summary>
    /// 模型名称（如 deepseek-chat、claude-3-5-sonnet）
    /// </summary>
    public string Model { get; set; } = "deepseek-chat";
}

/// <summary>
/// 数据库连接配置
/// </summary>
public class ConnectionConfig
{
    public string Name { get; set; } = "";
    public string Dialect { get; set; } = "";
    public string ConnectionString { get; set; } = "";
}
