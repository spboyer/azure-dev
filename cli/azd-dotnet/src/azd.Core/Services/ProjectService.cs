// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Models;
using Azure.Dev.Cli.Core.Parsers;
using Microsoft.Extensions.Logging;

namespace Azure.Dev.Cli.Core.Services;

/// <summary>
/// Service for managing project configuration from azure.yaml
/// </summary>
public class ProjectService : IProjectService
{
    private const string AzureYamlFileName = "azure.yaml";
    private const string DefaultSchemaVersion = "v1.0";

    private readonly ILogger<ProjectService> _logger;
    private readonly AzureYamlParser _parser;

    public ProjectService(ILogger<ProjectService> logger)
    {
        _logger = logger;
        _parser = new AzureYamlParser();
    }

    /// <summary>
    /// Load project configuration from azure.yaml
    /// </summary>
    public async Task<ProjectConfig> LoadAsync(string? projectPath = null, CancellationToken cancellationToken = default)
    {
        projectPath = ResolveProjectPath(projectPath);
        var azureYamlPath = Path.Combine(projectPath, AzureYamlFileName);

        _logger.LogInformation("Loading project configuration from {Path}", azureYamlPath);

        if (!File.Exists(azureYamlPath))
        {
            throw new FileNotFoundException($"azure.yaml not found at: {azureYamlPath}");
        }

        try
        {
            var config = await _parser.ParseFileAsync(azureYamlPath, cancellationToken);
            _logger.LogInformation("Successfully loaded project '{ProjectName}' with {ServiceCount} services", 
                config.Name, config.Services.Count);
            
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load project configuration from {Path}", azureYamlPath);
            throw new InvalidOperationException($"Failed to parse azure.yaml: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Save project configuration to azure.yaml
    /// </summary>
    public async Task SaveAsync(ProjectConfig config, string? projectPath = null, CancellationToken cancellationToken = default)
    {
        projectPath = ResolveProjectPath(projectPath);
        var azureYamlPath = Path.Combine(projectPath, AzureYamlFileName);

        _logger.LogInformation("Saving project configuration to {Path}", azureYamlPath);

        // Set default schema version if not specified
        if (string.IsNullOrEmpty(config.MetaSchemaVersion))
        {
            config.MetaSchemaVersion = DefaultSchemaVersion;
        }

        try
        {
            await _parser.SerializeToFileAsync(config, azureYamlPath, cancellationToken);
            _logger.LogInformation("Successfully saved project '{ProjectName}'", config.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save project configuration to {Path}", azureYamlPath);
            throw new InvalidOperationException($"Failed to save azure.yaml: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Initialize a new project from a template
    /// </summary>
    public async Task<ProjectConfig> InitializeAsync(string templateName, string projectPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing new project from template '{Template}' at {Path}", templateName, projectPath);

        // Ensure the directory exists
        if (!Directory.Exists(projectPath))
        {
            Directory.CreateDirectory(projectPath);
        }

        // Create a basic project configuration
        var config = new ProjectConfig
        {
            Name = Path.GetFileName(projectPath) ?? "my-app",
            MetaSchemaVersion = DefaultSchemaVersion,
            Path = projectPath,
            Metadata = new ProjectMetadata
            {
                Template = templateName
            },
            Services = new Dictionary<string, ServiceConfig>()
        };

        // Save the initial configuration
        await SaveAsync(config, projectPath, cancellationToken);

        _logger.LogInformation("Successfully initialized project '{ProjectName}'", config.Name);

        return config;
    }

    /// <summary>
    /// Detect project type and language
    /// </summary>
    public async Task<DetectedProject> DetectAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Detecting project type at {Path}", projectPath);

        var detected = new DetectedProject();

        // Check for various project indicators
        var files = Directory.GetFiles(projectPath, "*.*", SearchOption.TopDirectoryOnly);

        // .NET detection
        if (files.Any(f => f.EndsWith(".csproj") || f.EndsWith(".fsproj") || f.EndsWith(".vbproj")))
        {
            detected.Language = "dotnet";
            detected.Framework = await DetectDotNetFrameworkAsync(projectPath, cancellationToken);
        }
        // Node.js detection
        else if (files.Any(f => Path.GetFileName(f) == "package.json"))
        {
            detected.Language = "js";
            detected.Framework = await DetectNodeFrameworkAsync(projectPath, cancellationToken);
        }
        // Python detection
        else if (files.Any(f => Path.GetFileName(f) == "requirements.txt" || Path.GetFileName(f) == "pyproject.toml"))
        {
            detected.Language = "python";
            detected.Framework = await DetectPythonFrameworkAsync(projectPath, cancellationToken);
        }
        // Java detection
        else if (files.Any(f => Path.GetFileName(f) == "pom.xml" || Path.GetFileName(f) == "build.gradle"))
        {
            detected.Language = "java";
            detected.Framework = await DetectJavaFrameworkAsync(projectPath, cancellationToken);
        }
        // Go detection
        else if (files.Any(f => Path.GetFileName(f) == "go.mod"))
        {
            detected.Language = "go";
        }

        // Detect services (subdirectories with project files)
        DetectServices(projectPath, detected);

        _logger.LogInformation("Detected project: Language={Language}, Framework={Framework}, Services={ServiceCount}",
            detected.Language, detected.Framework, detected.DetectedServices.Count);

        return detected;
    }

    private string ResolveProjectPath(string? projectPath)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            projectPath = Directory.GetCurrentDirectory();
        }

        return Path.GetFullPath(projectPath);
    }

    private async Task<string> DetectDotNetFrameworkAsync(string projectPath, CancellationToken cancellationToken)
    {
        var csprojFiles = Directory.GetFiles(projectPath, "*.csproj");
        if (csprojFiles.Length == 0)
            return "dotnet";

        try
        {
            var content = await File.ReadAllTextAsync(csprojFiles[0], cancellationToken);
            if (content.Contains("<Project Sdk=\"Microsoft.NET.Sdk.Web\">"))
                return "aspnet";
            if (content.Contains("Microsoft.Azure.Functions"))
                return "azurefunctions";
        }
        catch
        {
            // Ignore errors during framework detection
        }

        return "dotnet";
    }

    private async Task<string> DetectNodeFrameworkAsync(string projectPath, CancellationToken cancellationToken)
    {
        var packageJsonPath = Path.Combine(projectPath, "package.json");
        if (!File.Exists(packageJsonPath))
            return "node";

        try
        {
            var content = await File.ReadAllTextAsync(packageJsonPath, cancellationToken);
            if (content.Contains("\"express\""))
                return "express";
            if (content.Contains("\"next\""))
                return "next";
            if (content.Contains("\"react\""))
                return "react";
        }
        catch
        {
            // Ignore errors during framework detection
        }

        return "node";
    }

    private async Task<string> DetectPythonFrameworkAsync(string projectPath, CancellationToken cancellationToken)
    {
        var requirementsPath = Path.Combine(projectPath, "requirements.txt");
        if (!File.Exists(requirementsPath))
            return "python";

        try
        {
            var content = await File.ReadAllTextAsync(requirementsPath, cancellationToken);
            if (content.Contains("flask", StringComparison.OrdinalIgnoreCase))
                return "flask";
            if (content.Contains("django", StringComparison.OrdinalIgnoreCase))
                return "django";
            if (content.Contains("fastapi", StringComparison.OrdinalIgnoreCase))
                return "fastapi";
        }
        catch
        {
            // Ignore errors during framework detection
        }

        return "python";
    }

    private async Task<string> DetectJavaFrameworkAsync(string projectPath, CancellationToken cancellationToken)
    {
        var pomPath = Path.Combine(projectPath, "pom.xml");
        if (File.Exists(pomPath))
        {
            try
            {
                var content = await File.ReadAllTextAsync(pomPath, cancellationToken);
                if (content.Contains("spring-boot"))
                    return "springboot";
            }
            catch
            {
                // Ignore errors during framework detection
            }
        }

        return "java";
    }

    private void DetectServices(string projectPath, DetectedProject detected)
    {
        // Look for common service directories
        var subdirectories = Directory.GetDirectories(projectPath);
        
        foreach (var dir in subdirectories)
        {
            var dirName = Path.GetFileName(dir);
            
            // Skip common non-service directories
            if (dirName.StartsWith('.') || 
                dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                dirName.Equals("infra", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if directory contains a project file
            var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly);
            if (files.Any(f => 
                f.EndsWith(".csproj") || 
                f.EndsWith("package.json") || 
                f.EndsWith("requirements.txt") ||
                f.EndsWith("pom.xml")))
            {
                detected.DetectedServices.Add(dirName);
            }
        }
    }
}
