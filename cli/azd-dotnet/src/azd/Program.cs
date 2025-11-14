// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Azure.Dev.Cli.Commands;
using Azure.Dev.Cli.Services;
using Azure.Dev.Cli.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Azure.Dev.Cli;

/// <summary>
/// Main entry point for the Azure Developer CLI
/// </summary>
internal class Program
{
    private const string AppName = "azd";
    private const string AppDescription = "The Azure Developer CLI is an open-source tool that helps onboard and manage your project on Azure";

    static async Task<int> Main(string[] args)
    {
        // Enable console colors on Windows
        if (OperatingSystem.IsWindows())
        {
            EnableWindowsColors();
        }

        // Check for debug mode
        bool debugEnabled = args.Contains("--debug");
        
        // Setup logging
        using var logFileCleanup = SetupLogging(debugEnabled);

        // Initialize telemetry
        var telemetrySystem = TelemetrySystem.Initialize();

        try
        {
            // Build host with dependency injection
            var host = CreateHost(args, debugEnabled);

            // Create root command
            var rootCommand = CreateRootCommand(host.Services);

            // Build command line parser
            var parser = new CommandLineBuilder(rootCommand)
                .UseDefaults()
                .UseExceptionHandler((ex, context) =>
                {
                    var logger = host.Services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Unhandled exception occurred");
                    context.ExitCode = 1;
                })
                .Build();

            // Execute command
            var exitCode = await parser.InvokeAsync(args);

            // Check for updates
            await CheckForUpdatesAsync();

            return exitCode;
        }
        finally
        {
            // Shutdown telemetry
            if (telemetrySystem != null)
            {
                await telemetrySystem.ShutdownAsync();
            }
        }
    }

