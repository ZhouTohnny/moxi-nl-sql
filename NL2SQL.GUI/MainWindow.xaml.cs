using System.Data;
using System.Windows;
using System.Windows.Input;
using NL2SQL.Models;
using NL2SQL.Services;

namespace NL2SQL.GUI;

public partial class MainWindow : Window
{
    private AppConfig _config = null!;
    private SqlGenerator? _generator;
    private SqlExecutor? _executor;
    private SqlHistoryManager _historyManager = new();
    private SavedSqlManager _savedSqlManager = new();
    private List<SchemaReader.TableInfo> _tables = new();
    private List<SchemaReader.TableInfo> _allTables = new();
    private ConnectionConfig? _currentConnection;

    public record TableItem(string Name, string? Comment)
    {
        public string DisplayText => string.IsNullOrEmpty(Comment) ? Name : $"{Name} ({Comment})";
    }

    public MainWindow()
    {
        InitializeComponent();
        LoadConfig();
    }

    private void LoadConfig()
    {
        try
        {
            _config = AppConfig.Load();

            // 加载模型列表
            cmbModel.ItemsSource = _config.Models;
            var activeModel = _config.GetActiveModel();
            if (activeModel != null)
                cmbModel.SelectedItem = activeModel;

            // 加载连接列表
            cmbConnection.ItemsSource = _config.Connections;
            if (_config.Connections.Count > 0)
                cmbConnection.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载配置文件失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CmbModel_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (cmbModel.SelectedItem is ModelConfig model)
        {
            _config.ActiveModel = model.Name;
            // 如果已连接，需要重新创建 SqlGenerator
            if (_generator != null && _currentConnection != null)
            {
                RecreateGenerator();
            }
        }
    }

    private void CmbConnection_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (txtStatus == null) return;
        _generator = null;
        _executor = null;
        _currentConnection = null;
        _allTables.Clear();
        _tables.Clear();
        lstTables.ItemsSource = null;
        txtStatus.Text = "未连接";
        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 124, 0));
    }

    private int _rowLimit = 500;

    private void CmbRowLimit_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (cmbRowLimit.SelectedItem is System.Windows.Controls.ComboBoxItem item)
        {
            _rowLimit = int.Parse(item.Tag?.ToString() ?? "1000");
            if (_executor != null)
            {
                _executor.MaxRows = _rowLimit;
                UpdateSqlWithRowLimit();
            }
        }
    }

    /// <summary>
    /// 选择行数后，立即更新 SQL 编辑框中的 SQL
    /// </summary>
    private void UpdateSqlWithRowLimit()
    {
        var sql = txtOutput.Text.Trim();
        if (string.IsNullOrEmpty(sql) || sql.StartsWith("--"))
            return;

        var upperSql = sql.ToUpper();
        if (upperSql.StartsWith("SELECT") || upperSql.StartsWith("WITH"))
        {
            txtOutput.Text = _executor!.ApplyRowLimit(sql, _rowLimit);
        }
    }

    /// <summary>
    /// 连接数据库
    /// </summary>
    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (cmbConnection.SelectedItem is not ConnectionConfig conn)
        {
            MessageBox.Show("请先选择一个数据库连接。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialect = Enum.Parse<DatabaseDialect>(conn.Dialect);
        btnConnect.IsEnabled = false;
        txtStatus.Text = "连接中...";
        txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243));

        try
        {
            var reader = new SchemaReader(dialect, conn.ConnectionString);
            _allTables = await reader.ReadTablesAsync();
            var schema = await reader.ReadSchemaAsync();
            _tables = _allTables;

            var model = _config.GetActiveModel();
            if (model == null || string.IsNullOrEmpty(model.ApiKey))
            {
                MessageBox.Show("请先配置模型 API Key。", "配置缺失", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _generator = new SqlGenerator(dialect, model.ApiKey, model.BaseUrl, model.Model, schema, model.ApiType);
            _executor = new SqlExecutor(dialect, conn.ConnectionString) { MaxRows = _rowLimit };
            _currentConnection = conn;

            UpdateTableList();

            txtStatus.Text = $"已连接 ({_allTables.Count} 张表)";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80));
        }
        catch (Exception ex)
        {
            txtStatus.Text = "连接失败";
            txtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244, 67, 54));
            MessageBox.Show($"连接失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnConnect.IsEnabled = true;
        }
    }

    private void UpdateTableList()
    {
        lstTables.ItemsSource = _tables.Select(t => new TableItem(t.TableName, t.Comment)).ToList();
    }

    /// <summary>
    /// 重新创建 SqlGenerator（切换模型时调用）
    /// </summary>
    private void RecreateGenerator()
    {
        if (_currentConnection == null) return;

        var model = _config.GetActiveModel();
        if (model == null) return;

        var dialect = Enum.Parse<DatabaseDialect>(_currentConnection.Dialect);
        var schema = _generator?.GetSchema();

        _generator = new SqlGenerator(dialect, model.ApiKey, model.BaseUrl, model.Model, schema, model.ApiType);
    }

    private void TxtTableSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (txtTableSearch.Text == "🔍 搜索表名...")
        {
            txtTableSearch.Text = "";
            txtTableSearch.Foreground = System.Windows.Media.Brushes.Black;
        }
    }

    private void TxtTableSearch_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtTableSearch.Text))
        {
            txtTableSearch.Text = "🔍 搜索表名...";
            txtTableSearch.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158));
        }
    }

    private void TxtTableSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (txtTableSearch.Text == "🔍 搜索表名..." || _allTables == null) return;

        var keyword = txtTableSearch.Text.Trim().ToUpper();
        if (string.IsNullOrEmpty(keyword))
        {
            _tables = _allTables;
        }
        else
        {
            _tables = _allTables.Where(t =>
                t.TableName.ToUpper().Contains(keyword) ||
                (t.Comment != null && t.Comment.ToUpper().Contains(keyword))
            ).ToList();
        }
        UpdateTableList();
    }

    private void LstTables_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (lstTables.SelectedItem is TableItem item)
        {
            var table = _allTables.FirstOrDefault(t => t.TableName == item.Name);
            if (table != null)
            {
                var info = $"-- 表: {table.TableName}";
                if (table.Comment != null)
                    info += $" ({table.Comment})";
                info += "\n-- 字段:\n";
                foreach (var col in table.Columns)
                {
                    info += $"--   {col.Name} {col.DataType}";
                    if (col.IsPrimaryKey) info += " [PK]";
                    if (!col.IsNullable) info += " NOT NULL";
                    if (col.Comment != null) info += $" -- {col.Comment}";
                    info += "\n";
                }
                txtOutput.Text = info;
            }
        }
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        await GenerateSqlAsync();
    }

    private async void TxtInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            await GenerateSqlAsync();
            e.Handled = true;
        }
    }

    private async Task GenerateSqlAsync()
    {
        var input = txtInput.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show("请输入自然语言描述。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_generator == null)
        {
            MessageBox.Show("请先连接数据库。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        btnGenerate.IsEnabled = false;
        btnGenerate.Content = "生成中...";
        txtOutput.Text = "正在调用 DeepSeek 生成 SQL...";

        try
        {
            string sql;
            if (chkMultiTurn.IsChecked == true)
            {
                sql = await _generator.GenerateWithHistoryAsync(input);
            }
            else
            {
                _generator.ClearHistory();
                sql = await _generator.GenerateAsync(input);
            }

            // 根据当前行数限制，立即应用到生成的 SQL
            if (_executor != null && _rowLimit > 0)
            {
                var upperSql = sql.ToUpper();
                if (upperSql.StartsWith("SELECT") || upperSql.StartsWith("WITH"))
                {
                    sql = _executor.ApplyRowLimit(sql, _rowLimit);
                }
            }

            txtOutput.Text = sql;

            _historyManager.Add(new SqlHistoryItem
            {
                NaturalLanguage = input,
                Sql = sql,
                Database = _currentConnection?.Name ?? ""
            });
        }
        catch (Exception ex)
        {
            txtOutput.Text = $"生成失败: {ex.Message}";
        }
        finally
        {
            btnGenerate.IsEnabled = true;
            btnGenerate.Content = "⚡ 生成 SQL";
        }
    }

    private async void BtnExecute_Click(object sender, RoutedEventArgs e)
    {
        var sql = txtOutput.Text.Trim();
        if (string.IsNullOrEmpty(sql) || sql.StartsWith("--"))
        {
            MessageBox.Show("请先输入 SQL。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_executor == null)
        {
            MessageBox.Show("请先连接数据库。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        btnExecute.IsEnabled = false;
        btnExecute.Content = "执行中...";
        txtResultInfo.Text = "正在执行...";

        try
        {
            var upperSql = sql.TrimStart().ToUpper();
            if (upperSql.StartsWith("SELECT") || upperSql.StartsWith("WITH"))
            {
                var (dataTable, truncated, totalRows, executedSql) = await _executor.ExecuteQueryAsync(sql);

                var info = $"✓ 返回 {dataTable.Rows.Count} 行，{dataTable.Columns.Count} 列";
                if (truncated)
                    info += $"（已限制最多 {_executor.MaxRows} 行）";
                txtResultInfo.Text = info;

                // 将实际执行的 SQL 同步到编辑框
                txtOutput.Text = executedSql;

                // 在新窗口中展示结果
                var resultWindow = new ResultWindow(dataTable, "查询结果") { Owner = this };
                resultWindow.Show();
            }
            else
            {
                // 禁止删除操作
                if (upperSql.StartsWith("DELETE") || upperSql.Contains(" DROP "))
                {
                    MessageBox.Show("安全限制：禁止执行 DELETE 和 DROP 操作！", "警告", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtResultInfo.Text = "✗ 已阻止危险操作";
                    return;
                }

                // INSERT/UPDATE 操作：先预览数据，再确认执行
                if (upperSql.StartsWith("INSERT") || upperSql.StartsWith("UPDATE"))
                {
                    var operation = upperSql.StartsWith("INSERT") ? "插入" : "更新";
                    DataTable? previewData = null;

                    try
                    {
                        if (upperSql.StartsWith("UPDATE"))
                        {
                            // UPDATE：先查询当前数据
                            var tableName = ExtractTableName(sql);
                            var whereClause = ExtractWhereClause(sql);
                            if (!string.IsNullOrEmpty(tableName))
                            {
                                var selectSql = $"SELECT * FROM {tableName}";
                                if (!string.IsNullOrEmpty(whereClause))
                                    selectSql += $" WHERE {whereClause}";

                                var (currentData, _, _, _) = await _executor.ExecuteQueryAsync(selectSql);
                                previewData = currentData;
                            }
                        }
                        else
                        {
                            // INSERT：显示将插入的值
                            previewData = ParseInsertValues(sql);
                        }
                    }
                    catch
                    {
                        // 预览失败不影响执行
                    }

                    var title = operation == "插入" ? "📥 确认插入数据" : "📝 确认更新数据";
                    var description = operation == "插入"
                        ? "以下数据将被插入到数据库："
                        : "以下数据将被更新（当前值 → 新值）：";

                    var confirmWindow = new ConfirmWindow(title, description, sql, previewData) { Owner = this };
                    confirmWindow.ShowDialog();

                    if (!confirmWindow.IsConfirmed)
                    {
                        txtResultInfo.Text = "已取消";
                        return;
                    }

                    var affected = await _executor.ExecuteNonQueryAsync(sql);
                    txtResultInfo.Text = $"✓ {operation}成功，影响 {affected} 行";
                }
                else
                {
                    // 其他操作直接执行
                    var affected = await _executor.ExecuteNonQueryAsync(sql);
                    txtResultInfo.Text = $"✓ 执行成功，影响 {affected} 行";
                }
            }
        }
        catch (Exception ex)
        {
            txtResultInfo.Text = "✗ 执行失败";
            MessageBox.Show($"执行 SQL 失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnExecute.IsEnabled = true;
            btnExecute.Content = "▶ 执行";
        }
    }

    /// <summary>
    /// 从 SQL 中提取表名
    /// </summary>
    private string? ExtractTableName(string sql)
    {
        var upper = sql.ToUpper();
        if (upper.StartsWith("UPDATE"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(sql, @"UPDATE\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        if (upper.StartsWith("INSERT"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(sql, @"INSERT\s+INTO\s+(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }
        return null;
    }

    /// <summary>
    /// 从 SQL 中提取 WHERE 子句
    /// </summary>
    private string? ExtractWhereClause(string sql)
    {
        var match = System.Text.RegularExpressions.Regex.Match(sql, @"\bWHERE\b\s+(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// 解析 INSERT 语句中的值
    /// </summary>
    private DataTable? ParseInsertValues(string sql)
    {
        try
        {
            var dt = new DataTable();

            // 提取列名
            var colMatch = System.Text.RegularExpressions.Regex.Match(sql, @"\(([^)]+)\)\s*VALUES", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!colMatch.Success) return null;

            var columns = colMatch.Groups[1].Value.Split(',').Select(c => c.Trim().Trim('"', '`', '[' ,']'));
            foreach (var col in columns)
                dt.Columns.Add(col);

            // 提取值
            var valMatch = System.Text.RegularExpressions.Regex.Match(sql, @"VALUES\s*\(([^)]+)\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!valMatch.Success) return null;

            var values = valMatch.Groups[1].Value.Split(',').Select(v => v.Trim().Trim('\''));
            var row = dt.NewRow();
            var i = 0;
            foreach (var val in values)
            {
                if (i < dt.Columns.Count)
                    row[i++] = val;
            }
            dt.Rows.Add(row);

            return dt;
        }
        catch
        {
            return null;
        }
    }

    private void BtnHistory_Click(object sender, RoutedEventArgs e)
    {
        var history = _historyManager.GetAll();
        if (history.Count == 0)
        {
            MessageBox.Show("暂无历史记录。", "历史记录", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new HistoryWindow(history) { Owner = this };
        dialog.ShowDialog();

        if (dialog.SelectedSql != null)
            txtOutput.Text = dialog.SelectedSql;

        if (dialog.ShouldClear)
            _historyManager.Clear();
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(txtOutput.Text))
        {
            Clipboard.SetText(txtOutput.Text);
            MessageBox.Show("已复制到剪贴板！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        txtOutput.Text = "";
        txtResultInfo.Text = "";
        if (_generator != null)
            _generator.ClearHistory();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_config) { Owner = this };
        dialog.ShowDialog();

        if (dialog.IsSaved)
        {
            _config = AppConfig.Load();
            cmbConnection.ItemsSource = _config.Connections;
            if (_config.Connections.Count > 0)
                cmbConnection.SelectedIndex = 0;
            CmbConnection_SelectionChanged(null!, null!);
        }
    }

    /// <summary>
    /// 打开常用 SQL 窗口
    /// </summary>
    private void BtnSavedSql_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SavedSqlWindow(_savedSqlManager) { Owner = this };
        dialog.ShowDialog();

        if (dialog.SelectedSql != null)
        {
            txtOutput.Text = dialog.SelectedSql;
        }
    }

    /// <summary>
    /// 保存当前 SQL 为常用 SQL
    /// </summary>
    private void BtnSaveSql_Click(object sender, RoutedEventArgs e)
    {
        var sql = txtOutput.Text.Trim();
        if (string.IsNullOrEmpty(sql) || sql.StartsWith("--"))
        {
            MessageBox.Show("请先生成或输入 SQL。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveSqlDialog(_savedSqlManager, sql, _currentConnection?.Name ?? "") { Owner = this };
        dialog.ShowDialog();
    }
}
