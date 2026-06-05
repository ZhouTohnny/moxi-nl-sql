using System.Windows;
using System.Windows.Controls;
using NL2SQL.Models;

namespace NL2SQL.GUI;

public partial class SettingsWindow : Window
{
    private readonly AppConfig _config;
    private List<ModelConfig> _models;
    private List<ConnectionConfig> _connections;
    private int _selectedModelIndex = -1;
    private int _selectedConnIndex = -1;

    public bool IsSaved { get; private set; }

    public SettingsWindow(AppConfig config)
    {
        InitializeComponent();
        _config = config;

        // 复制配置
        _models = config.Models.Select(m => new ModelConfig
        {
            Name = m.Name,
            ApiKey = m.ApiKey,
            BaseUrl = m.BaseUrl,
            Model = m.Model
        }).ToList();

        _connections = config.Connections.Select(c => new ConnectionConfig
        {
            Name = c.Name,
            Dialect = c.Dialect,
            ConnectionString = c.ConnectionString
        }).ToList();

        lstModels.ItemsSource = _models;
        lstConnections.ItemsSource = _connections;

        if (_models.Count > 0)
            lstModels.SelectedIndex = 0;

        if (_connections.Count > 0)
            lstConnections.SelectedIndex = 0;
    }

    #region 模型配置

    private void LstModels_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveCurrentModelEdit();
        _selectedModelIndex = lstModels.SelectedIndex;

        if (_selectedModelIndex >= 0 && _selectedModelIndex < _models.Count)
        {
            var model = _models[_selectedModelIndex];
            txtModelName.Text = model.Name;
            txtModelApiKey.Text = model.ApiKey;
            txtModelBaseUrl.Text = model.BaseUrl;
            txtModelId.Text = model.Model;

            // 选中对应的 API 类型
            for (int i = 0; i < cmbApiType.Items.Count; i++)
            {
                if (cmbApiType.Items[i] is ComboBoxItem item && item.Tag?.ToString() == model.ApiType)
                {
                    cmbApiType.SelectedIndex = i;
                    break;
                }
            }
        }
    }

    private void SaveCurrentModelEdit()
    {
        if (_selectedModelIndex >= 0 && _selectedModelIndex < _models.Count)
        {
            _models[_selectedModelIndex].Name = txtModelName.Text.Trim();
            _models[_selectedModelIndex].ApiKey = txtModelApiKey.Text.Trim();
            _models[_selectedModelIndex].BaseUrl = txtModelBaseUrl.Text.Trim();
            _models[_selectedModelIndex].Model = txtModelId.Text.Trim();
            _models[_selectedModelIndex].ApiType = (cmbApiType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "OpenAI";

            lstModels.ItemsSource = null;
            lstModels.ItemsSource = _models;
        }
    }

    private void BtnAddModel_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentModelEdit();

        var newModel = new ModelConfig
        {
            Name = $"模型_{_models.Count + 1}",
            BaseUrl = "https://api.deepseek.com",
            Model = "deepseek-chat"
        };
        _models.Add(newModel);

        lstModels.ItemsSource = null;
        lstModels.ItemsSource = _models;
        lstModels.SelectedIndex = _models.Count - 1;
    }

    private void BtnDeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedModelIndex < 0 || _selectedModelIndex >= _models.Count)
        {
            MessageBox.Show("请先选择要删除的模型。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定删除模型「{_models[_selectedModelIndex].Name}」？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _models.RemoveAt(_selectedModelIndex);
            _selectedModelIndex = -1;

            lstModels.ItemsSource = null;
            lstModels.ItemsSource = _models;

            txtModelName.Text = "";
            txtModelApiKey.Text = "";
            txtModelBaseUrl.Text = "";
            txtModelId.Text = "";

            if (_models.Count > 0)
                lstModels.SelectedIndex = 0;
        }
    }

    #endregion

    #region 数据库连接配置

    private void LstConnections_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SaveCurrentConnEdit();
        _selectedConnIndex = lstConnections.SelectedIndex;

        if (_selectedConnIndex >= 0 && _selectedConnIndex < _connections.Count)
        {
            var conn = _connections[_selectedConnIndex];
            txtConnName.Text = conn.Name;

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

    private void SaveCurrentConnEdit()
    {
        if (_selectedConnIndex >= 0 && _selectedConnIndex < _connections.Count)
        {
            _connections[_selectedConnIndex].Name = txtConnName.Text.Trim();
            _connections[_selectedConnIndex].Dialect = (cmbDialect.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "MySQL";
            _connections[_selectedConnIndex].ConnectionString = txtConnStr.Text.Trim();

            lstConnections.ItemsSource = null;
            lstConnections.ItemsSource = _connections;
        }
    }

    private void BtnAddConn_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentConnEdit();

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

    private void BtnDeleteConn_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedConnIndex < 0 || _selectedConnIndex >= _connections.Count)
        {
            MessageBox.Show("请先选择要删除的连接。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"确定删除连接「{_connections[_selectedConnIndex].Name}」？", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _connections.RemoveAt(_selectedConnIndex);
            _selectedConnIndex = -1;

            lstConnections.ItemsSource = null;
            lstConnections.ItemsSource = _connections;

            txtConnName.Text = "";
            txtConnStr.Text = "";

            if (_connections.Count > 0)
                lstConnections.SelectedIndex = 0;
        }
    }

    #endregion

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentModelEdit();
        SaveCurrentConnEdit();

        // 验证
        foreach (var model in _models)
        {
            if (string.IsNullOrWhiteSpace(model.Name))
            {
                MessageBox.Show("模型名称不能为空。", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

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
        _config.Models = _models;
        _config.Connections = _connections;

        // 如果当前激活的模型被删除了，设置为第一个
        if (!_models.Any(m => m.Name == _config.ActiveModel) && _models.Count > 0)
            _config.ActiveModel = _models[0].Name;

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
