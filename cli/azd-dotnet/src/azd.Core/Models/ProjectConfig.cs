// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using YamlDotNet.Serialization;

namespace Azure.Dev.Cli.Core.Models;

/// <summary>
/// Represents an Azure project configuration (azure.yaml)
/// This is the top level object serialized into an azure.yaml file.
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Metadata that specifies the schema version (e.g., "v1.0")
    /// Used during Save to write the file schema annotation for intellisense
    /// </summary>
    [YamlIgnore]
    public string? MetaSchemaVersion { get; set; }

    /// <summary>
    /// Name of the project
    /// </summary>
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Required versions of tools for this project
    /// </summary>
    [YamlMember(Alias = "requiredVersions")]
    public RequiredVersions? RequiredVersions { get; set; }

    /// <summary>
    /// Azure resource group name (supports environment variable expansion)
    /// </summary>
    [YamlMember(Alias = "resourceGroup")]
    public string? ResourceGroupName { get; set; }

    /// <summary>
    /// Path to the project directory (not serialized to YAML)
    /// </summary>
    [YamlIgnore]
    public string? Path { get; set; }

    /// <summary>
    /// Project metadata (template slug, etc.)
    /// </summary>
    [YamlMember(Alias = "metadata")]
    public ProjectMetadata? Metadata { get; set; }

    /// <summary>
    /// Services defined in the project
    /// </summary>
    [YamlMember(Alias = "services")]
    public Dictionary<string, ServiceConfig> Services { get; set; } = new();

    /// <summary>
    /// Infrastructure provisioning options
    /// </summary>
    [YamlMember(Alias = "infra")]
    public InfraOptions? Infra { get; set; }

    /// <summary>
    /// Pipeline configuration
    /// </summary>
    [YamlMember(Alias = "pipeline")]
    public PipelineOptions? Pipeline { get; set; }

    /// <summary>
    /// Project-level hooks
    /// </summary>
    [YamlMember(Alias = "hooks")]
    public HooksConfig? Hooks { get; set; }

    /// <summary>
    /// State management configuration
    /// </summary>
    [YamlMember(Alias = "state")]
    public StateConfig? State { get; set; }

    /// <summary>
    /// Platform-specific configuration
    /// </summary>
    [YamlMember(Alias = "platform")]
    public PlatformConfig? Platform { get; set; }

    /// <summary>
    /// Workflows configuration
    /// </summary>
    [YamlMember(Alias = "workflows")]
    public Dictionary<string, WorkflowConfig>? Workflows { get; set; }

    /// <summary>
    /// Cloud configuration
    /// </summary>
    [YamlMember(Alias = "cloud")]
    public CloudConfig? Cloud { get; set; }

    /// <summary>
    /// Resources defined in the project
    /// </summary>
    [YamlMember(Alias = "resources")]
    public Dictionary<string, ResourceConfig>? Resources { get; set; }
}

/// <summary>
/// Required versions of tools for this project
/// </summary>
public class RequiredVersions
{
    /// <summary>
    /// Semver range for azd CLI (e.g., ">=1.0.0")
    /// </summary>
    [YamlMember(Alias = "azd")]
    public string? Azd { get; set; }

    /// <summary>
    /// Required extension versions
    /// </summary>
    [YamlMember(Alias = "extensions")]
    public Dictionary<string, string>? Extensions { get; set; }
}

/// <summary>
/// Project metadata
/// </summary>
public class ProjectMetadata
{
    /// <summary>
    /// Template slug identifying the template and version (e.g., "todo-python-mongo@1.0")
    /// </summary>
    [YamlMember(Alias = "template")]
    public string? Template { get; set; }
}

