using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace LMCUI.Controls;

using System;
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
        await Startup.Initialize((pim => {
            pb.Value = (int)Math.Round(((double)pim.progress / pim.total * 100)
                , MidpointRounding.AwayFromZero);
            pl.Content = $"{pim.progress} / {pim.total} {pim.msg}";
        }));
        await Task.Delay(800, ct);
        pb.Value = 100;
    }
}