// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Models;

namespace Azure.Dev.Cli.Core.Services;

/// <summary>
/// Service for managing project configuration
/// </summary>
public interface IProjectService
{
    /// <summary>
    /// Load project configuration from azure.yaml
    /// </summary>
    Task<ProjectConfig> LoadAsync(string? projectPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Save project configuration to azure.yaml
    /// </summary>
    Task SaveAsync(ProjectConfig config, string? projectPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initialize a new project from a template
    /// </summary>
    Task<ProjectConfig> InitializeAsync(string templateName, string projectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect project type and language
    /// </summary>
    Task<DetectedProject> DetectAsync(string projectPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Detected project information
/// </summary>
public class DetectedProject
{
    public string Language { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public List<string> DetectedServices { get; set; } = new();
}
