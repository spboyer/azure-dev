// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Azure.Dev.Cli.Core.Parsers;

/// <summary>
/// Parser for azure.yaml project configuration files
/// </summary>
public class AzureYamlParser
{
    private readonly IDeserializer _deserializer;
    private readonly ISerializer _serializer;

    public AzureYamlParser()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    /// <summary>
    /// Parse azure.yaml content into ProjectConfig
    /// </summary>
    public ProjectConfig Parse(string yamlContent, string projectPath)
    {
        var config = _deserializer.Deserialize<ProjectConfig>(yamlContent);
        
        if (config == null)
        {
            throw new InvalidOperationException("Failed to parse azure.yaml - result is null");
        }

        // Set the project path
        config.Path = projectPath;

        // Set up parent references for services
        foreach (var (serviceName, serviceConfig) in config.Services)
        {
            serviceConfig.Project = config;
            serviceConfig.Name = serviceName;
        }

        // Set up parent references for resources
        if (config.Resources != null)
        {
            foreach (var (resourceName, resourceConfig) in config.Resources)
            {
                resourceConfig.Project = config;
                if (string.IsNullOrEmpty(resourceConfig.Name))
                {
                    resourceConfig.Name = resourceName;
                }
            }
        }

        return config;
    }

    /// <summary>
    /// Parse azure.yaml from a file
    /// </summary>
    public async Task<ProjectConfig> ParseFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"azure.yaml not found at: {filePath}");
        }

        var yamlContent = await File.ReadAllTextAsync(filePath, cancellationToken);
        var projectPath = Path.GetDirectoryName(filePath) ?? string.Empty;
        
        return Parse(yamlContent, projectPath);
    }

    /// <summary>
    /// Serialize ProjectConfig to YAML string
    /// </summary>
    public string Serialize(ProjectConfig config)
    {
        var yaml = _serializer.Serialize(config);

        // Add schema annotation if version is specified
        if (!string.IsNullOrEmpty(config.MetaSchemaVersion))
        {
            var schemaComment = $"# yaml-language-server: $schema=https://raw.githubusercontent.com/Azure/azure-dev/main/schemas/{config.MetaSchemaVersion}/azure.yaml.json";
            yaml = schemaComment + System.Environment.NewLine + System.Environment.NewLine + yaml;
        }

        return yaml;
    }

    /// <summary>
    /// Serialize ProjectConfig to a file
    /// </summary>
    public async Task SerializeToFileAsync(ProjectConfig config, string filePath, CancellationToken cancellationToken = default)
    {
        var yaml = Serialize(config);
        
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, yaml, cancellationToken);
    }

    /// <summary>
    /// Validate YAML syntax without full deserialization
    /// </summary>
    public bool TryValidateYaml(string yamlContent, out string? errorMessage)
    {
        try
        {
            _deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
