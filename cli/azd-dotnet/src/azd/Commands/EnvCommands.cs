// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Invocation;
using Azure.Dev.Cli.Core.Services;

namespace Azure.Dev.Cli.Commands;

/// <summary>
/// Handles azd env commands
/// </summary>
public class EnvCommands
{
    private readonly IEnvironmentService _environmentService;

    public EnvCommands(IEnvironmentService environmentService)
    {
        _environmentService = environmentService;
    }

    /// <summary>
    /// Creates the env command and subcommands
    /// </summary>
    public static Command CreateCommand(IEnvironmentService environmentService)
    {
        var envCommand = new Command("env", "Manage environments");
        var commands = new EnvCommands(environmentService);

        // azd env new
        var newCommand = new Command("new", "Create a new environment")
        {
            new Argument<string>("name", "The name of the environment to create"),
            new Option<string?>(["--subscription", "-s"], "Azure subscription ID"),
            new Option<string?>(["--location", "-l"], "Azure location")
        };
        newCommand.SetHandler(commands.HandleNewAsync);
        envCommand.AddCommand(newCommand);

        // azd env select
        var selectCommand = new Command("select", "Set the default environment")
        {
            new Argument<string>("name", "The name of the environment to select")
        };
        selectCommand.SetHandler(commands.HandleSelectAsync);
        envCommand.AddCommand(selectCommand);

        // azd env list
        var listCommand = new Command("list", "List all environments");
        listCommand.SetHandler(commands.HandleListAsync);
        envCommand.AddCommand(listCommand);

        // azd env get-values
        var getValuesCommand = new Command("get-values", "Get all environment values")
        {
            new Argument<string?>("name", () => null, "The name of the environment (uses default if not specified)")
        };
        getValuesCommand.SetHandler(commands.HandleGetValuesAsync);
        envCommand.AddCommand(getValuesCommand);

        // azd env refresh
        var refreshCommand = new Command("refresh", "Refresh environment settings")
        {
            new Argument<string?>("name", () => null, "The name of the environment (uses default if not specified)")
        };
        refreshCommand.SetHandler(commands.HandleRefreshAsync);
        envCommand.AddCommand(refreshCommand);

        return envCommand;
    }

    /// <summary>
    /// Handles azd env new command
    /// </summary>
    private async Task HandleNewAsync(InvocationContext context)
    {
        var parseResult = context.ParseResult;
        var name = parseResult.CommandResult.Tokens.FirstOrDefault()?.Value ?? string.Empty;
        
        string? subscription = null;
        string? location = null;
        
        foreach (var option in parseResult.CommandResult.Children.OfType<System.CommandLine.Parsing.OptionResult>())
        {
            if (option.Option.Name == "subscription")
                subscription = option.GetValueOrDefault<string?>();
            if (option.Option.Name == "location")
                location = option.GetValueOrDefault<string?>();
        }
        
        var cancellationToken = context.GetCancellationToken();

        try
        {
            var environment = await _environmentService.CreateAsync(name, subscription, location, cancellationToken);
            
            Console.WriteLine($"Created new environment '{environment.Name}'");
            
            // Check if this is the first environment
            var environments = await _environmentService.ListAsync(cancellationToken);
            if (environments.Count == 1)
            {
                await _environmentService.SetDefaultEnvironmentAsync(name, cancellationToken);
                Console.WriteLine($"Set '{name}' as the default environment");
            }
            else
            {
                Console.WriteLine($"\nTo set this as your default environment, run:");
                Console.WriteLine($"  azd env select {name}");
            }

            Console.WriteLine($"\nEnvironment location: {_environmentService.GetEnvPath(environment)}");
            
            context.ExitCode = 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            context.ExitCode = 1;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            context.ExitCode = 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error creating environment: {ex.Message}");
            context.ExitCode = 1;
        }
    }

    /// <summary>
    /// Handles azd env select command
    /// </summary>
    private async Task HandleSelectAsync(InvocationContext context)
    {
        var parseResult = context.ParseResult;
        var name = parseResult.CommandResult.Tokens.FirstOrDefault()?.Value ?? string.Empty;
        var cancellationToken = context.GetCancellationToken();

        try
        {
            var environment = await _environmentService.GetAsync(name, cancellationToken);
            
            if (environment == null)
            {
                Console.Error.WriteLine($"Error: Environment '{name}' not found");
                Console.WriteLine("\nAvailable environments:");
                await PrintEnvironmentListAsync(cancellationToken);
                context.ExitCode = 1;
                return;
            }

            await _environmentService.SetDefaultEnvironmentAsync(name, cancellationToken);
            Console.WriteLine($"Set '{name}' as the default environment");
            context.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error selecting environment: {ex.Message}");
            context.ExitCode = 1;
        }
    }

