// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Azure.Dev.Cli.Core.Models;
using Azure.Dev.Cli.Core.Services;

namespace Azure.Dev.Cli.Core.Storage;

/// <summary>
/// Handles persistent storage of environment data
/// </summary>
public class EnvironmentStore
{
    private readonly IConfigService _configService;
    private const string DotEnvFileName = ".env";
    private const string ConfigFileName = "config.json";
    private const string EnvironmentsDirectory = ".azure";
    private const string ProjectStateFileName = "project.json";

    public EnvironmentStore(IConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>
    /// Gets the root directory for all environments in the current project
    /// </summary>
    public string GetEnvironmentsDirectory()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        return Path.Combine(currentDirectory, EnvironmentsDirectory);
    }

    /// <summary>
    /// Gets the root directory for a specific environment
    /// </summary>
    public string GetEnvironmentRoot(string environmentName)
    {
        return Path.Combine(GetEnvironmentsDirectory(), environmentName);
    }

    /// <summary>
    /// Gets the path to the .env file for an environment
    /// </summary>
    public string GetEnvPath(string environmentName)
    {
        return Path.Combine(GetEnvironmentRoot(environmentName), DotEnvFileName);
    }

    /// <summary>
    /// Gets the path to the config.json file for an environment
    /// </summary>
    public string GetConfigPath(string environmentName)
    {
        return Path.Combine(GetEnvironmentRoot(environmentName), ConfigFileName);
    }

    /// <summary>
    /// Gets the path to the project state file
    /// </summary>
    public string GetProjectStatePath()
    {
        return Path.Combine(GetEnvironmentsDirectory(), ProjectStateFileName);
    }

    /// <summary>
    /// Checks if an environment exists
    /// </summary>
    public bool Exists(string environmentName)
    {
        return Directory.Exists(GetEnvironmentRoot(environmentName));
    }

    /// <summary>
    /// Lists all environments
    /// </summary>
    public async Task<List<string>> ListEnvironmentNamesAsync(CancellationToken cancellationToken = default)
    {
        var environmentsDir = GetEnvironmentsDirectory();
        
        if (!Directory.Exists(environmentsDir))
        {
            return new List<string>();
        }

        var directories = Directory.GetDirectories(environmentsDir);
        var environmentNames = directories
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();

        return await Task.FromResult(environmentNames);
    }

    /// <summary>
    /// Loads an environment from disk
    /// </summary>
    public async Task<Models.Environment> LoadAsync(string environmentName, CancellationToken cancellationToken = default)
    {
        if (!Exists(environmentName))
        {
            throw new DirectoryNotFoundException($"Environment '{environmentName}' not found");
        }

        var environment = new Models.Environment
        {
            Name = environmentName,
            Values = new Dictionary<string, string>()
        };

        // Load .env file
        var envPath = GetEnvPath(environmentName);
        if (File.Exists(envPath))
        {
            var envLines = await File.ReadAllLinesAsync(envPath, cancellationToken);
            environment.Values = ParseDotEnv(envLines);
            
            // Extract well-known values
            environment.SubscriptionId = environment.Values.GetValueOrDefault("AZURE_SUBSCRIPTION_ID");
            environment.TenantId = environment.Values.GetValueOrDefault("AZURE_TENANT_ID");
            environment.Location = environment.Values.GetValueOrDefault("AZURE_LOCATION");
        }

        // Load config.json if it exists
        var configPath = GetConfigPath(environmentName);
        if (File.Exists(configPath))
        {
            var configJson = await File.ReadAllTextAsync(configPath, cancellationToken);
            if (!string.IsNullOrWhiteSpace(configJson))
            {
                // Config is stored but not yet fully integrated - future enhancement
            }
        }

        return environment;
    }

