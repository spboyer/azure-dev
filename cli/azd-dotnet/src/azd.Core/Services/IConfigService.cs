// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Dev.Cli.Core.Services;

/// <summary>
/// Service for managing AZD user configuration stored in ~/.azd/config.json
/// Configuration data should not be specific to a given repository/project.
/// </summary>
public interface IConfigService
{
    /// <summary>
    /// Gets the raw configuration data as a dictionary
    /// </summary>
    IReadOnlyDictionary<string, object?> Raw { get; }

    /// <summary>
    /// Gets the raw configuration data with vault references resolved
    /// </summary>
    IReadOnlyDictionary<string, object?> ResolvedRaw { get; }

    /// <summary>
    /// Gets a value indicating whether the configuration is empty
    /// </summary>
    bool IsEmpty { get; }

    /// <summary>
    /// Retrieves the value stored at the specified path
    /// </summary>
    /// <param name="path">Dot-separated path (e.g., "key.subkey.value")</param>
    /// <returns>The value if found, null otherwise</returns>
    object? Get(string path);

    /// <summary>
    /// Retrieves the value stored at the specified path as a string
    /// </summary>
    /// <param name="path">Dot-separated path</param>
    /// <returns>The string value if found, null otherwise</returns>
    string? GetString(string path);

    /// <summary>
    /// Retrieves the map stored at the specified path
    /// </summary>
    /// <param name="path">Dot-separated path</param>
    /// <returns>The dictionary if found, null otherwise</returns>
    IReadOnlyDictionary<string, object?>? GetMap(string path);

    /// <summary>
    /// Retrieves the value stored at the specified path and deserializes it to the specified type
    /// </summary>
    /// <typeparam name="T">The type to deserialize to</typeparam>
    /// <param name="path">Dot-separated path</param>
    /// <returns>The deserialized value if found, default(T) otherwise</returns>
    T? GetSection<T>(string path) where T : class;

    /// <summary>
    /// Stores the value at the specified path
    /// </summary>
    /// <param name="path">Dot-separated path</param>
    /// <param name="value">The value to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetAsync(string path, object? value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a secret at the specified path within a local user vault
    /// </summary>
    /// <param name="path">Dot-separated path</param>
    /// <param name="value">The secret value to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SetSecretAsync(string path, string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the value stored at the specified path
    /// </summary>
    /// <param name="path">Dot-separated path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnsetAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads configuration from the default user config file (~/.azd/config.json)
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves configuration to the default user config file (~/.azd/config.json)
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken = default);
}