/// <summary>
/// Infrastructure provisioning options
/// </summary>
public class InfraOptions
{
    /// <summary>
    /// Infrastructure provider (bicep, terraform)
    /// </summary>
    [YamlMember(Alias = "provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// Path to infrastructure files
    /// </summary>
    [YamlMember(Alias = "path")]
    public string? Path { get; set; }

    /// <summary>
    /// Module name for infrastructure
    /// </summary>
    [YamlMember(Alias = "module")]
    public string? Module { get; set; }
}

/// <summary>
/// State management configuration
/// </summary>
public class StateConfig
{
    /// <summary>
    /// Remote state backend
    /// </summary>
    [YamlMember(Alias = "remote")]
    public RemoteStateConfig? Remote { get; set; }
}

/// <summary>
/// Remote state configuration
/// </summary>
public class RemoteStateConfig
{
    /// <summary>
    /// Backend type (azurerm, etc.)
    /// </summary>
    [YamlMember(Alias = "backend")]
    public string? Backend { get; set; }

    /// <summary>
    /// Backend-specific configuration
    /// </summary>
    [YamlMember(Alias = "config")]
    public Dictionary<string, object>? Config { get; set; }
}

/// <summary>
/// Platform-specific configuration
/// </summary>
public class PlatformConfig
{
    /// <summary>
    /// Platform type (e.g., "devcenter")
    /// </summary>
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    /// <summary>
    /// Platform-specific config
    /// </summary>
    [YamlMember(Alias = "config")]
    public Dictionary<string, object>? Config { get; set; }
}

/// <summary>
/// Workflow configuration
/// </summary>
public class WorkflowConfig
{
    /// <summary>
    /// Workflow steps
    /// </summary>
    [YamlMember(Alias = "steps")]
    public List<WorkflowStep>? Steps { get; set; }
}

/// <summary>
/// Workflow step
/// </summary>
public class WorkflowStep
{
    /// <summary>
    /// Command to execute
    /// </summary>
    [YamlMember(Alias = "azd")]
    public string? Azd { get; set; }

    /// <summary>
    /// Shell command to execute
    /// </summary>
    [YamlMember(Alias = "run")]
    public string? Run { get; set; }

    /// <summary>
    /// Working directory
    /// </summary>
    [YamlMember(Alias = "dir")]
    public string? Dir { get; set; }

    /// <summary>
    /// Environment variables
    /// </summary>
    [YamlMember(Alias = "env")]
    public Dictionary<string, string>? Env { get; set; }

    /// <summary>
    /// Continue on error
    /// </summary>
    [YamlMember(Alias = "continueOnError")]
    public bool ContinueOnError { get; set; }
}

/// <summary>
/// Cloud configuration
/// </summary>
public class CloudConfig
{
    /// <summary>
    /// Cloud provider (azure, etc.)
    /// </summary>
    [YamlMember(Alias = "provider")]
    public string? Provider { get; set; }
}

/// <summary>
/// Represents a service within the project
/// </summary>
public class ServiceConfig
{
    /// <summary>
    /// Reference to the parent project configuration (not serialized)
    /// </summary>
    [YamlIgnore]
    public ProjectConfig? Project { get; set; }

    /// <summary>
    /// The friendly name/key of the service from azure.yaml (not serialized)
    /// </summary>
    [YamlIgnore]
    public string? Name { get; set; }

    /// <summary>
    /// Azure resource group name (supports environment variable expansion)
    /// </summary>
    [YamlMember(Alias = "resourceGroup")]
    public string? ResourceGroupName { get; set; }

    /// <summary>
    /// Name to override the default Azure resource name
    /// </summary>
    [YamlMember(Alias = "resourceName")]
    public string? ResourceName { get; set; }

    /// <summary>
    /// ARM API version to use for the service
    /// </summary>
    [YamlMember(Alias = "apiVersion")]
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Relative path to the project folder from project root
    /// </summary>
    [YamlMember(Alias = "project")]
    public string? RelativePath { get; set; }

    /// <summary>
    /// Azure hosting model (appservice, function, containerapp, aks, etc.)
    /// </summary>
    [YamlMember(Alias = "host")]
    public string? Host { get; set; }

    /// <summary>
    /// Programming language of the service
    /// </summary>
    [YamlMember(Alias = "language")]
    public string? Language { get; set; }

    /// <summary>
    /// Output path for build artifacts
    /// </summary>
    [YamlMember(Alias = "dist")]
    public string? OutputPath { get; set; }

    /// <summary>
    /// Source image for container-based applications
    /// </summary>
    [YamlMember(Alias = "image")]
    public string? Image { get; set; }

    /// <summary>
    /// Docker options for configuring the output image
    /// </summary>
    [YamlMember(Alias = "docker")]
    public DockerProjectOptions? Docker { get; set; }

    /// <summary>
    /// Kubernetes/AKS options
    /// </summary>
    [YamlMember(Alias = "k8s")]
    public AksOptions? K8s { get; set; }

    /// <summary>
    /// Azure Spring Apps options
    /// </summary>
    [YamlMember(Alias = "spring")]
    public SpringOptions? Spring { get; set; }

    /// <summary>
    /// Infrastructure module path relative to root infra folder
    /// </summary>
    [YamlMember(Alias = "module")]
    public string? Module { get; set; }

    /// <summary>
    /// Infrastructure provisioning configuration
    /// </summary>
    [YamlMember(Alias = "infra")]
    public InfraOptions? Infra { get; set; }

    /// <summary>
    /// Service-level hooks
    /// </summary>
    [YamlMember(Alias = "hooks")]
    public HooksConfig? Hooks { get; set; }

    /// <summary>
    /// Dependencies on other services and resources
    /// </summary>
    [YamlMember(Alias = "uses")]
    public List<string>? Uses { get; set; }

    /// <summary>
    /// Custom configuration for the service target
    /// </summary>
    [YamlMember(Alias = "config")]
    public Dictionary<string, object>? Config { get; set; }

    /// <summary>
    /// Environment variables for the service (supports expansion)
    /// </summary>
    [YamlMember(Alias = "env")]
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Indicates if service is build-only (not deployed)
    /// </summary>
    [YamlIgnore]
    public bool BuildOnly { get; set; }

    /// <summary>
    /// Gets the fully qualified path to the service project
    /// </summary>
    public string GetPath()
    {
        if (string.IsNullOrEmpty(RelativePath))
            return Project?.Path ?? string.Empty;

        if (System.IO.Path.IsPathRooted(RelativePath))
            return RelativePath;

        return System.IO.Path.Combine(Project?.Path ?? string.Empty, RelativePath);
    }
}

/// <summary>
/// Docker project options for configuring container images
/// </summary>
public class DockerProjectOptions
{
    /// <summary>
    /// Path to Dockerfile
    /// </summary>
    [YamlMember(Alias = "path")]
    public string? Path { get; set; }

    /// <summary>
    /// Docker build context
    /// </summary>
    [YamlMember(Alias = "context")]
    public string? Context { get; set; }

    /// <summary>
    /// Target stage in multi-stage build
    /// </summary>
    [YamlMember(Alias = "target")]
    public string? Target { get; set; }

    /// <summary>
    /// Registry to push the image to
    /// </summary>
    [YamlMember(Alias = "registry")]
    public string? Registry { get; set; }

    /// <summary>
    /// Image name
    /// </summary>
    [YamlMember(Alias = "image")]
    public string? Image { get; set; }

    /// <summary>
    /// Image tag
    /// </summary>
    [YamlMember(Alias = "tag")]
    public string? Tag { get; set; }

    /// <summary>
    /// Build arguments
    /// </summary>
    [YamlMember(Alias = "buildArgs")]
    public Dictionary<string, string>? BuildArgs { get; set; }
}

/// <summary>
/// Kubernetes/AKS configuration options
/// </summary>
public class AksOptions
{
    /// <summary>
    /// Path to Kubernetes manifest files
    /// </summary>
    [YamlMember(Alias = "deploymentPath")]
    public string? DeploymentPath { get; set; }

    /// <summary>
    /// Kubernetes namespace
    /// </summary>
    [YamlMember(Alias = "namespace")]
    public string? Namespace { get; set; }

    /// <summary>
    /// Helm chart configuration
    /// </summary>
    [YamlMember(Alias = "helm")]
    public HelmOptions? Helm { get; set; }

    /// <summary>
    /// Ingress configuration
    /// </summary>
    [YamlMember(Alias = "ingress")]
    public IngressOptions? Ingress { get; set; }

    /// <summary>
    /// Service configuration
    /// </summary>
    [YamlMember(Alias = "service")]
    public K8sServiceOptions? Service { get; set; }

    /// <summary>
    /// Deployment configuration
    /// </summary>
    [YamlMember(Alias = "deployment")]
    public K8sDeploymentOptions? Deployment { get; set; }
}

/// <summary>
/// Helm chart options
/// </summary>
public class HelmOptions
{
    /// <summary>
    /// Path to Helm chart
    /// </summary>
    [YamlMember(Alias = "chart")]
    public string? Chart { get; set; }

    /// <summary>
    /// Release name
    /// </summary>
    [YamlMember(Alias = "releaseName")]
    public string? ReleaseName { get; set; }

    /// <summary>
    /// Values file path
    /// </summary>
    [YamlMember(Alias = "values")]
    public string? Values { get; set; }
}

/// <summary>
/// Kubernetes ingress options
/// </summary>
public class IngressOptions
{
    /// <summary>
    /// Relative path for routing
    /// </summary>
    [YamlMember(Alias = "relativePath")]
    public string? RelativePath { get; set; }
}

/// <summary>
/// Kubernetes service options
/// </summary>
public class K8sServiceOptions
{
    /// <summary>
    /// Service type (ClusterIP, NodePort, LoadBalancer)
    /// </summary>
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }
}

/// <summary>
/// Kubernetes deployment options
/// </summary>
public class K8sDeploymentOptions
{
    /// <summary>
    /// Container port
    /// </summary>
    [YamlMember(Alias = "port")]
    public int? Port { get; set; }
}

/// <summary>
/// Azure Spring Apps options
/// </summary>
public class SpringOptions
{
    /// <summary>
    /// Spring Cloud config server URI
    /// </summary>
    [YamlMember(Alias = "configServerUri")]
    public string? ConfigServerUri { get; set; }

