using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using NL2SQL.Models;

namespace NL2SQL.Services;

/// <summary>
/// 数据库表结构读取器
/// </summary>
public class SchemaReader
{
    /// <summary>
    /// 表信息
    /// </summary>
    public record TableInfo(string TableName, string? Comment, List<ColumnInfo> Columns);

    /// <summary>
    /// 列信息
    /// </summary>
    public record ColumnInfo(string Name, string DataType, bool IsNullable, bool IsPrimaryKey, string? Comment);

    private readonly DatabaseDialect _dialect;
    private readonly string _connectionString;

    public SchemaReader(DatabaseDialect dialect, string connectionString)
    {
        _dialect = dialect;
        _connectionString = connectionString;
    }

    /// <summary>
    /// 创建数据库连接
    /// </summary>
    private DbConnection CreateConnection() => _dialect switch
    {
        DatabaseDialect.MySQL => new MySqlConnection(_connectionString),
        DatabaseDialect.Oracle => new OracleConnection(_connectionString),
        DatabaseDialect.SqlServer => new SqlConnection(_connectionString),
        DatabaseDialect.PostgreSQL => new NpgsqlConnection(_connectionString),
        DatabaseDialect.SQLite => new SqliteConnection(_connectionString),
        _ => throw new NotSupportedException($"不支持的数据库类型: {_dialect}")
    };

    /// <summary>
    /// 读取所有表结构，生成 DDL 描述
    /// </summary>
    public async Task<string> ReadSchemaAsync()
    {
        var tables = await ReadTablesAsync();
        return FormatSchema(tables);
    }

    /// <summary>
    /// 读取所有表信息
    /// </summary>
    public async Task<List<TableInfo>> ReadTablesAsync()
    {
        return _dialect switch
        {
            DatabaseDialect.MySQL => await ReadMySqlSchemaAsync(),
            DatabaseDialect.Oracle => await ReadOracleSchemaAsync(),
            DatabaseDialect.SqlServer => await ReadSqlServerSchemaAsync(),
            DatabaseDialect.PostgreSQL => await ReadPostgreSqlSchemaAsync(),
            DatabaseDialect.SQLite => await ReadSqliteSchemaAsync(),
            _ => throw new NotSupportedException($"不支持的数据库类型: {_dialect}")
        };
    }

    /// <summary>
    /// 将表结构格式化为可读的 DDL 描述
    /// </summary>
    private string FormatSchema(List<TableInfo> tables)
    {
        using var sw = new StringWriter();

        foreach (var table in tables)
        {
            sw.WriteLine($"-- 表: {table.TableName}{(table.Comment != null ? $" ({table.Comment})" : "")}");
            sw.WriteLine($"CREATE TABLE {table.TableName} (");

            var lines = new List<string>();
            var pkColumns = new List<string>();

            foreach (var col in table.Columns)
            {
                var parts = new List<string>
                {
                    $"  {col.Name}",
                    col.DataType,
                    col.IsNullable ? "NULL" : "NOT NULL"
                };

                if (!string.IsNullOrEmpty(col.Comment))
                    parts.Add($"-- {col.Comment}");

                lines.Add(string.Join(" ", parts));

                if (col.IsPrimaryKey)
                    pkColumns.Add(col.Name);
            }

            if (pkColumns.Count > 0)
                lines.Add($"  PRIMARY KEY ({string.Join(", ", pkColumns)})");

            sw.WriteLine(string.Join(",\n", lines));
            sw.WriteLine(");\n");
        }

        return sw.ToString();
    }

    #region MySQL

