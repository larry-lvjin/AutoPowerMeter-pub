using System.Windows;
using Wpf.Ui.Controls;

namespace AutoPowerMeter.Views;

public partial class TokenPromptDialog : FluentWindow
{
    public string Token => TokenBox.Password.Trim();

    public TokenPromptDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TokenBox.Focus();
    }

    private void Confirm_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            System.Windows.MessageBox.Show("Token 不能为空。", "配置提示",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;
}
