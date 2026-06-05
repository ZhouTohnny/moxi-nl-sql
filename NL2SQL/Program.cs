using NL2SQL.Models;
using NL2SQL.Services;

// 加载配置文件
AppConfig config;
try
{
    config = AppConfig.Load();
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine("已加载配置文件: appsettings.json");
    Console.ResetColor();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"加载配置文件失败: {ex.Message}");
    Console.ResetColor();
    return;
}

// 解析命令行参数
var apiKey = config.DeepSeek.ApiKey;
string? connectionName = null;
string? schemaFile = null;
string? connectionString = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--name" or "-n" when i + 1 < args.Length:
            connectionName = args[++i];
            break;
        case "--schema" or "-s" when i + 1 < args.Length:
            schemaFile = args[++i];
            break;
        case "--conn" or "-c" when i + 1 < args.Length:
            connectionString = args[++i];
            break;
        case "--api-key" or "-k" when i + 1 < args.Length:
            apiKey = args[++i];
            break;
        case "--list" or "-l":
            ListConnections(config);
            return;
        case "--help" or "-h":
            PrintUsage();
            return;
    }
}

// 从环境变量获取 API Key
var envApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
if (!string.IsNullOrEmpty(envApiKey))
    apiKey = envApiKey;

if (string.IsNullOrEmpty(apiKey))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("错误：请在 appsettings.json 中配置 DeepSeek:ApiKey，或设置 DEEPSEEK_API_KEY 环境变量");
    Console.ResetColor();
    return;
}

// 获取连接配置
ConnectionConfig? connConfig = null;

if (!string.IsNullOrEmpty(connectionString))
{
    // 命令行直接指定连接串
    connConfig = new ConnectionConfig { Name = "命令行", Dialect = "MySQL", ConnectionString = connectionString };
}
else if (!string.IsNullOrEmpty(connectionName))
{
    // 按名称查找
    connConfig = config.GetConnection(connectionName);
    if (connConfig == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"错误：找不到名为「{connectionName}」的连接");
        Console.ResetColor();
        Console.WriteLine("\n可用连接：");
        ListConnections(config);
        return;
    }
}
else if (config.Connections.Count > 0)
{
    // 默认使用第一个连接
    connConfig = config.Connections[0];
}

if (connConfig == null)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine("错误：未配置数据库连接。请在 appsettings.json 中添加连接，或使用 --conn 参数指定。");
    Console.ResetColor();
    return;
}

var dialect = Enum.Parse<DatabaseDialect>(connConfig.Dialect);

// 获取表结构
string? schema = null;

try
{
    Console.ForegroundColor = ConsoleColor.DarkGray;
    Console.WriteLine($"正在连接 [{connConfig.Name}] {dialect} 数据库...");
    Console.ResetColor();

    var reader = new SchemaReader(dialect, connConfig.ConnectionString);
    var tables = await reader.ReadTablesAsync();
    schema = await reader.ReadSchemaAsync();

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"成功读取 {tables.Count} 张表的结构:");
    Console.ResetColor();
    foreach (var table in tables)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  • {table.TableName}");
        Console.ResetColor();
        if (table.Comment != null)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($" ({table.Comment})");
            Console.ResetColor();
        }
        Console.WriteLine($" - {table.Columns.Count} 个字段");
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"连接数据库失败: {ex.Message}");
    Console.ResetColor();

    if (!string.IsNullOrEmpty(schemaFile))
        Console.WriteLine("将尝试从文件读取表结构...");
    else
        Console.WriteLine("提示：你也可以使用 --schema 参数指定表结构文件");
}

if (schema == null && !string.IsNullOrEmpty(schemaFile))
{
    if (!File.Exists(schemaFile))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"错误：找不到表结构文件: {schemaFile}");
        Console.ResetColor();
        return;
    }
    schema = await File.ReadAllTextAsync(schemaFile);
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"已从文件加载表结构: {schemaFile}");
    Console.ResetColor();
}

// 初始化生成器
var generator = new SqlGenerator(dialect, apiKey, schema);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════╗");
Console.WriteLine("║       自然语言转 SQL 工具 (NL2SQL)     ║");
Console.WriteLine("╚══════════════════════════════════════╝");
Console.ResetColor();
Console.WriteLine($"  连接: {connConfig.Name} ({dialect})");
Console.WriteLine();
Console.WriteLine("输入自然语言描述，按回车生成 SQL。输入 'quit' 退出。");
Console.WriteLine(new string('─', 40));

while (true)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.Write("\n> ");
    Console.ResetColor();

    var input = Console.ReadLine()?.Trim();
    if (string.IsNullOrEmpty(input))
        continue;

    if (input is "quit" or "exit")
        break;

    try
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("正在生成...");
        Console.ResetColor();

        var sql = await generator.GenerateAsync(input);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n生成的 SQL:");
        Console.ResetColor();
        Console.WriteLine(new string('─', 40));
        Console.WriteLine(sql);
        Console.WriteLine(new string('─', 40));
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n生成失败: {ex.Message}");
        Console.ResetColor();
    }
}

Console.WriteLine("\n再见！");

static void ListConnections(AppConfig config)
{
    if (config.Connections.Count == 0)
    {
        Console.WriteLine("  暂无配置的连接。");
        return;
    }

    Console.WriteLine("\n已配置的连接：");
    Console.WriteLine(new string('─', 50));
    foreach (var conn in config.Connections)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($"  {conn.Name}");
        Console.ResetColor();
        Console.WriteLine($" ({conn.Dialect})");
    }
    Console.WriteLine(new string('─', 50));
}

static void PrintUsage()
{
    Console.WriteLine("""
        用法: nl2sql [选项]

        选项:
          --name, -n <名称>    使用指定名称的连接（来自配置文件）
          --conn, -c <连接串>  直接指定连接字符串
          --schema, -s <文件>  表结构 SQL 文件路径
          --api-key, -k <密钥> DeepSeek API Key
          --list, -l           列出所有已配置的连接
          --help, -h           显示帮助信息

        示例:
          # 列出所有连接
          nl2sql --list

          # 使用指定名称的连接
          nl2sql --name "Oracle 生产"

          # 直接指定连接串
          nl2sql --conn "Server=localhost;Database=mydb;Uid=root;Pwd=123;"

        配置文件: appsettings.json
          在 Connections 数组中配置多个命名连接
        """);
}
