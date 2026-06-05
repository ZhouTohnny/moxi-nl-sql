using System.Windows;
using System.Windows.Input;
using NL2SQL.Models;

namespace NL2SQL.GUI;

public partial class HistoryWindow : Window
{
    public string? SelectedSql { get; private set; }
    public bool ShouldClear { get; private set; }

    private readonly List<SqlHistoryItem> _history;

    public HistoryWindow(List<SqlHistoryItem> history)
    {
        InitializeComponent();
        _history = history;
        lstHistory.ItemsSource = history;
    }

    private void LstHistory_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lstHistory.SelectedItem is SqlHistoryItem item)
        {
            SelectedSql = item.Sql;
            Close();
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定清空所有历史记录？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ShouldClear = true;
            lstHistory.ItemsSource = null;
            MessageBox.Show("历史记录已清空。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