    private static IHost CreateHost(string[] args, bool debugEnabled)
    {
        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Register core services
                services.AddSingleton<IVersionService, VersionService>();
                services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
                services.AddSingleton<ITelemetryService, TelemetryService>();
                
                // Register command handlers
                services.AddTransient<InitCommandHandler>();
                services.AddTransient<UpCommandHandler>();
                services.AddTransient<DownCommandHandler>();
                services.AddTransient<DeployCommandHandler>();
                services.AddTransient<ProvisionCommandHandler>();
                
                // Add Application Insights telemetry
                services.AddApplicationInsightsTelemetryWorkerService(options =>
                {
                    options.EnableAdaptiveSampling = false;
                });
                
                // Add OpenTelemetry
                services.AddOpenTelemetry()
                    .WithTracing(builder => builder
                        .AddSource("Azure.Dev.Cli")
                        .AddHttpClientInstrumentation()
                        .AddOtlpExporter());
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.ClearProviders();
                if (debugEnabled)
                {
                    logging.AddConsole();
                    logging.SetMinimumLevel(LogLevel.Debug);
                }
                else
                {
                    logging.SetMinimumLevel(LogLevel.Warning);
                }
            });

        return builder.Build();
    }

    private static RootCommand CreateRootCommand(IServiceProvider services)
    {
        var rootCommand = new RootCommand(AppDescription)
        {
            Name = AppName
        };

        // Add global options
        var cwdOption = new Option<string?>(
            aliases: new[] { "--cwd", "-C" },
            description: "Sets the current working directory");
        rootCommand.AddGlobalOption(cwdOption);

        var debugOption = new Option<bool>(
            aliases: new[] { "--debug" },
            description: "Enable debug logging");
        rootCommand.AddGlobalOption(debugOption);

        var outputOption = new Option<string>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => "text",
            description: "Output format (text, json, yaml)");
        rootCommand.AddGlobalOption(outputOption);

        // Register commands
        rootCommand.AddCommand(CreateInitCommand(services));
        rootCommand.AddCommand(CreateUpCommand(services));
        rootCommand.AddCommand(CreateDownCommand(services));
        rootCommand.AddCommand(CreateDeployCommand(services));
        rootCommand.AddCommand(CreateProvisionCommand(services));
        rootCommand.AddCommand(CreateAuthCommand(services));
        rootCommand.AddCommand(CreateConfigCommand(services));
        rootCommand.AddCommand(CreateEnvCommand(services));
        rootCommand.AddCommand(CreateInfraCommand(services));
        rootCommand.AddCommand(CreateMonitorCommand(services));
        rootCommand.AddCommand(CreatePipelineCommand(services));
        rootCommand.AddCommand(CreateVersionCommand(services));

        return rootCommand;
    }

    private static Command CreateInitCommand(IServiceProvider services)
    {
        var command = new Command("init", "Initialize a new application")
        {
            new Option<string?>(new[] { "--template", "-t" }, "Template name or URL"),
            new Option<string?>(new[] { "--location", "-l" }, "Azure location for resources"),
            new Option<string?>(new[] { "--subscription", "-s" }, "Azure subscription ID"),
        };

        command.SetHandler(async (context) =>
        {
            var handler = services.GetRequiredService<InitCommandHandler>();
            await handler.HandleAsync(context);
        });

        return command;
    }

    private static Command CreateUpCommand(IServiceProvider services)
    {
        var command = new Command("up", "Provision and deploy the application")
        {
            new Option<string?>(new[] { "--environment", "-e" }, "Environment name"),
        };

        command.SetHandler(async (context) =>
        {
            var handler = services.GetRequiredService<UpCommandHandler>();
            await handler.HandleAsync(context);
        });

        return command;
    }

    private static Command CreateDownCommand(IServiceProvider services)
    {
        var command = new Command("down", "Delete all Azure resources")
        {
            new Option<bool>(new[] { "--force", "-f" }, "Don't prompt for confirmation"),
            new Option<bool>(new[] { "--purge" }, "Permanently delete resources"),
        };

        command.SetHandler(async (context) =>
        {
            var handler = services.GetRequiredService<DownCommandHandler>();
            await handler.HandleAsync(context);
        });

        return command;
    }

    private static Command CreateDeployCommand(IServiceProvider services)
    {
        var command = new Command("deploy", "Deploy application code");
        
        command.SetHandler(async (context) =>
        {
            var handler = services.GetRequiredService<DeployCommandHandler>();
            await handler.HandleAsync(context);
        });

        return command;
    }

    private static Command CreateProvisionCommand(IServiceProvider services)
    {
        var command = new Command("provision", "Provision Azure resources")
        {
            new Option<bool>(new[] { "--preview" }, "Preview changes before provisioning"),
        };

        command.SetHandler(async (context) =>
        {
            var handler = services.GetRequiredService<ProvisionCommandHandler>();
            await handler.HandleAsync(context);
        });

        return command;
    }

    private static Command CreateAuthCommand(IServiceProvider services)
    {
        var authCommand = new Command("auth", "Authenticate with Azure");
        
        var loginCommand = new Command("login", "Log in to Azure");
        var logoutCommand = new Command("logout", "Log out from Azure");
        var tokenCommand = new Command("token", "Get access token");

        authCommand.AddCommand(loginCommand);
        authCommand.AddCommand(logoutCommand);
        authCommand.AddCommand(tokenCommand);

        return authCommand;
    }

    private static Command CreateConfigCommand(IServiceProvider services)
    {
        var configCommand = new Command("config", "Manage configuration");
        
        var getCommand = new Command("get", "Get configuration value");
        var setCommand = new Command("set", "Set configuration value");
        var unsetCommand = new Command("unset", "Unset configuration value");
        var listCommand = new Command("list", "List all configuration");

        configCommand.AddCommand(getCommand);
        configCommand.AddCommand(setCommand);
        configCommand.AddCommand(unsetCommand);
        configCommand.AddCommand(listCommand);

        return configCommand;
    }

    private static Command CreateEnvCommand(IServiceProvider services)
    {
        var envCommand = new Command("env", "Manage environments");
        
        var newCommand = new Command("new", "Create new environment");
        var selectCommand = new Command("select", "Select active environment");
        var listCommand = new Command("list", "List environments");
        var getValuesCommand = new Command("get-values", "Get environment values");

        envCommand.AddCommand(newCommand);
        envCommand.AddCommand(selectCommand);
        envCommand.AddCommand(listCommand);
        envCommand.AddCommand(getValuesCommand);

        return envCommand;
    }

    private static Command CreateInfraCommand(IServiceProvider services)
    {
        var infraCommand = new Command("infra", "Manage infrastructure");
        
        var createCommand = new Command("create", "Create infrastructure template");
        var deleteCommand = new Command("delete", "Delete infrastructure");
        var generateCommand = new Command("generate", "Generate infrastructure from code");

        infraCommand.AddCommand(createCommand);
        infraCommand.AddCommand(deleteCommand);
        infraCommand.AddCommand(generateCommand);

        return infraCommand;
    }

    private static Command CreateMonitorCommand(IServiceProvider services)
    {
        var monitorCommand = new Command("monitor", "Monitor application");
        
        var logsCommand = new Command("logs", "View application logs");
        var metricsCommand = new Command("metrics", "View application metrics");

        monitorCommand.AddCommand(logsCommand);
        monitorCommand.AddCommand(metricsCommand);

        return monitorCommand;
    }

    private static Command CreatePipelineCommand(IServiceProvider services)
    {
        var pipelineCommand = new Command("pipeline", "Manage CI/CD pipelines");
        
        var configCommand = new Command("config", "Configure CI/CD pipeline");

        pipelineCommand.AddCommand(configCommand);

        return pipelineCommand;
    }

    private static Command CreateVersionCommand(IServiceProvider services)
    {
        var command = new Command("version", "Show version information");
        
        command.SetHandler(() =>
        {
            var versionService = services.GetRequiredService<IVersionService>();
            Console.WriteLine($"azd version {versionService.GetVersion()}");
        });

        return command;
    }

    private static void EnableWindowsColors()
    {
        // Enable ANSI color codes on Windows
        var handle = NativeMethods.GetStdHandle(-11); // STD_OUTPUT_HANDLE
        if (NativeMethods.GetConsoleMode(handle, out uint mode))
        {
            mode |= 0x0004; // ENABLE_VIRTUAL_TERMINAL_PROCESSING
            NativeMethods.SetConsoleMode(handle, mode);
        }
    }

    private static IDisposable SetupLogging(bool debugEnabled)
    {
        var logFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".azd",
            $"azd-{DateTime.Now:yyyy-MM-dd}.log");

        Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);

        var fileStream = new StreamWriter(logFile, append: true, System.Text.Encoding.UTF8);
        fileStream.AutoFlush = true;

        return fileStream;
    }

    private static async Task CheckForUpdatesAsync()
    {
        // Check for CLI updates (similar to Go version)
        try
        {
            if (Environment.GetEnvironmentVariable("AZD_SKIP_UPDATE_CHECK") == "true")
            {
                return;
            }

            var updateCheckService = new UpdateCheckService();
            var latestVersion = await updateCheckService.GetLatestVersionAsync();
            var currentVersion = VersionService.GetCurrentVersion();

            if (latestVersion > currentVersion)
            {
                Console.WriteLine();
                Console.WriteLine($"WARNING: your version of azd is out of date, you have {currentVersion} and the latest version is {latestVersion}");
                Console.WriteLine();
                Console.WriteLine("To update to the latest version, visit https://aka.ms/azd/upgrade");
            }
        }
        catch
        {
            // Silently ignore update check failures
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern IntPtr GetStdHandle(int nStdHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
