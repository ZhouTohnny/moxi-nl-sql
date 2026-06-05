namespace NL2SQL.Models;

/// <summary>
/// 数据库方言枚举
/// </summary>
public enum DatabaseDialect
{
    MySQL,
    Oracle,
    SqlServer,
    PostgreSQL,
    SQLite
}

/// <summary>
/// 数据库方言配置
/// </summary>
public static class DialectConfig
{
    private static readonly Dictionary<DatabaseDialect, string> DialectPrompts = new()
    {
        [DatabaseDialect.MySQL] = """
            你是一个 MySQL SQL 专家。请生成 MySQL 兼容的 SQL 语句。
            注意：
            - 使用反引号(`)引用标识符
            - 分页使用 LIMIT offset, count
            - 字符串使用单引号
            - 支持 IFNULL、GROUP_CONCAT 等 MySQL 特有函数
            """,

        [DatabaseDialect.Oracle] = """
            你是一个 Oracle SQL 专家。请生成 Oracle 兼容的 SQL 语句。
            注意：
            - 使用双引号(")引用标识符
            - 分页使用 ROWNUM 或 FETCH FIRST ... ROWS ONLY（Oracle 12c+）
            - 字符串使用单引号
            - 使用 NVL 代替 IFNULL，LISTAGG 代替 GROUP_CONCAT
            - 序列使用 sequence.NEXTVAL
            """,

        [DatabaseDialect.SqlServer] = """
            你是一个 SQL Server 专家。请生成 SQL Server 兼容的 SQL 语句。
            注意：
            - 使用方括号([])引用标识符
            - 分页使用 OFFSET ... FETCH NEXT ... ROWS ONLY
            - 字符串使用单引号
            - 使用 ISNULL 代替 IFNULL
            - 使用 TOP 或 FETCH 进行行数限制
            """,

        [DatabaseDialect.PostgreSQL] = """
            你是一个 PostgreSQL 专家。请生成 PostgreSQL 兼容的 SQL 语句。
            注意：
            - 使用双引号(")引用标识符
            - 分页使用 LIMIT ... OFFSET
            - 字符串使用单引号
            - 支持丰富的数据类型和数组操作
            """,

        [DatabaseDialect.SQLite] = """
            你是一个 SQLite 专家。请生成 SQLite 兼容的 SQL 语句。
            注意：
            - 使用双引号(")引用标识符
            - 分页使用 LIMIT ... OFFSET
            - 字符串使用单引号
            - 数据类型较简单，注意类型亲和性
            """
    };

    public static string GetDialectPrompt(DatabaseDialect dialect)
        => DialectPrompts.TryGetValue(dialect, out var prompt) ? prompt : "";

    public static string[] GetAvailableDialects()
        => Enum.GetNames<DatabaseDialect>();

    public static DatabaseDialect Parse(string name)
        => Enum.Parse<DatabaseDialect>(name, ignoreCase: true);
}
