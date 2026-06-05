using System.Text.Json;

namespace NL2SQL.Models;

/// <summary>
/// 应用配置
/// </summary>
public class AppConfig
{
    public DeepSeekConfig DeepSeek { get; set; } = new();
    public List<ConnectionConfig> Connections { get; set; } = new();

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

    /// <summary>
    /// 获取指定方言的第一个连接
    /// </summary>
    public ConnectionConfig? GetFirstConnection(DatabaseDialect dialect)
        => Connections.FirstOrDefault(c => c.Dialect == dialect.ToString());
}

/// <summary>
/// DeepSeek API 配置
/// </summary>
public class DeepSeekConfig
{
    public string ApiKey { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-chat";
}

/// <summary>
/// 数据库连接配置（支持命名）
/// </summary>
public class ConnectionConfig
{
    /// <summary>
    /// 连接名称，如 "生产环境"、"测试库"
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// 数据库方言：MySQL, Oracle, SqlServer, PostgreSQL, SQLite
    /// </summary>
    public string Dialect { get; set; } = "";

    /// <summary>
    /// 连接字符串
    /// </summary>
    public string ConnectionString { get; set; } = "";
}
