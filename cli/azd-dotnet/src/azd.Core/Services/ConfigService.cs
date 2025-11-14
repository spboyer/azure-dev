// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Azure.Dev.Cli.Core.Services;

/// <summary>
/// Service for managing AZD user configuration
/// </summary>
public partial class ConfigService : IConfigService
{
    private const string VaultKeyName = "vault";
    private static readonly Regex VaultPattern = GenerateVaultPattern();

    private readonly ILogger<ConfigService> _logger;
    private Dictionary<string, object?> _data = new();
    private string? _vaultId;
    private Dictionary<string, object?>? _vault;
    private string? _configFilePath;

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Raw => _data;

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> ResolvedRaw
    {
        get
        {
            var resolved = new Dictionary<string, object?>();
            var paths = GetAllPaths(_data);

            foreach (var path in paths)
            {
                if (path == VaultKeyName)
                {
                    // Resolved raw should not include vault reference
                    continue;
                }

                var value = Get(path);
                SetValueAtPath(resolved, path, value);
            }

            return resolved;
        }
    }

    /// <inheritdoc/>
    public bool IsEmpty => _data.Count == 0;

    /// <inheritdoc/>
    public object? Get(string path)
    {
        var value = GetValueAtPath(_data, path);

        // Check if it's a vault reference and resolve it
        if (value is string strValue && VaultPattern.IsMatch(strValue) && _vault != null)
        {
            var parts = strValue.Split('/');
            if (parts.Length == 4)
            {
                var secretId = parts[3];
                var secretValue = GetValueAtPath(_vault, secretId);
                if (secretValue is string encodedValue)
                {
                    try
                    {
                        var decodedBytes = Convert.FromBase64String(encodedValue);
                        return Encoding.UTF8.GetString(decodedBytes);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to decode vault secret at path {Path}", path);
                    }
                }
            }
        }

        return value;
    }

    /// <inheritdoc/>
    public string? GetString(string path)
    {
        var value = Get(path);
        return value?.ToString();
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?>? GetMap(string path)
    {
        var value = Get(path);
        if (value is Dictionary<string, object?> dict)
        {
            return dict;
        }

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(jsonElement);
        }

        return null;
    }

    /// <inheritdoc/>
    public T? GetSection<T>(string path) where T : class
    {
        // Get the raw value from the path (not resolved through Get which handles vault references)
        var value = GetValueAtPath(_data, path);
        if (value == null)
        {
            return null;
        }

        if (value is T typedValue)
        {
            return typedValue;
        }

        // If it's a dictionary, we need to resolve any vault references in it
        if (value is Dictionary<string, object?> dict)
        {
            var resolvedDict = new Dictionary<string, object?>();
            foreach (var kvp in dict)
            {
                var fullPath = string.IsNullOrEmpty(path) ? kvp.Key : $"{path}.{kvp.Key}";
                resolvedDict[kvp.Key] = Get(fullPath);
            }
            value = resolvedDict;
        }

        // Try to deserialize using System.Text.Json
        try
        {
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize section at path {Path} to type {Type}", path, typeof(T).Name);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync(string path, object? value, CancellationToken cancellationToken = default)
    {
        SetValueAtPath(_data, path, value);
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SetSecretAsync(string path, string value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_vaultId))
        {
            _vault = new Dictionary<string, object?>();
            _vaultId = Guid.NewGuid().ToString();
            SetValueAtPath(_data, VaultKeyName, _vaultId);
        }

        var pathId = Guid.NewGuid().ToString();
        var vaultRef = $"vault://{_vaultId}/{pathId}";
        var encodedValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

        SetValueAtPath(_vault!, pathId, encodedValue);
        SetValueAtPath(_data, path, vaultRef);

        await SaveAsync(cancellationToken);
        await SaveVaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UnsetAsync(string path, CancellationToken cancellationToken = default)
    {
        RemoveValueAtPath(_data, path);
        await SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _configFilePath = GetConfigFilePath();

        if (File.Exists(_configFilePath))
        {
            var json = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
            _data = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();

            // Load vault ID if present
            if (_data.TryGetValue(VaultKeyName, out var vaultValue) && vaultValue is string vaultId)
            {
                _vaultId = vaultId;
                await LoadVaultAsync(cancellationToken);
            }
        }
        else
        {
            _data = new Dictionary<string, object?>();
        }
    }

    /// <inheritdoc/>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        _configFilePath ??= GetConfigFilePath();

        var configDir = Path.GetDirectoryName(_configFilePath)!;
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        });

