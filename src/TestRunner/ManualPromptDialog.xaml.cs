using System.Windows;

namespace AutoPowerMeter.TestRunner;

public enum PromptChoice { Continue, Skip, Abort }

public partial class ManualPromptDialog : Window
{
    public PromptChoice Choice { get; private set; } = PromptChoice.Abort;

    public ManualPromptDialog(string title, string body, string? hint = null)
    {
        InitializeComponent();
        TitleText.Text = title;
        BodyText.Text = body;
        if (!string.IsNullOrEmpty(hint))
            HintText.Text = hint;
        else
            HintText.Visibility = Visibility.Collapsed;
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = PromptChoice.Continue;
        DialogResult = true;
        Close();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = PromptChoice.Skip;
        DialogResult = true;
        Close();
    }

    private void AbortButton_Click(object sender, RoutedEventArgs e)
    {
        Choice = PromptChoice.Abort;
        DialogResult = false;
        Close();
    }
}