    /// <summary>
    /// Saves an environment to disk
    /// </summary>
    public async Task SaveAsync(Models.Environment environment, CancellationToken cancellationToken = default)
    {
        var environmentRoot = GetEnvironmentRoot(environment.Name);
        
        // Ensure directory exists
        Directory.CreateDirectory(environmentRoot);

        // Ensure AZURE_ENV_NAME is set
        if (!environment.Values.ContainsKey("AZURE_ENV_NAME"))
        {
            environment.Values["AZURE_ENV_NAME"] = environment.Name;
        }

        // Update well-known values in the dictionary
        if (!string.IsNullOrEmpty(environment.SubscriptionId))
        {
            environment.Values["AZURE_SUBSCRIPTION_ID"] = environment.SubscriptionId;
        }
        if (!string.IsNullOrEmpty(environment.TenantId))
        {
            environment.Values["AZURE_TENANT_ID"] = environment.TenantId;
        }
        if (!string.IsNullOrEmpty(environment.Location))
        {
            environment.Values["AZURE_LOCATION"] = environment.Location;
        }

        // Save .env file
        var envPath = GetEnvPath(environment.Name);
        var envContent = SerializeDotEnv(environment.Values);
        await File.WriteAllTextAsync(envPath, envContent, cancellationToken);

        // Save config.json (minimal for now)
        var configPath = GetConfigPath(environment.Name);
        var config = new Dictionary<string, object>();
        var configJson = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(configPath, configJson, cancellationToken);

        environment.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deletes an environment from disk
    /// </summary>
    public Task DeleteAsync(string environmentName, CancellationToken cancellationToken = default)
    {
        var environmentRoot = GetEnvironmentRoot(environmentName);
        
        if (Directory.Exists(environmentRoot))
        {
            Directory.Delete(environmentRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the default environment name from project state
    /// </summary>
    public async Task<string?> GetDefaultEnvironmentNameAsync(CancellationToken cancellationToken = default)
    {
        var projectStatePath = GetProjectStatePath();
        
        if (!File.Exists(projectStatePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(projectStatePath, cancellationToken);
            var projectState = JsonSerializer.Deserialize<ProjectState>(json);
            return projectState?.DefaultEnvironment;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Sets the default environment name in project state
    /// </summary>
    public async Task SetDefaultEnvironmentNameAsync(string? environmentName, CancellationToken cancellationToken = default)
    {
        var environmentsDir = GetEnvironmentsDirectory();
        Directory.CreateDirectory(environmentsDir);

        var projectStatePath = GetProjectStatePath();
        var projectState = new ProjectState
        {
            DefaultEnvironment = environmentName
        };

        var json = JsonSerializer.Serialize(projectState, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        await File.WriteAllTextAsync(projectStatePath, json, cancellationToken);
    }

    /// <summary>
    /// Parses a .env file content into a dictionary
    /// </summary>
    private Dictionary<string, string> ParseDotEnv(string[] lines)
    {
        var result = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex == -1)
            {
                continue;
            }

            var key = trimmed.Substring(0, separatorIndex).Trim();
            var value = trimmed.Substring(separatorIndex + 1).Trim();

            // Remove quotes if present
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            {
                value = value.Substring(1, value.Length - 2);
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Serializes a dictionary to .env file format
    /// </summary>
    private string SerializeDotEnv(Dictionary<string, string> values)
    {
        var lines = new List<string>();

        foreach (var kvp in values.OrderBy(x => x.Key))
        {
            var value = kvp.Value;
            
            // Quote values that contain spaces or special characters, or start with digits
            var needsQuotes = value.Contains(' ') || 
                             value.Contains('\t') || 
                             value.Contains('"') ||
                             (value.Length > 0 && char.IsDigit(value[0]));

            if (needsQuotes)
            {
                // Escape quotes in the value
                value = value.Replace("\"", "\\\"");
                lines.Add($"{kvp.Key}=\"{value}\"");
            }
            else
            {
                lines.Add($"{kvp.Key}={value}");
            }
        }

        return string.Join(System.Environment.NewLine, lines);
    }

    private class ProjectState
    {
        public string? DefaultEnvironment { get; set; }
    }
}
