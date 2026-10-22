using System;
using Velopack;

namespace Claudestrap;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        LauncherStartup.Start();
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
