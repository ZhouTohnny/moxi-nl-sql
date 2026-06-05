using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using NL2SQL.Models;

namespace NL2SQL.Services;

/// <summary>
/// SQL 执行器
/// </summary>
public class SqlExecutor
{
    private readonly DatabaseDialect _dialect;
    private readonly string _connectionString;

    /// <summary>
    /// 最大返回行数（0 = 不限制）
    /// </summary>
    public int MaxRows { get; set; } = 1000;

    /// <summary>
    /// 查询超时秒数
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    public SqlExecutor(DatabaseDialect dialect, string connectionString)
    {
        _dialect = dialect;
        _connectionString = connectionString;
    }

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
    /// 移除 SQL 中已有的行数限制
    /// </summary>
    private string RemoveExistingLimits(string sql)
    {
        // 移除 LIMIT n, n OFFSET m
        sql = Regex.Replace(sql, @"\s+LIMIT\s+\d+(\s+OFFSET\s+\d+)?\s*;?\s*$", "", RegexOptions.IgnoreCase);

        // 移除 FETCH FIRST n ROWS ONLY / FETCH NEXT n ROWS ONLY
        sql = Regex.Replace(sql, @"\s+FETCH\s+(FIRST|NEXT)\s+\d+\s+ROWS?\s+ONLY\s*;?\s*$", "", RegexOptions.IgnoreCase);

        // 移除 Oracle 的 ROWNUM <= n (在 WHERE 子句中)
        sql = Regex.Replace(sql, @"\bWHERE\s+ROWNUM\s*<=\s*\d+\s*", "WHERE 1=1 ", RegexOptions.IgnoreCase);
        sql = Regex.Replace(sql, @"\bAND\s+ROWNUM\s*<=\s*\d+\s*", "", RegexOptions.IgnoreCase);

        // 移除 SqlServer 的 TOP n
        sql = Regex.Replace(sql, @"^SELECT\s+TOP\s+\d+\s+", "SELECT ", RegexOptions.IgnoreCase);

        return sql.TrimEnd().TrimEnd(';');
    }

    /// <summary>
    /// 为 SQL 自动添加行数限制
    /// </summary>
    public string ApplyRowLimit(string sql, int maxRows)
    {
        if (maxRows <= 0)
            return sql;

        // 先移除已有的限制
        var cleanSql = RemoveExistingLimits(sql);

        return _dialect switch
        {
            DatabaseDialect.MySQL or DatabaseDialect.PostgreSQL or DatabaseDialect.SQLite
                => $"{cleanSql} LIMIT {maxRows}",

            DatabaseDialect.SqlServer
                => Regex.Replace(cleanSql, @"^SELECT\b", $"SELECT TOP {maxRows}", RegexOptions.IgnoreCase),

            DatabaseDialect.Oracle
                => $"SELECT * FROM ({cleanSql}) WHERE ROWNUM <= {maxRows}",

            _ => cleanSql
        };
    }

    /// <summary>
    /// 执行查询 SQL，返回 DataTable（自动限制行数）
    /// </summary>
    public async Task<(DataTable Data, bool Truncated, int TotalRows, string ExecutedSql)> ExecuteQueryAsync(string sql)
    {
        var finalSql = MaxRows > 0 ? ApplyRowLimit(sql, MaxRows) : sql;

        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = finalSql;
        cmd.CommandTimeout = TimeoutSeconds;

        var dataTable = new DataTable();
        await using var reader = await cmd.ExecuteReaderAsync();
        dataTable.Load(reader);

        return (dataTable, dataTable.Rows.Count >= MaxRows, dataTable.Rows.Count, finalSql);
    }

    /// <summary>
    /// 执行非查询 SQL（INSERT/UPDATE/DELETE），返回影响行数
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync(string sql)
    {
        await using var conn = CreateConnection();
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = TimeoutSeconds;

        return await cmd.ExecuteNonQueryAsync();
    }
}
