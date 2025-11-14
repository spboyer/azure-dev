// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Dev.Cli.Core.Models;

/// <summary>
/// Represents an Azure project configuration (azure.yaml)
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Name of the project
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to infrastructure files
    /// </summary>
    public string? InfraFolder { get; set; }

    /// <summary>
    /// Services defined in the project
    /// </summary>
    public Dictionary<string, ServiceConfig> Services { get; set; } = new();

    /// <summary>
    /// Pipeline configuration
    /// </summary>
    public PipelineConfig? Pipeline { get; set; }

    /// <summary>
    /// Project metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a service within the project
/// </summary>
public class ServiceConfig
{
    /// <summary>
    /// Programming language of the service
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Hosting platform (appservice, containerapp, aks, etc.)
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Path to the service source code
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Docker configuration
    /// </summary>
    public DockerConfig? Docker { get; set; }

    /// <summary>
    /// Infrastructure module name
    /// </summary>
    public string? Module { get; set; }

    /// <summary>
    /// Service hooks
    /// </summary>
    public HooksConfig? Hooks { get; set; }
}

/// <summary>
/// Docker configuration for a service
/// </summary>
public class DockerConfig
{
    /// <summary>
    /// Path to Dockerfile
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Docker build context
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Target stage in multi-stage build
    /// </summary>
    public string? Target { get; set; }
}

/// <summary>
/// Service lifecycle hooks
/// </summary>
public class HooksConfig
{
    /// <summary>
    /// Pre-provision hooks
    /// </summary>
    public List<string>? PreProvision { get; set; }

    /// <summary>
    /// Post-provision hooks
    /// </summary>
    public List<string>? PostProvision { get; set; }

    /// <summary>
    /// Pre-deploy hooks
    /// </summary>
    public List<string>? PreDeploy { get; set; }

    /// <summary>
    /// Post-deploy hooks
    /// </summary>
    public List<string>? PostDeploy { get; set; }

    /// <summary>
    /// Pre-package hooks
    /// </summary>
    public List<string>? PrePackage { get; set; }

    /// <summary>
    /// Post-package hooks
    /// </summary>
    public List<string>? PostPackage { get; set; }
}

/// <summary>
/// Pipeline configuration
/// </summary>
public class PipelineConfig
{
    /// <summary>
    /// Pipeline provider (github, azdo)
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Pipeline configuration path
    /// </summary>
    public string? Path { get; set; }
}
