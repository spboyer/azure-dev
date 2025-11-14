// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Dev.Cli.Core.Models;

/// <summary>
/// Represents an environment configuration
/// </summary>
public class Environment
{
    /// <summary>
    /// Environment name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Azure subscription ID
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// Azure tenant ID
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure location
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Environment variables
    /// </summary>
    public Dictionary<string, string> Values { get; set; } = new();

    /// <summary>
    /// Whether this is the default environment
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// When the environment was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the environment was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }
}
