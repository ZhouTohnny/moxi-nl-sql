using System.Data;
using System.Windows;

namespace NL2SQL.GUI;

public partial class ConfirmWindow : Window
{
    public bool IsConfirmed { get; private set; }

    public ConfirmWindow(string title, string description, string sql, DataTable? previewData = null)
    {
        InitializeComponent();

        txtTitle.Text = title;
        txtDescription.Text = description;
        txtSql.Text = sql;

        if (previewData != null && previewData.Rows.Count > 0)
        {
            dgPreview.ItemsSource = previewData.DefaultView;
        }
        else
        {
            dgPreview.Visibility = Visibility.Collapsed;
        }
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }
}
