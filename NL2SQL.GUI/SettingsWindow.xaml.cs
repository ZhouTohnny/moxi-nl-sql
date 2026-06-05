using System.Windows;
using System.Windows.Controls;
using NL2SQL.Models;

namespace NL2SQL.GUI;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private List<ConnectionConfig> _connections;
    private int _selectedIndex = -1;

    public bool IsSaved { get; private set; }

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;
        _connections = config.Connections.Select(c => new ConnectionConfig
        {
            Name = c.Name,
            Dialect = c.Dialect,
            ConnectionString = c.ConnectionString
        }).ToList();

        txtApiKey.Text = config.DeepSeek.ApiKey;
        lstConnections.ItemsSource = _connections;

        if (_connections.Count > 0)
            lstConnections.SelectedIndex = 0;
    }

    private void LstConnections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 保存当前编辑的内容
        SaveCurrentEdit();

        _selectedIndex = lstConnections.SelectedIndex;
        if (_selectedIndex >= 0 && _selectedIndex < _connections.Count)
        {
            var conn = _connections[_selectedIndex];
            txtName.Text = conn.Name;

            // 选中对应的数据库类型
            for (int i = 0; i < cmbDialect.Items.Count; i++)
            {
                if (cmbDialect.Items[i] is ComboBoxItem item && item.Tag?.ToString() == conn.Dialect)
                {
                    cmbDialect.SelectedIndex = i;
                    break;
                }
            }

            txtConnStr.Text = conn.ConnectionString;
        }
    }

    private void SaveCurrentEdit()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _connections.Count)
        {
            _connections[_selectedIndex].Name = txtName.Text.Trim();
            _connections[_selectedIndex].Dialect = (cmbDialect.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MySQL";
            _connections[_selectedIndex].ConnectionString = txtConnStr.Text.Trim();

            // 刷新列表显示
            lstConnections.ItemsSource = null;
            lstConnections.ItemsSource = _connections;
        }
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEdit();

        var newConn = new ConnectionConfig
        {
            Name = $"连接_{_connections.Count + 1}",
            Dialect = "MySQL",
            ConnectionString = ""
        };
        _connections.Add(newConn);

        lstConnections.ItemsSource = null;
        lstConnections.ItemsSource = _connections;
        lstConnections.SelectedIndex = _connections.Count - 1;
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedIndex < 0 || _selectedIndex >= _connections.Count)
        {
            MessageBox.Show("请先选择要删除的连接。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定删除连接「{_connections[_selectedIndex].Name}」？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _connections.RemoveAt(_selectedIndex);
            _selectedIndex = -1;

            lstConnections.ItemsSource = null;
            lstConnections.ItemsSource = _connections;

            txtName.Text = "";
            txtConnStr.Text = "";

            if (_connections.Count > 0)
                lstConnections.SelectedIndex = 0;
        }
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEdit();

        // 验证
        foreach (var conn in _connections)
        {
            if (string.IsNullOrWhiteSpace(conn.Name))
            {
                MessageBox.Show("连接名称不能为空。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(conn.ConnectionString))
            {
                MessageBox.Show($"连接「{conn.Name}」的连接字符串不能为空。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // 保存
        _config.DeepSeek.ApiKey = txtApiKey.Text.Trim();
        _config.Connections = _connections;

        try
        {
            _config.Save();
            IsSaved = true;
            MessageBox.Show("配置已保存！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