        await File.WriteAllTextAsync(_configFilePath, json, cancellationToken);
    }

    private async Task LoadVaultAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_vaultId))
        {
            return;
        }

        var vaultPath = GetVaultFilePath(_vaultId);
        if (File.Exists(vaultPath))
        {
            var json = await File.ReadAllTextAsync(vaultPath, cancellationToken);
            _vault = JsonSerializer.Deserialize<Dictionary<string, object?>>(json) ?? new();
        }
    }

    private async Task SaveVaultAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_vaultId) || _vault == null)
        {
            return;
        }

        var vaultPath = GetVaultFilePath(_vaultId);
        var vaultDir = Path.GetDirectoryName(vaultPath)!;

        if (!Directory.Exists(vaultDir))
        {
            Directory.CreateDirectory(vaultDir);
        }

        var json = JsonSerializer.Serialize(_vault, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(vaultPath, json, cancellationToken);
    }

    private static string GetConfigFilePath()
    {
        var configDir = Environment.GetEnvironmentVariable("AZD_CONFIG_DIR");
        if (string.IsNullOrEmpty(configDir))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            configDir = Path.Combine(homeDir, ".azd");
        }

        return Path.Combine(configDir, "config.json");
    }

    private static string GetVaultFilePath(string vaultId)
    {
        var configDir = Environment.GetEnvironmentVariable("AZD_CONFIG_DIR");
        if (string.IsNullOrEmpty(configDir))
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            configDir = Path.Combine(homeDir, ".azd");
        }

        return Path.Combine(configDir, "vaults", $"{vaultId}.json");
    }

    private static object? GetValueAtPath(Dictionary<string, object?> data, string path)
    {
        var parts = path.Split('.');
        object? current = data;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(part, out current))
                {
                    return null;
                }
            }
            else if (current is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                if (!jsonElement.TryGetProperty(part, out var property))
                {
                    return null;
                }
                current = property;
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static void SetValueAtPath(Dictionary<string, object?> data, string path, object? value)
    {
        var parts = path.Split('.');
        Dictionary<string, object?> current = data;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (i == parts.Length - 1)
            {
                // Last part - set the value
                current[part] = value;
            }
            else
            {
                // Intermediate part - ensure a dictionary exists
                if (!current.TryGetValue(part, out var next) || next is not Dictionary<string, object?>)
                {
                    next = new Dictionary<string, object?>();
                    current[part] = next;
                }
                current = (Dictionary<string, object?>)next;
            }
        }
    }

    private static void RemoveValueAtPath(Dictionary<string, object?> data, string path)
    {
        var parts = path.Split('.');
        Dictionary<string, object?> current = data;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (i == parts.Length - 1)
            {
                // Last part - remove the key
                current.Remove(part);
            }
            else
            {
                // Intermediate part - navigate to the next level
                if (!current.TryGetValue(part, out var next) || next is not Dictionary<string, object?>)
                {
                    return; // Path doesn't exist
                }
                current = (Dictionary<string, object?>)next;
            }
        }
    }

    private static List<string> GetAllPaths(Dictionary<string, object?> data)
    {
        var paths = new List<string>();

        void AddPaths(Dictionary<string, object?> current, string prefix)
        {
            foreach (var kvp in current)
            {
                var fullPath = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (kvp.Value is Dictionary<string, object?> nestedDict)
                {
                    AddPaths(nestedDict, fullPath);
                }
                else
                {
                    paths.Add(fullPath);
                }
            }
        }

        AddPaths(data, string.Empty);
        return paths;
    }

    [GeneratedRegex(@"^vault://[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}/[a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12}$")]
    private static partial Regex GenerateVaultPattern();
}
