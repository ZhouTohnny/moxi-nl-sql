using System.Data;
using System.Text;
using System.Windows;

namespace NL2SQL.GUI;

public partial class ResultWindow : Window
{
    private readonly DataTable _dataTable;

    public ResultWindow(DataTable dataTable, string title = "查询结果")
    {
        InitializeComponent();
        _dataTable = dataTable;

        Title = title;
        txtInfo.Text = $"{dataTable.Rows.Count} 行，{dataTable.Columns.Count} 列";
        dgResult.ItemsSource = dataTable.DefaultView;

        // 自动调整窗口宽度，根据列数
        var width = Math.Min(1600, Math.Max(600, dataTable.Columns.Count * 120 + 100));
        Width = width;

        // 自动调整窗口高度，根据行数
        var height = Math.Min(900, Math.Max(400, dataTable.Rows.Count * 28 + 150));
        Height = height;
    }

    private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();

            // 表头
            var headers = _dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName);
            sb.AppendLine(string.Join("\t", headers));

            // 数据行
            foreach (DataRow row in _dataTable.Rows)
            {
                var values = row.ItemArray.Select(v => v?.ToString() ?? "");
                sb.AppendLine(string.Join("\t", values));
            }

            Clipboard.SetText(sb.ToString());
            MessageBox.Show($"已复制 {_dataTable.Rows.Count} 行数据到剪贴板！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