    private async Task<List<TableInfo>> ReadMySqlSchemaAsync()
    {
        var tables = new List<TableInfo>();

        await using var conn = new MySqlConnection(_connectionString);
        await conn.OpenAsync();

        // 获取当前数据库的所有表
        var tableCmd = conn.CreateCommand();
        tableCmd.CommandText = """
            SELECT TABLE_NAME, TABLE_COMMENT
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'
            ORDER BY TABLE_NAME
            """;

        await using var tableReader = await tableCmd.ExecuteReaderAsync();
        var tableNames = new List<(string Name, string? Comment)>();
        while (await tableReader.ReadAsync())
            tableNames.Add((tableReader.GetString(0), tableReader.IsDBNull(1) ? null : tableReader.GetString(1)));
        await tableReader.CloseAsync();

        foreach (var (tableName, tableComment) in tableNames)
        {
            var columns = new List<ColumnInfo>();

            var colCmd = conn.CreateCommand();
            colCmd.CommandText = """
                SELECT
                    c.COLUMN_NAME,
                    c.COLUMN_TYPE,
                    c.IS_NULLABLE,
                    c.COLUMN_COMMENT,
                    CASE WHEN k.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                        AND tc.TABLE_SCHEMA = ku.TABLE_SCHEMA
                    WHERE tc.TABLE_SCHEMA = DATABASE()
                        AND tc.TABLE_NAME = @table
                        AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) k ON c.COLUMN_NAME = k.COLUMN_NAME
                WHERE c.TABLE_SCHEMA = DATABASE()
                    AND c.TABLE_NAME = @table
                ORDER BY c.ORDINAL_POSITION
                """;
            colCmd.Parameters.AddWithValue("@table", tableName);

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                columns.Add(new ColumnInfo(
                    colReader.GetString(0),      // COLUMN_NAME
                    colReader.GetString(1),      // COLUMN_TYPE
                    colReader.GetString(2) == "YES",  // IS_NULLABLE
                    colReader.GetInt32(4) == 1,       // IS_PK
                    colReader.IsDBNull(3) ? null : colReader.GetString(3)  // COMMENT
                ));
            }

            tables.Add(new TableInfo(tableName, tableComment, columns));
        }

