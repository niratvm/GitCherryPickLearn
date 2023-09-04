using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AcrConnect.HomePage.Infrastructure.Extensions;
using AcrConnect.HomePage.Service.Extensions;
using AcrConnect.HomePage.Services;
using JetBrains.Annotations;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace AcrConnect.HomePage
{
    internal static class Program
    {
        private const string AppTitle = "acrconnect-homepage-service";
        internal static async Task<int> Main(string[] args)
        {
            // We are setting the args to null because otherwise checkmarx complains about Code injection high severity vulnerability
            // using the HostBuilder.Build() overload instead of the HostBuilder.Build(args) overload breaks migrations due to github issue
            // https://github.com/dotnet/runtime/issues/60600
            args = null;

            SetCurrentDirectory();
            CreateStartupLogger();
            SetConsoleTitle();

            try
            {
                var host = CreateWebHostBuilder(args).Build();
                var logger = host.Services.GetRequiredService<ILogger<ApplicationInitializer>>();
                var scopeFactory = host.Services.GetRequiredService<IServiceScopeFactory>();
                var initializer = host.Services.GetRequiredService<ApplicationInitializer>();

                using (var scope = scopeFactory.CreateScope())
                {
                    try
                    {
                        await initializer.InitializeAsync(scope).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Failed to initialize database. Abort the application...");
                        throw;
                    }
                }

                await host.RunAsync().ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly.");
                return 1;
            }


        }

        [NotNull]
        private static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((context, configuration) =>
                {
                    configuration.AddEnvironmentVariables("ACRCONNECT_HP_");
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseKestrel((context, serverOptions) => //options.ConfigureEndpoints());
                    {
                        serverOptions.Configure(context.Configuration.GetSection("Kestrel"));
                    });
                })
                .UseSerilog((context, configuration) =>
                {
                    InitializeSerilogConfig(configuration, context.Configuration);
                }, preserveStaticLogger: true) // preserveStaticLogger keeps static instance around for startup failures   
                                               // if linux this is NoOp
                .UseWindowsService();
        }

        private static void InitializeSerilogConfig(LoggerConfiguration logConfiguration, IConfiguration appConfiguration)
        {
            const string DefaultLogFileName = "log.txt";

            try
            {
                if (appConfiguration.GetUseJsonSerilogConfiguration())
                {
                    Console.WriteLine("using logging configuration from appsettings.json");
                    logConfiguration.ApplyCustomOverrides(appConfiguration.GetSection("Serilog:MinimumLevel:OverrideList"))
                        .ReadFrom.Configuration(appConfiguration);
                }
                else
                {
                    Console.WriteLine("using code specified logging configuration");
                    logConfiguration.CreateCodeBasedLogConfiguration(appConfiguration.GetLogsPath(), DefaultLogFileName);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void SetConsoleTitle()
        {
            if (!WindowsServiceHelpers.IsWindowsService())
            {
                Console.Title = AppTitle;
            }
        }

        private static void CreateStartupLogger()
        {
            var pathToLogs = GetPathToLogsBasedOnOS();
            var logFileName = "startup.log";

            var logConfig = new LoggerConfiguration().CreateCodeBasedLogConfiguration(pathToLogs, logFileName);
            Log.Logger = logConfig.CreateLogger();
        }

        private static string GetPathToLogsBasedOnOS()
        {
            string pathToLogs = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string[] pathSegments = new string[] { Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "acr-connect-apps", "ConnectExpress", ".volumes", "logs", AppTitle };
                pathToLogs = Path.Combine(pathSegments);
            }
            else
            {                
                string[] pathSegments = new string[] { "/volumes/logs", AppTitle };
                pathToLogs = Path.Combine(pathSegments);
            }

            return pathToLogs;
        }

        private static void SetCurrentDirectory()
        {
            // if it's a windows service we need to set the current directory so it doesn't try to make it c:\windows\system32
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && WindowsServiceHelpers.IsWindowsService())
            {
                var basetDir = string.Empty;
                try
                {
                    basetDir = AppContext.BaseDirectory;
                    Directory.SetCurrentDirectory(basetDir);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while performing setting base directory");
                    throw;
                }
            }
        }
    }
}
