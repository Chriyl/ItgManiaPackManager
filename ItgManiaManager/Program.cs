using ItgManiaManager;
using ItgManiaManager.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<IPackService, PackService>();

                // registra la form
                services.AddTransient<MainWindow>();
            })
            .Build();

        var mainForm = host.Services.GetRequiredService<MainWindow>();
        Application.Run(mainForm);
    }
}