        return tables;
    }

    #endregion

    #region Oracle

    private async Task<List<TableInfo>> ReadOracleSchemaAsync()
    {
        var tables = new List<TableInfo>();

        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        // 获取当前用户的表
        var tableCmd = conn.CreateCommand();
        tableCmd.CommandText = """
            SELECT t.TABLE_NAME, c.COMMENTS
            FROM USER_TABLES t
            LEFT JOIN USER_TAB_COMMENTS c ON t.TABLE_NAME = c.TABLE_NAME
            ORDER BY t.TABLE_NAME
            """;

        await using var tableReader = await tableCmd.ExecuteReaderAsync();
        var tableNames = new List<(string Name, string? Comment)>();
        while (await tableReader.ReadAsync())
            tableNames.Add((tableReader.GetString(0), tableReader.IsDBNull(1) ? null : tableReader.GetString(1)));
        await tableReader.CloseAsync();

        foreach (var (tableName, tableComment) in tableNames)
        {
            var columns = new List<ColumnInfo>();

            var colCmd = conn.CreateCommand();
            colCmd.CommandText = """
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE || CASE
                        WHEN c.DATA_TYPE IN ('VARCHAR2','CHAR','NVARCHAR2','NCHAR')
                            THEN '(' || c.DATA_LENGTH || ')'
                        WHEN c.DATA_TYPE = 'NUMBER' AND c.DATA_PRECISION IS NOT NULL
                            THEN '(' || c.DATA_PRECISION || CASE WHEN c.DATA_SCALE > 0 THEN ',' || c.DATA_SCALE ELSE '' END || ')'
                        ELSE ''
                    END AS DATA_TYPE,
                    c.NULLABLE,
                    cc.COMMENTS,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
                FROM USER_TAB_COLUMNS c
                LEFT JOIN USER_COL_COMMENTS cc
                    ON c.TABLE_NAME = cc.TABLE_NAME AND c.COLUMN_NAME = cc.COLUMN_NAME
                LEFT JOIN (
                    SELECT acc.COLUMN_NAME
                    FROM USER_CONSTRAINTS ac
                    JOIN USER_CONS_COLUMNS acc ON ac.CONSTRAINT_NAME = acc.CONSTRAINT_NAME
                    WHERE ac.TABLE_NAME = :tbl AND ac.CONSTRAINT_TYPE = 'P'
                ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                WHERE c.TABLE_NAME = :tbl
                ORDER BY c.COLUMN_ID
                """;
            colCmd.Parameters.Add("tbl", tableName);

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                columns.Add(new ColumnInfo(
                    colReader.GetString(0),
                    colReader.GetString(1),
                    colReader.GetString(2) == "Y",
                    colReader.GetInt32(4) == 1,
                    colReader.IsDBNull(3) ? null : colReader.GetString(3)
                ));
            }

            tables.Add(new TableInfo(tableName, tableComment, columns));
        }

        return tables;
    }

    #endregion

    #region SqlServer

    private async Task<List<TableInfo>> ReadSqlServerSchemaAsync()
    {
        var tables = new List<TableInfo>();

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        var tableCmd = conn.CreateCommand();
        tableCmd.CommandText = """
            SELECT
                t.TABLE_NAME,
                ep.value AS TABLE_COMMENT
            FROM INFORMATION_SCHEMA.TABLES t
            LEFT JOIN sys.extended_properties ep
                ON ep.major_id = OBJECT_ID(t.TABLE_SCHEMA + '.' + t.TABLE_NAME)
                AND ep.minor_id = 0
                AND ep.name = 'MS_Description'
            WHERE t.TABLE_TYPE = 'BASE TABLE'
            ORDER BY t.TABLE_NAME
            """;

        await using var tableReader = await tableCmd.ExecuteReaderAsync();
        var tableNames = new List<(string Name, string? Comment)>();
        while (await tableReader.ReadAsync())
            tableNames.Add((tableReader.GetString(0), tableReader.IsDBNull(1) ? null : tableReader.GetValue(1)?.ToString()));
        await tableReader.CloseAsync();

        foreach (var (tableName, tableComment) in tableNames)
        {
            var columns = new List<ColumnInfo>();

            var colCmd = conn.CreateCommand();
            colCmd.CommandText = """
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE +
                        CASE
                            WHEN c.DATA_TYPE IN ('varchar','char','nvarchar','nchar')
                                THEN '(' + CASE WHEN c.CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CAST(c.CHARACTER_MAXIMUM_LENGTH AS VARCHAR) END + ')'
                            WHEN c.DATA_TYPE = 'decimal'
                                THEN '(' + CAST(c.NUMERIC_PRECISION AS VARCHAR) + ',' + CAST(c.NUMERIC_SCALE AS VARCHAR) + ')'
                            ELSE ''
                        END AS DATA_TYPE,
                    c.IS_NULLABLE,
                    ep.value AS COLUMN_COMMENT,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN sys.extended_properties ep
                    ON ep.major_id = OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME)
                    AND ep.minor_id = c.ORDINAL_POSITION
                    AND ep.name = 'MS_Description'
                LEFT JOIN (
                    SELECT ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    WHERE tc.TABLE_NAME = @table AND tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                WHERE c.TABLE_NAME = @table
                ORDER BY c.ORDINAL_POSITION
                """;
            colCmd.Parameters.AddWithValue("@table", tableName);

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                columns.Add(new ColumnInfo(
                    colReader.GetString(0),
                    colReader.GetString(1),
                    colReader.GetString(2) == "YES",
                    colReader.GetInt32(4) == 1,
                    colReader.IsDBNull(3) ? null : colReader.GetValue(3)?.ToString()
                ));
            }

            tables.Add(new TableInfo(tableName, tableComment, columns));
        }

        return tables;
    }

    #endregion

    #region PostgreSQL

    private async Task<List<TableInfo>> ReadPostgreSqlSchemaAsync()
    {
        var tables = new List<TableInfo>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var tableCmd = conn.CreateCommand();
        tableCmd.CommandText = """
            SELECT
                t.table_name,
                obj_description((t.table_schema || '.' || t.table_name)::regclass, 'pg_class') AS table_comment
            FROM information_schema.tables t
            WHERE t.table_schema = 'public' AND t.table_type = 'BASE TABLE'
            ORDER BY t.table_name
            """;

        await using var tableReader = await tableCmd.ExecuteReaderAsync();
        var tableNames = new List<(string Name, string? Comment)>();
        while (await tableReader.ReadAsync())
            tableNames.Add((tableReader.GetString(0), tableReader.IsDBNull(1) ? null : tableReader.GetString(1)));
        await tableReader.CloseAsync();

        foreach (var (tableName, tableComment) in tableNames)
        {
            var columns = new List<ColumnInfo>();

            var colCmd = conn.CreateCommand();
            colCmd.CommandText = """
                SELECT
                    c.column_name,
                    c.data_type ||
                        CASE
                            WHEN c.data_type IN ('character varying','character')
                                THEN '(' || c.character_maximum_length || ')'
                            WHEN c.data_type = 'numeric'
                                THEN '(' || c.numeric_precision || ',' || c.numeric_scale || ')'
                            ELSE ''
                        END AS data_type,
                    c.is_nullable,
                    col_description((c.table_schema || '.' || c.table_name)::regclass, c.ordinal_position) AS column_comment,
                    CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS is_pk
                FROM information_schema.columns c
                LEFT JOIN (
                    SELECT ku.column_name
                    FROM information_schema.table_constraints tc
                    JOIN information_schema.key_column_usage ku
                        ON tc.constraint_name = ku.constraint_name
                    WHERE tc.table_name = @table AND tc.constraint_type = 'PRIMARY KEY'
                ) pk ON c.column_name = pk.column_name
                WHERE c.table_schema = 'public' AND c.table_name = @table
                ORDER BY c.ordinal_position
                """;
            colCmd.Parameters.AddWithValue("@table", tableName);

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                columns.Add(new ColumnInfo(
                    colReader.GetString(0),
                    colReader.GetString(1),
                    colReader.GetString(2) == "YES",
                    colReader.GetBoolean(3),
                    colReader.IsDBNull(3) ? null : colReader.GetString(3)
                ));
            }

            tables.Add(new TableInfo(tableName, tableComment, columns));
        }

        return tables;
    }

    #endregion

    #region SQLite

    private async Task<List<TableInfo>> ReadSqliteSchemaAsync()
    {
        var tables = new List<TableInfo>();

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        var tableCmd = conn.CreateCommand();
        tableCmd.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";

        await using var tableReader = await tableCmd.ExecuteReaderAsync();
        var tableNames = new List<string>();
        while (await tableReader.ReadAsync())
            tableNames.Add(tableReader.GetString(0));
        await tableReader.CloseAsync();

        foreach (var tableName in tableNames)
        {
            var columns = new List<ColumnInfo>();

            var colCmd = conn.CreateCommand();
            colCmd.CommandText = $"PRAGMA table_info('{tableName}')";

            await using var colReader = await colCmd.ExecuteReaderAsync();
            while (await colReader.ReadAsync())
            {
                columns.Add(new ColumnInfo(
                    colReader.GetString(1),         // name
                    colReader.GetString(2),         // type
                    colReader.GetInt32(3) == 0,     // notnull (0 = nullable)
                    colReader.GetInt32(5) == 1,     // pk
                    null
                ));
            }

            tables.Add(new TableInfo(tableName, null, columns));
        }

        return tables;
    }

    #endregion

    /// <summary>
    /// 根据方言生成连接字符串示例
    /// </summary>
    public static string GetConnectionStringExample(DatabaseDialect dialect) => dialect switch
    {
        DatabaseDialect.MySQL => "Server=localhost;Port=3306;Database=mydb;Uid=root;Pwd=password;",
        DatabaseDialect.Oracle => "Data Source=localhost:1521/ORCL;User Id=system;Password=password;",
        DatabaseDialect.SqlServer => "Server=localhost;Database=mydb;Trusted_Connection=True;TrustServerCertificate=True;",
        DatabaseDialect.PostgreSQL => "Host=localhost;Port=5432;Database=mydb;Username=postgres;Password=password;",
        DatabaseDialect.SQLite => "Data Source=mydb.db;",
        _ => ""
    };
}
