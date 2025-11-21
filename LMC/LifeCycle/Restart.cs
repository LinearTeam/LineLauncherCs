using System.Diagnostics;

namespace LMC.LifeCycle;

public class Restart
{
    public static void Run()
    {
        var path = Environment.ProcessPath;
        var arguments = Environment.GetCommandLineArgs();
        if (!arguments.Contains("--restart"))
        {
            arguments = arguments.Append("--restart=" + Environment.ProcessId).ToArray();
        }
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            Arguments = string.Join(" ", arguments.Skip(1)),
            UseShellExecute = true
        });
    }
}