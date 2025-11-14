// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Azure.Dev.Cli.Core.Models;
using Azure.Dev.Cli.Core.Storage;

namespace Azure.Dev.Cli.Core.Services;

/// <summary>
/// Service for managing azd environments
/// </summary>
public class EnvironmentService : IEnvironmentService
{
    private readonly EnvironmentStore _store;
    private readonly IConfigService _configService;
    
    // Same restrictions as Azure deployment names
    private static readonly Regex EnvironmentNameRegex = new(@"^[a-zA-Z0-9-\(\)_\.]{1,64}$", RegexOptions.Compiled);
    private const int EnvironmentNameMaxLength = 64;

    // Well-known environment variable names
    public const string EnvNameEnvVarName = "AZURE_ENV_NAME";
    public const string LocationEnvVarName = "AZURE_LOCATION";
    public const string SubscriptionIdEnvVarName = "AZURE_SUBSCRIPTION_ID";
    public const string TenantIdEnvVarName = "AZURE_TENANT_ID";
    public const string PrincipalIdEnvVarName = "AZURE_PRINCIPAL_ID";
    public const string PrincipalTypeEnvVarName = "AZURE_PRINCIPAL_TYPE";
    public const string ContainerRegistryEndpointEnvVarName = "AZURE_CONTAINER_REGISTRY_ENDPOINT";
    public const string ResourceGroupEnvVarName = "AZURE_RESOURCE_GROUP";

    public EnvironmentService(IConfigService configService)
    {
        _configService = configService;
        _store = new EnvironmentStore(configService);
    }

    /// <inheritdoc />
    public async Task<Models.Environment> CreateAsync(
        string name, 
        string? subscription = null, 
        string? location = null, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment name cannot be empty", nameof(name));
        }

        if (!IsValidEnvironmentName(name))
        {
            throw new ArgumentException(
                $"Environment name '{name}' is invalid. It should contain only alphanumeric characters, hyphens, parentheses, underscores, and periods, and be 1-64 characters long.",
                nameof(name));
        }

        if (_store.Exists(name))
        {
            throw new InvalidOperationException($"Environment '{name}' already exists");
        }

        var environment = new Models.Environment
        {
            Name = name,
            SubscriptionId = subscription,
            Location = location,
            Values = new Dictionary<string, string>
            {
                [EnvNameEnvVarName] = name
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await SaveAsync(environment, cancellationToken);

        return environment;
    }

    /// <inheritdoc />
    public async Task<Models.Environment?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (!_store.Exists(name))
        {
            return null;
        }

        try
        {
            return await _store.LoadAsync(name, cancellationToken);
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<List<EnvironmentDescription>> ListAsync(CancellationToken cancellationToken = default)
    {
        var environmentNames = await _store.ListEnvironmentNamesAsync(cancellationToken);
        var defaultEnvName = await _store.GetDefaultEnvironmentNameAsync(cancellationToken);

        var descriptions = new List<EnvironmentDescription>();

        foreach (var name in environmentNames)
        {
            descriptions.Add(new EnvironmentDescription
            {
                Name = name,
                DotEnvPath = _store.GetEnvPath(name),
                ConfigPath = _store.GetConfigPath(name),
                HasLocal = true,
                HasRemote = false,
                IsDefault = name == defaultEnvName
            });
        }

        return descriptions;
    }

    /// <inheritdoc />
    public async Task SaveAsync(Models.Environment environment, CancellationToken cancellationToken = default)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (string.IsNullOrWhiteSpace(environment.Name))
        {
            throw new ArgumentException("Environment name cannot be empty", nameof(environment));
        }

        await _store.SaveAsync(environment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment name cannot be empty", nameof(name));
        }

        if (!_store.Exists(name))
        {
            throw new DirectoryNotFoundException($"Environment '{name}' not found");
        }

        await _store.DeleteAsync(name, cancellationToken);

        // Clear default if this was the default environment
        var defaultEnv = await _store.GetDefaultEnvironmentNameAsync(cancellationToken);
        if (defaultEnv == name)
        {
            await _store.SetDefaultEnvironmentNameAsync(null, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetDefaultEnvironmentNameAsync(CancellationToken cancellationToken = default)
    {
        return await _store.GetDefaultEnvironmentNameAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetDefaultEnvironmentAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment name cannot be empty", nameof(name));
        }

        if (!_store.Exists(name))
        {
            throw new DirectoryNotFoundException($"Environment '{name}' not found");
        }

        await _store.SetDefaultEnvironmentNameAsync(name, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetValuesAsync(string environmentName, CancellationToken cancellationToken = default)
    {
        var environment = await GetAsync(environmentName, cancellationToken);
        
        if (environment == null)
        {
            throw new DirectoryNotFoundException($"Environment '{environmentName}' not found");
        }

        return new Dictionary<string, string>(environment.Values);
    }

    /// <inheritdoc />
    public async Task ReloadAsync(Models.Environment environment, CancellationToken cancellationToken = default)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        var reloaded = await _store.LoadAsync(environment.Name, cancellationToken);
        
        // Update the existing environment object
        environment.Values = reloaded.Values;
        environment.SubscriptionId = reloaded.SubscriptionId;
        environment.TenantId = reloaded.TenantId;
        environment.Location = reloaded.Location;
        environment.UpdatedAt = reloaded.UpdatedAt;
    }

    /// <inheritdoc />
    public bool IsValidEnvironmentName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Length > EnvironmentNameMaxLength)
        {
            return false;
        }

        return EnvironmentNameRegex.IsMatch(name);
    }

    /// <inheritdoc />
    public string GetEnvPath(Models.Environment environment)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        return _store.GetEnvPath(environment.Name);
    }

    /// <inheritdoc />
    public string GetConfigPath(Models.Environment environment)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        return _store.GetConfigPath(environment.Name);
    }

    /// <summary>
    /// Cleans a name by replacing invalid characters with hyphens
    /// </summary>
    public static string CleanName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var cleaned = new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if ((c >= 'a' && c <= 'z') ||
                (c >= 'A' && c <= 'Z') ||
                (c >= '0' && c <= '9') ||
                c == '-' ||
                c == '(' ||
                c == ')' ||
                c == '_' ||
                c == '.')
            {
                cleaned[i] = c;
            }
            else
            {
                cleaned[i] = '-';
            }
        }

        return new string(cleaned);
    }

    /// <summary>
    /// Gets a service-specific property from the environment
    /// </summary>
    public static string? GetServiceProperty(Models.Environment environment, string serviceName, string propertyName)
    {
        var key = $"SERVICE_{ToEnvironmentKey(serviceName)}_{propertyName}";
        return environment.Values.GetValueOrDefault(key);
    }

    /// <summary>
    /// Sets a service-specific property in the environment
    /// </summary>
    public static void SetServiceProperty(Models.Environment environment, string serviceName, string propertyName, string value)
    {
        var key = $"SERVICE_{ToEnvironmentKey(serviceName)}_{propertyName}";
        environment.Values[key] = value;
    }

    /// <summary>
    /// Converts a name to an environment variable key format
    /// </summary>
    private static string ToEnvironmentKey(string name)
    {
        return name.Replace("-", "_").ToUpperInvariant();
    }
}
