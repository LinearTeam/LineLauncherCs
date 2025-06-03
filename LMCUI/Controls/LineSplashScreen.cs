using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using FluentAvalonia.UI.Windowing;

namespace LMCUI.Controls;

public class LineSplashScreen : IApplicationSplashScreen
{
    
    public async Task RunTasks(CancellationToken cancellationToken)
    {
        await ((LineSplashScreenContent)SplashScreenContent).InitializeAsync(cancellationToken);
    }

    public string AppName { get; init; }
    public IImage AppIcon { get; init; }
    public object SplashScreenContent { get; } = new LineSplashScreenContent();
    public int MinimumShowTime { get; init; } = 1500;
}