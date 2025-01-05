using Marauder.Mini.Commands;
using Marauder.Mini.Commands.Build;
using Marauder.Mini.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace Marauder.Mini;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        var app = host.Services.GetRequiredService<Application>();
        return await app.RunAsync(args);
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
        => Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) => 
            {
                var env = context.HostingEnvironment.EnvironmentName;
                
                config.Sources.Clear();
                config.AddEnvironmentVariables();
                config.AddJsonFile("appsettings.json");
                config.AddJsonFile($"appsettings.{env}.json", optional: true);
                config.AddCommandLine(args);
            })
            .ConfigureLogging((context, builder) =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddConsole(console => 
                {
                    console.FormatterName = "AppFormatter";
                });
                builder.AddConsoleFormatter<AppFormatter, ConsoleFormatterOptions>(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";
                });
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<Application>();
                services.AddScoped<BuildCommand>();
                services.AddScoped<BuildEnumsCommand>();
                services.AddScoped<ChantCommand>();
                services.AddScoped<RunCommand>();
                services.AddScoped<TestCommand>();
                services.AddScoped<CancellationTokenSource>();

                services.AddSingleton<IGameClientService, GameClientService>();
            });
    
    public class AppFormatter(IOptions<ConsoleFormatterOptions> options) 
    : ConsoleFormatter("AppFormatter")
    {
        private readonly ConsoleFormatterOptions _options = options.Value;

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            var color = logEntry.LogLevel switch
            {
                LogLevel.Trace => ConsoleColor.Gray,
                LogLevel.Debug => ConsoleColor.Blue,
                LogLevel.Information => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.Magenta,
                _ => ConsoleColor.White,
            };

            var colorCode = AnsiCodes.GetForegroundColorCode(color);
            var timestampCode = AnsiCodes.GetForegroundColorCode(ConsoleColor.Magenta);
            var resetCode = AnsiCodes.Reset;
            var timestamp = DateTime.Now.ToString(_options.TimestampFormat);
            var message = logEntry.Formatter(logEntry.State, logEntry.Exception);

            textWriter.WriteLine($"{timestampCode}{timestamp}{resetCode}{colorCode}{message}{resetCode}");
        }

        public static class AnsiCodes
        {
            public static string GetForegroundColorCode(ConsoleColor color) =>
                color switch
                {
                    ConsoleColor.Black => "\u001b[30m",
                    ConsoleColor.Red => "\u001b[31m",
                    ConsoleColor.Green => "\u001b[32m",
                    ConsoleColor.Yellow => "\u001b[33m",
                    ConsoleColor.Blue => "\u001b[34m",
                    ConsoleColor.Magenta => "\u001b[35m",
                    ConsoleColor.Cyan => "\u001b[36m",
                    ConsoleColor.White => "\u001b[37m",
                    _ => "\u001b[39m", // Default foreground color
                };

            public static string GetBackgroundColorCode(ConsoleColor color) =>
                color switch
                {
                    ConsoleColor.Black => "\u001b[40m",
                    ConsoleColor.Red => "\u001b[41m",
                    ConsoleColor.Green => "\u001b[42m",
                    ConsoleColor.Yellow => "\u001b[43m",
                    ConsoleColor.Blue => "\u001b[44m",
                    ConsoleColor.Magenta => "\u001b[45m",
                    ConsoleColor.Cyan => "\u001b[46m",
                    ConsoleColor.White => "\u001b[47m",
                    _ => "\u001b[49m", // Default background color
                };

            public static string Reset => "\u001b[0m";
        }
    }
}

// using System;
// using System.Threading.Tasks;

// class Program
// {
//     static async Task Main(string[] args)
//     {
//         int i = 0;
//         while (true)
//         {
//             await PerformAsyncOperation(i);
//             i++;
//             // Additional logic or a delay can be added here
//         }
//     }

//     static async Task PerformAsyncOperation(int i)
//     {
//         // Simulate an asynchronous operation
//         await Task.Delay(1000);
//         Console.WriteLine($"Operation {i} completed at {DateTime.Now}");
//     }
// }
