using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace LMCUI.Controls;

using System;
using I18n;
using LMC.LifeCycle;

public partial class LineSplashScreenContent : UserControl
{
    public LineSplashScreenContent()
    {
        InitializeComponent();
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        pb.Value = 0;
        await Task.Delay(800, ct);
        pb.Value = 100;
    }
}