// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Models;

namespace Azure.Dev.Cli.Core.Validation;

/// <summary>
/// Validator for project configuration
/// </summary>
public class ProjectValidator
{
    /// <summary>
    /// Validate project configuration
    /// </summary>
    public ValidationResult Validate(ProjectConfig config)
    {
        var result = new ValidationResult();

        // Validate project name
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            result.AddError("Project name is required");
        }

        // Validate services
        if (config.Services.Count == 0)
        {
            result.AddWarning("Project has no services defined");
        }

        foreach (var (serviceName, serviceConfig) in config.Services)
        {
            ValidateService(serviceName, serviceConfig, result);
        }

        // Validate resources
        if (config.Resources != null)
        {
            foreach (var (resourceName, resourceConfig) in config.Resources)
            {
                ValidateResource(resourceName, resourceConfig, result);
            }
        }

        // Validate infrastructure configuration
        if (config.Infra != null)
        {
            ValidateInfrastructure(config.Infra, result);
        }

        // Validate dependencies
        ValidateDependencies(config, result);

        return result;
    }

    private void ValidateService(string serviceName, ServiceConfig serviceConfig, ValidationResult result)
    {
        var context = $"Service '{serviceName}'";

        // Validate service has either a path or an image
        if (string.IsNullOrWhiteSpace(serviceConfig.RelativePath) && string.IsNullOrWhiteSpace(serviceConfig.Image))
        {
            result.AddError($"{context}: Must specify either 'project' path or 'image'");
        }

        // Validate host is specified
        if (string.IsNullOrWhiteSpace(serviceConfig.Host))
        {
            result.AddWarning($"{context}: No 'host' specified. Deployment target will need to be determined.");
        }

        // Validate language is specified if project path is set
        if (!string.IsNullOrWhiteSpace(serviceConfig.RelativePath) && string.IsNullOrWhiteSpace(serviceConfig.Language))
        {
            result.AddWarning($"{context}: No 'language' specified. Language will be auto-detected.");
        }

        // Validate Docker configuration if specified
        if (serviceConfig.Docker != null)
        {
            ValidateDockerConfig(serviceName, serviceConfig.Docker, result);
        }

        // Validate hooks
        if (serviceConfig.Hooks != null)
        {
            ValidateHooks(serviceName, serviceConfig.Hooks, result);
        }

        // Validate dependencies exist
        if (serviceConfig.Uses != null)
        {
            foreach (var dependency in serviceConfig.Uses)
            {
                if (serviceConfig.Project != null)
                {
                    var dependencyExists = serviceConfig.Project.Services.ContainsKey(dependency) ||
                                          (serviceConfig.Project.Resources?.ContainsKey(dependency) ?? false);
                    
                    if (!dependencyExists)
                    {
                        result.AddError($"{context}: Dependency '{dependency}' not found in project");
                    }
                }
            }
        }
    }

    private void ValidateResource(string resourceName, ResourceConfig resourceConfig, ValidationResult result)
    {
        var context = $"Resource '{resourceName}'";

        // Validate resource type is specified
        if (string.IsNullOrWhiteSpace(resourceConfig.Type))
        {
            result.AddError($"{context}: Resource type is required");
        }

        // Validate dependencies exist
        if (resourceConfig.Uses != null)
        {
            foreach (var dependency in resourceConfig.Uses)
            {
                if (resourceConfig.Project != null)
                {
                    var dependencyExists = resourceConfig.Project.Services.ContainsKey(dependency) ||
                                          (resourceConfig.Project.Resources?.ContainsKey(dependency) ?? false);
                    
                    if (!dependencyExists)
                    {
                        result.AddError($"{context}: Dependency '{dependency}' not found in project");
                    }
                }
            }
        }
    }

    private void ValidateDockerConfig(string serviceName, DockerProjectOptions docker, ValidationResult result)
    {
        var context = $"Service '{serviceName}' Docker configuration";

        // If a dockerfile path is specified, ensure it exists (relative to service or absolute)
        // Note: We can't validate file existence here as we don't have filesystem access in validator
        // This would be done in the service implementation

        // Validate build args format
        if (docker.BuildArgs != null)
        {
            foreach (var (key, value) in docker.BuildArgs)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    result.AddError($"{context}: Build arg key cannot be empty");
                }
            }
        }
    }

    private void ValidateHooks(string serviceName, HooksConfig hooks, ValidationResult result)
    {
        var context = $"Service '{serviceName}' hooks";

        ValidateHookList(context, "preprovision", hooks.PreProvision, result);
        ValidateHookList(context, "postprovision", hooks.PostProvision, result);
        ValidateHookList(context, "predeploy", hooks.PreDeploy, result);
        ValidateHookList(context, "postdeploy", hooks.PostDeploy, result);
        ValidateHookList(context, "prepackage", hooks.PrePackage, result);
        ValidateHookList(context, "postpackage", hooks.PostPackage, result);
    }

    private void ValidateHookList(string context, string hookName, List<HookConfig>? hooks, ValidationResult result)
    {
        if (hooks == null) return;

        for (int i = 0; i < hooks.Count; i++)
        {
            var hook = hooks[i];
            if (string.IsNullOrWhiteSpace(hook.Run))
            {
                result.AddError($"{context}.{hookName}[{i}]: 'run' command is required");
            }
        }
    }

    private void ValidateInfrastructure(InfraOptions infra, ValidationResult result)
    {
        // Validate provider is recognized
        if (!string.IsNullOrWhiteSpace(infra.Provider))
        {
            var validProviders = new[] { "bicep", "terraform" };
            if (!validProviders.Contains(infra.Provider.ToLowerInvariant()))
            {
                result.AddWarning($"Infrastructure provider '{infra.Provider}' is not a recognized provider. Valid providers: {string.Join(", ", validProviders)}");
            }
        }
    }

    private void ValidateDependencies(ProjectConfig config, ValidationResult result)
    {
        // Build dependency graph to detect cycles
        var graph = new Dictionary<string, List<string>>();
        
        // Add services to graph
        foreach (var (serviceName, serviceConfig) in config.Services)
        {
            graph[serviceName] = serviceConfig.Uses ?? new List<string>();
        }

        // Add resources to graph
        if (config.Resources != null)
        {
            foreach (var (resourceName, resourceConfig) in config.Resources)
            {
                graph[resourceName] = resourceConfig.Uses ?? new List<string>();
            }
        }

        // Check for cycles
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        foreach (var node in graph.Keys)
        {
            if (HasCycle(node, graph, visited, recursionStack, out var cycle))
            {
                result.AddError($"Circular dependency detected: {string.Join(" -> ", cycle)}");
                break;
            }
        }
    }

    private bool HasCycle(string node, Dictionary<string, List<string>> graph, 
        HashSet<string> visited, HashSet<string> recursionStack, out List<string> cycle)
    {
        cycle = new List<string>();

        if (!visited.Contains(node))
        {
            visited.Add(node);
            recursionStack.Add(node);

            if (graph.ContainsKey(node))
            {
                foreach (var neighbor in graph[node])
                {
                    if (!graph.ContainsKey(neighbor))
                        continue;

                    if (!visited.Contains(neighbor))
                    {
                        if (HasCycle(neighbor, graph, visited, recursionStack, out cycle))
                        {
                            cycle.Insert(0, node);
                            return true;
                        }
                    }
                    else if (recursionStack.Contains(neighbor))
                    {
                        cycle.Add(neighbor);
                        cycle.Add(node);
                        return true;
                    }
                }
            }
        }

        recursionStack.Remove(node);
        return false;
    }
}

/// <summary>
/// Result of project validation
/// </summary>
public class ValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();

    public bool IsValid => Errors.Count == 0;

    public void AddError(string error) => Errors.Add(error);
    public void AddWarning(string warning) => Warnings.Add(warning);

    public override string ToString()
    {
        var messages = new List<string>();
        
        if (Errors.Count > 0)
        {
            messages.Add($"Errors ({Errors.Count}):");
            messages.AddRange(Errors.Select(e => $"  - {e}"));
        }
        
        if (Warnings.Count > 0)
        {
            messages.Add($"Warnings ({Warnings.Count}):");
            messages.AddRange(Warnings.Select(w => $"  - {w}"));
        }

        return messages.Count > 0 ? string.Join(System.Environment.NewLine, messages) : "Validation passed";
    }
}
