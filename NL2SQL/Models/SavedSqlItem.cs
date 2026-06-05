using System.Text.Json;

namespace NL2SQL.Models;

/// <summary>
/// 常用 SQL 项
/// </summary>
public class SavedSqlItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Name { get; set; } = "";
    public string Sql { get; set; } = "";
    public string Category { get; set; } = "默认";
    public string Database { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// 常用 SQL 管理器
/// </summary>
public class SavedSqlManager
{
    private readonly string _filePath;
    private List<SavedSqlItem> _items = new();

    public SavedSqlManager(string filePath = "saved_sql.json")
    {
        _filePath = filePath;
        Load();
    }

    public List<SavedSqlItem> GetAll() => _items.OrderBy(x => x.Category).ThenByDescending(x => x.CreatedAt).ToList();

    public List<SavedSqlItem> GetByCategory(string category)
        => _items.Where(x => x.Category == category).OrderByDescending(x => x.CreatedAt).ToList();

    public List<string> GetCategories() => _items.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();

    public void Add(SavedSqlItem item)
    {
        _items.Add(item);
        Save();
    }

    public void Update(SavedSqlItem item)
    {
        var index = _items.FindIndex(x => x.Id == item.Id);
        if (index >= 0)
        {
            _items[index] = item;
            Save();
        }
    }

    public void Remove(string id)
    {
        _items.RemoveAll(x => x.Id == id);
        Save();
    }

    private void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            var json = File.ReadAllText(_filePath);
            _items = JsonSerializer.Deserialize<List<SavedSqlItem>>(json) ?? new();
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
