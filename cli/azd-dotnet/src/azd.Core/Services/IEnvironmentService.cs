// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Models;

namespace Azure.Dev.Cli.Core.Services;

/// <summary>
/// Service for managing azd environments
/// </summary>
public interface IEnvironmentService
{
    /// <summary>
    /// Creates a new environment with the specified name
    /// </summary>
    /// <param name="name">The environment name</param>
    /// <param name="subscription">Optional subscription ID</param>
    /// <param name="location">Optional Azure location</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created environment</returns>
    Task<Models.Environment> CreateAsync(
        string name, 
        string? subscription = null, 
        string? location = null, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an environment by name
    /// </summary>
    /// <param name="name">The environment name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The environment if found, null otherwise</returns>
    Task<Models.Environment?> GetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all environments
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of environment descriptions</returns>
    Task<List<EnvironmentDescription>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves an environment
    /// </summary>
    /// <param name="environment">The environment to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveAsync(Models.Environment environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an environment by name
    /// </summary>
    /// <param name="name">The environment name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default environment name
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The default environment name if set, null otherwise</returns>
    Task<string?> GetDefaultEnvironmentNameAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the default environment
    /// </summary>
    /// <param name="name">The environment name to set as default</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetDefaultEnvironmentAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the values from an environment (equivalent to azd env get-values)
    /// </summary>
    /// <param name="environmentName">The environment name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of environment values</returns>
    Task<Dictionary<string, string>> GetValuesAsync(string environmentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reloads an environment from the data store
    /// </summary>
    /// <param name="environment">The environment to reload</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ReloadAsync(Models.Environment environment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an environment name
    /// </summary>
    /// <param name="name">The environment name to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValidEnvironmentName(string name);

    /// <summary>
    /// Gets the path to the .env file for an environment
    /// </summary>
    /// <param name="environment">The environment</param>
    /// <returns>The full path to the .env file</returns>
    string GetEnvPath(Models.Environment environment);

    /// <summary>
    /// Gets the path to the config.json file for an environment
    /// </summary>
    /// <param name="environment">The environment</param>
    /// <returns>The full path to the config.json file</returns>
    string GetConfigPath(Models.Environment environment);
}

/// <summary>
/// Metadata description of an environment
/// </summary>
public class EnvironmentDescription
{
    /// <summary>
    /// The name of the environment
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The path to the local .env file for the environment
    /// </summary>
    public string DotEnvPath { get; set; } = string.Empty;

    /// <summary>
    /// The path to the config.json file
    /// </summary>
    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>
    /// Specifies when the environment exists locally
    /// </summary>
    public bool HasLocal { get; set; }

    /// <summary>
    /// Specifies when the environment exists remotely
    /// </summary>
    public bool HasRemote { get; set; }

    /// <summary>
    /// Specifies when the environment is the default environment
    /// </summary>
    public bool IsDefault { get; set; }
}