    /// <summary>
    /// Handles azd env list command
    /// </summary>
    private async Task HandleListAsync(InvocationContext context)
    {
        var cancellationToken = context.GetCancellationToken();

        try
        {
            var environments = await _environmentService.ListAsync(cancellationToken);
            
            if (environments.Count == 0)
            {
                Console.WriteLine("No environments found.");
                Console.WriteLine("\nTo create an environment, run:");
                Console.WriteLine("  azd env new <name>");
                context.ExitCode = 0;
                return;
            }

            Console.WriteLine($"{"NAME",-30} {"DEFAULT",-10} {"PATH"}");
            Console.WriteLine(new string('-', 80));

            foreach (var env in environments)
            {
                var defaultMarker = env.IsDefault ? "*" : "";
                Console.WriteLine($"{env.Name,-30} {defaultMarker,-10} {env.DotEnvPath}");
            }

            context.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error listing environments: {ex.Message}");
            context.ExitCode = 1;
        }
    }

    /// <summary>
    /// Handles azd env get-values command
    /// </summary>
    private async Task HandleGetValuesAsync(InvocationContext context)
    {
        var parseResult = context.ParseResult;
        var name = parseResult.CommandResult.Tokens.FirstOrDefault()?.Value;
        var cancellationToken = context.GetCancellationToken();

        try
        {
            // If no name specified, use default environment
            if (string.IsNullOrWhiteSpace(name))
            {
                name = await _environmentService.GetDefaultEnvironmentNameAsync(cancellationToken);
                
                if (string.IsNullOrWhiteSpace(name))
                {
                    Console.Error.WriteLine("Error: No environment specified and no default environment set");
                    Console.WriteLine("\nTo set a default environment, run:");
                    Console.WriteLine("  azd env select <name>");
                    context.ExitCode = 1;
                    return;
                }
            }

            var values = await _environmentService.GetValuesAsync(name, cancellationToken);
            
            if (values.Count == 0)
            {
                Console.WriteLine($"No values set in environment '{name}'");
                context.ExitCode = 0;
                return;
            }

            Console.WriteLine($"Environment '{name}' values:");
            Console.WriteLine();

            foreach (var kvp in values.OrderBy(x => x.Key))
            {
                Console.WriteLine($"{kvp.Key}={kvp.Value}");
            }

            context.ExitCode = 0;
        }
        catch (DirectoryNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            context.ExitCode = 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error getting environment values: {ex.Message}");
            context.ExitCode = 1;
        }
    }

    /// <summary>
    /// Handles azd env refresh command
    /// </summary>
    private async Task HandleRefreshAsync(InvocationContext context)
    {
        var parseResult = context.ParseResult;
        var name = parseResult.CommandResult.Tokens.FirstOrDefault()?.Value;
        var cancellationToken = context.GetCancellationToken();

        try
        {
            // If no name specified, use default environment
            if (string.IsNullOrWhiteSpace(name))
            {
                name = await _environmentService.GetDefaultEnvironmentNameAsync(cancellationToken);
                
                if (string.IsNullOrWhiteSpace(name))
                {
                    Console.Error.WriteLine("Error: No environment specified and no default environment set");
                    context.ExitCode = 1;
                    return;
                }
            }

            var environment = await _environmentService.GetAsync(name, cancellationToken);
            
            if (environment == null)
            {
                Console.Error.WriteLine($"Error: Environment '{name}' not found");
                context.ExitCode = 1;
                return;
            }

            await _environmentService.ReloadAsync(environment, cancellationToken);
            Console.WriteLine($"Refreshed environment '{name}'");
            context.ExitCode = 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error refreshing environment: {ex.Message}");
            context.ExitCode = 1;
        }
    }

    /// <summary>
    /// Helper method to print environment list
    /// </summary>
    private async Task PrintEnvironmentListAsync(CancellationToken cancellationToken)
    {
        var environments = await _environmentService.ListAsync(cancellationToken);
        foreach (var env in environments)
        {
            var marker = env.IsDefault ? " (default)" : "";
            Console.WriteLine($"  - {env.Name}{marker}");
        }
    }
}
