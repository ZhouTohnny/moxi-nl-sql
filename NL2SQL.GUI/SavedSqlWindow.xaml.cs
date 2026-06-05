using System.Windows;
using System.Windows.Controls;
using NL2SQL.Models;

namespace NL2SQL.GUI;

public partial class SavedSqlWindow : Window
{
    private readonly SavedSqlManager _manager;
    private List<SavedSqlItem> _allItems = new();

    public string? SelectedSql { get; private set; }

    public SavedSqlWindow(SavedSqlManager manager)
    {
        InitializeComponent();
        _manager = manager;
        LoadData();
    }

    private void LoadData()
    {
        _allItems = _manager.GetAll();

        var categories = _manager.GetCategories();
        categories.Insert(0, "全部");
        lstCategories.ItemsSource = categories;
        lstCategories.SelectedIndex = 0;
    }

    private void LstCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (lstCategories.SelectedItem is string category)
        {
            lstSavedSql.ItemsSource = category == "全部"
                ? _allItems
                : _allItems.Where(x => x.Category == category).ToList();
        }
    }

    private void LstSavedSql_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 选中时不做操作，让用户通过 Expander 箭头展开/折叠
    }

    private void BtnUse_Click(object sender, RoutedEventArgs e)
    {
        UseSelected();
    }

    private void UseSelected()
    {
        if (lstSavedSql.SelectedItem is SavedSqlItem item)
        {
            SelectedSql = item.Sql;
            Close();
        }
        else
        {
            MessageBox.Show("请先选择一个常用 SQL。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (lstSavedSql.SelectedItem is not SavedSqlItem item)
        {
            MessageBox.Show("请先选择要删除的 SQL。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定删除「{item.Name}」？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _manager.Remove(item.Id);
            LoadData();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
