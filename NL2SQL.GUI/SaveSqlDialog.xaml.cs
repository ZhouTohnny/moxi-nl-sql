using System.Windows;
using NL2SQL.Models;

namespace NL2SQL.GUI;

public partial class SaveSqlDialog : Window
{
    private readonly SavedSqlManager _manager;
    private readonly string _sql;
    private readonly string _database;

    public bool IsSaved { get; private set; }

    public SaveSqlDialog(SavedSqlManager manager, string sql, string database)
    {
        InitializeComponent();
        _manager = manager;
        _sql = sql;
        _database = database;

        // 加载已有分类
        var categories = manager.GetCategories();
        if (categories.Count == 0)
            categories.Add("默认");
        cmbCategory.ItemsSource = categories;
        cmbCategory.SelectedIndex = 0;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var name = txtName.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("请输入名称。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var category = cmbCategory.Text.Trim();
        if (string.IsNullOrEmpty(category))
            category = "默认";

        _manager.Add(new SavedSqlItem
        {
            Name = name,
            Sql = _sql,
            Category = category,
            Database = _database
        });

        IsSaved = true;
        MessageBox.Show("保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
