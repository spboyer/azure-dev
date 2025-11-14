// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Dev.Cli.Infra.Provisioning;

/// <summary>
/// Interface for infrastructure providers (Bicep, Terraform, etc.)
/// </summary>
public interface IInfrastructureProvider
{
    /// <summary>
    /// Provider name
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Deploy infrastructure
    /// </summary>
    Task<ProvisionResult> ProvisionAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete infrastructure
    /// </summary>
    Task DestroyAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Preview infrastructure changes
    /// </summary>
    Task<PreviewResult> PreviewAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get deployment state
    /// </summary>
    Task<DeploymentState> GetStateAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for provisioning
/// </summary>
public class ProvisionOptions
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Dictionary<string, string> Parameters { get; set; } = new();
    public string InfrastructurePath { get; set; } = string.Empty;
}

/// <summary>
/// Result of provisioning operation
/// </summary>
public class ProvisionResult
{
    public bool Success { get; set; }
    public Dictionary<string, string> Outputs { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Preview of infrastructure changes
/// </summary>
public class PreviewResult
{
    public List<ResourceChange> Changes { get; set; } = new();
}

/// <summary>
/// A resource change in preview
/// </summary>
public class ResourceChange
{
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public ChangeType ChangeType { get; set; }
}

/// <summary>
/// Type of resource change
/// </summary>
public enum ChangeType
{
    Create,
    Update,
    Delete,
    NoChange
}

/// <summary>
/// Deployment state
/// </summary>
public class DeploymentState
{
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, string> Outputs { get; set; } = new();
    public DateTime? LastDeployedAt { get; set; }
}
