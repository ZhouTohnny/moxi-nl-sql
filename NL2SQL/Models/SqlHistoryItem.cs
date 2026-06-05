using System.Text.Json;

namespace NL2SQL.Models;

/// <summary>
/// SQL 历史记录项
/// </summary>
public class SqlHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string NaturalLanguage { get; set; } = "";
    public string Sql { get; set; } = "";
    public string Database { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// SQL 历史记录管理
/// </summary>
public class SqlHistoryManager
{
    private readonly string _filePath;
    private List<SqlHistoryItem> _items = new();

    public SqlHistoryManager(string filePath = "sql_history.json")
    {
        _filePath = filePath;
        Load();
    }

    public List<SqlHistoryItem> GetAll() => _items.OrderByDescending(x => x.CreatedAt).ToList();

    public void Add(SqlHistoryItem item)
    {
        _items.Add(item);
        Save();
    }

    public void Remove(string id)
    {
        _items.RemoveAll(x => x.Id == id);
        Save();
    }

    public void Clear()
    {
        _items.Clear();
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            _items = JsonSerializer.Deserialize<List<SqlHistoryItem>>(json) ?? new();
        }
        catch
        {
            _items = new();
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_items, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
