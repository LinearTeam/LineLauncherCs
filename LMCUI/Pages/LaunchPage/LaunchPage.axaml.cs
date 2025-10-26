using Avalonia.Interactivity;
using LMCUI.Utils;

namespace LMCUI.Pages.LaunchPage;

public partial class LaunchPage : PageBase
{
    public LaunchPage() : base("Pages.LaunchPage.Title","LaunchPage")
    {
        InitializeComponent();
    }

    private void ShowInfoButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowInfo("信息提示", "这是一个信息提示示例，将在5秒后自动消失或手动关闭。");
    }

    private void ShowSuccessButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowSuccess("成功提示", "操作成功完成！这是一个成功提示示例。");
    }

    private void ShowWarningButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowWarning("警告提示", "这是一个警告提示，请注意潜在问题。");
    }

    private void ShowErrorButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowError("错误提示", "发生了一个错误，请检查操作是否正确。");
    }

    private void ShowTeachingTipButton_Click(object? sender, RoutedEventArgs e)
    {
        MessageQueueHelper.ShowTeachingTip("使用提示", "这是一个教学提示，用于引导用户完成特定操作。");
    }
}