    /// <summary>
    /// Spring Cloud Eureka server URI
    /// </summary>
    [YamlMember(Alias = "eurekaServerUri")]
    public string? EurekaServerUri { get; set; }
}

/// <summary>
/// Service lifecycle hooks configuration
/// Supports both legacy single hook and new multiple hooks per lifecycle event
/// </summary>
public class HooksConfig
{
    /// <summary>
    /// Pre-provision hooks
    /// </summary>
    [YamlMember(Alias = "preprovision")]
    public List<HookConfig>? PreProvision { get; set; }

    /// <summary>
    /// Post-provision hooks
    /// </summary>
    [YamlMember(Alias = "postprovision")]
    public List<HookConfig>? PostProvision { get; set; }

    /// <summary>
    /// Pre-deploy hooks
    /// </summary>
    [YamlMember(Alias = "predeploy")]
    public List<HookConfig>? PreDeploy { get; set; }

    /// <summary>
    /// Post-deploy hooks
    /// </summary>
    [YamlMember(Alias = "postdeploy")]
    public List<HookConfig>? PostDeploy { get; set; }

    /// <summary>
    /// Pre-package hooks
    /// </summary>
    [YamlMember(Alias = "prepackage")]
    public List<HookConfig>? PrePackage { get; set; }

    /// <summary>
    /// Post-package hooks
    /// </summary>
    [YamlMember(Alias = "postpackage")]
    public List<HookConfig>? PostPackage { get; set; }

    /// <summary>
    /// Pre-restore hooks
    /// </summary>
    [YamlMember(Alias = "prerestore")]
    public List<HookConfig>? PreRestore { get; set; }

    /// <summary>
    /// Post-restore hooks
    /// </summary>
    [YamlMember(Alias = "postrestore")]
    public List<HookConfig>? PostRestore { get; set; }

    /// <summary>
    /// Pre-down hooks
    /// </summary>
    [YamlMember(Alias = "predown")]
    public List<HookConfig>? PreDown { get; set; }

    /// <summary>
    /// Post-down hooks
    /// </summary>
    [YamlMember(Alias = "postdown")]
    public List<HookConfig>? PostDown { get; set; }
}

/// <summary>
/// Individual hook configuration
/// </summary>
public class HookConfig
{
    /// <summary>
    /// Shell command or script to run
    /// </summary>
    [YamlMember(Alias = "run")]
    public string? Run { get; set; }

    /// <summary>
    /// Shell to use (sh, bash, pwsh, etc.)
    /// </summary>
    [YamlMember(Alias = "shell")]
    public string? Shell { get; set; }

    /// <summary>
    /// Continue on error
    /// </summary>
    [YamlMember(Alias = "continueOnError")]
    public bool ContinueOnError { get; set; }

    /// <summary>
    /// Interactive mode
    /// </summary>
    [YamlMember(Alias = "interactive")]
    public bool Interactive { get; set; }

    /// <summary>
    /// Windows-specific command
    /// </summary>
    [YamlMember(Alias = "windows")]
    public HookConfig? Windows { get; set; }

    /// <summary>
    /// POSIX-specific command
    /// </summary>
    [YamlMember(Alias = "posix")]
    public HookConfig? Posix { get; set; }
}

/// <summary>
/// Pipeline configuration options
/// </summary>
public class PipelineOptions
{
    /// <summary>
    /// Pipeline provider (github, azdo)
    /// </summary>
    [YamlMember(Alias = "provider")]
    public string? Provider { get; set; }

    /// <summary>
    /// Variables to expose in the pipeline
    /// </summary>
    [YamlMember(Alias = "variables")]
    public List<string>? Variables { get; set; }

    /// <summary>
    /// Secrets to configure in the pipeline
    /// </summary>
    [YamlMember(Alias = "secrets")]
    public List<string>? Secrets { get; set; }
}

/// <summary>
/// Resource configuration for Azure resources
/// </summary>
public class ResourceConfig
{
    /// <summary>
    /// Reference to the parent project configuration (not serialized)
    /// </summary>
    [YamlIgnore]
    public ProjectConfig? Project { get; set; }

    /// <summary>
    /// Type of resource (db.redis, host.containerapp, etc.)
    /// </summary>
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    /// <summary>
    /// Name of the resource
    /// </summary>
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    /// <summary>
    /// Relationships to other resources
    /// </summary>
    [YamlMember(Alias = "uses")]
    public List<string>? Uses { get; set; }

    /// <summary>
    /// Indicates whether the resource is an existing resource
    /// </summary>
    [YamlMember(Alias = "existing")]
    public bool Existing { get; set; }

    /// <summary>
    /// Resource ID in the project (virtual field stored as environment state)
    /// </summary>
    [YamlIgnore]
    public string? ResourceId { get; set; }

    /// <summary>
    /// Type-specific properties (inline in YAML)
    /// </summary>
    [YamlIgnore]
    public Dictionary<string, object>? Props { get; set; }
}
