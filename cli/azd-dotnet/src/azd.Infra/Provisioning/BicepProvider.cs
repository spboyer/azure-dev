// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Azure.Dev.Cli.Infra.Provisioning;

/// <summary>
/// Bicep infrastructure provider
/// </summary>
public class BicepProvider : IInfrastructureProvider
{
    private readonly ArmClient _armClient;
    private readonly ILogger<BicepProvider> _logger;

    public string Name => "bicep";

    public BicepProvider(ArmClient armClient, ILogger<BicepProvider> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    public async Task<ProvisionResult> ProvisionAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Bicep deployment to resource group {ResourceGroup}", options.ResourceGroup);

        var startTime = DateTime.UtcNow;

        try
        {
            // Get subscription
            var subscription = await _armClient.GetSubscriptionResource(
                new Azure.Core.ResourceIdentifier($"/subscriptions/{options.SubscriptionId}"))
                .GetAsync(cancellationToken);

            // Get or create resource group
            var resourceGroup = await GetOrCreateResourceGroupAsync(
                subscription.Value,
                options.ResourceGroup,
                options.Location,
                cancellationToken);

            // Read Bicep file
            var bicepPath = Path.Combine(options.InfrastructurePath, "main.bicep");
            if (!File.Exists(bicepPath))
            {
                throw new FileNotFoundException($"Bicep file not found: {bicepPath}");
            }

            var bicepContent = await File.ReadAllTextAsync(bicepPath, cancellationToken);

            // TODO: Compile Bicep to ARM template
            // This would require running 'az bicep build' or using Bicep .NET SDK

            // TODO: Create deployment - requires Bicep compilation first
            var deploymentName = $"azd-{DateTime.UtcNow:yyyyMMddHHmmss}";
            // var deployment = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            // {
            //     // Template would be set here after compilation
            // });

            // TODO: Start deployment - requires template compilation first
            // var deploymentOperation = await resourceGroup.GetArmDeployments()
            //     .CreateOrUpdateAsync(
            //         Azure.WaitUntil.Completed,
            //         deploymentName,
            //         deployment,
            //         cancellationToken);
            //
            // var result = deploymentOperation.Value;

            // Extract outputs (placeholder until deployment is implemented)
            var outputs = new Dictionary<string, string>();
            // TODO: Extract outputs from deployment result
            // if (result.Data.Properties.Outputs != null)
            // {
            //     foreach (var output in result.Data.Properties.Outputs)
            //     {
            //         outputs[output.Key] = output.Value.ToString() ?? string.Empty;
            //     }
            // }

            var duration = DateTime.UtcNow - startTime;

            _logger.LogInformation("Bicep deployment completed successfully in {Duration}",  duration);

            return new ProvisionResult
            {
                Success = true,
                Outputs = outputs,
                Duration = duration
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bicep deployment failed");

            return new ProvisionResult
            {
                Success = false,
                Errors = new List<string> { ex.Message },
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    public async Task DestroyAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting resource group {ResourceGroup}", options.ResourceGroup);

        var subscription = await _armClient.GetSubscriptionResource(
            new Azure.Core.ResourceIdentifier($"/subscriptions/{options.SubscriptionId}"))
            .GetAsync(cancellationToken);

        var resourceGroup = await subscription.Value.GetResourceGroups()
            .GetAsync(options.ResourceGroup, cancellationToken);

        await resourceGroup.Value.DeleteAsync(Azure.WaitUntil.Completed, cancellationToken: cancellationToken);

        _logger.LogInformation("Resource group deleted successfully");
    }

    public Task<PreviewResult> PreviewAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement what-if operation
        throw new NotImplementedException("Preview not yet implemented for Bicep provider");
    }

    public async Task<DeploymentState> GetStateAsync(
        ProvisionOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _armClient.GetSubscriptionResource(
                new Azure.Core.ResourceIdentifier($"/subscriptions/{options.SubscriptionId}"))
                .GetAsync(cancellationToken);

            var resourceGroup = await subscription.Value.GetResourceGroups()
                .GetAsync(options.ResourceGroup, cancellationToken);

            // Get latest deployment
            var deployments = resourceGroup.Value.GetArmDeployments()
                .GetAllAsync(cancellationToken: cancellationToken);

            ArmDeploymentResource? latestDeployment = null;
            await foreach (var deployment in deployments)
            {
                if (latestDeployment == null ||
                    deployment.Data.Properties.Timestamp > latestDeployment.Data.Properties.Timestamp)
                {
                    latestDeployment = deployment;
                }
            }

            if (latestDeployment == null)
            {
                return new DeploymentState { Status = "NotDeployed" };
            }

            var outputs = new Dictionary<string, string>();
            if (latestDeployment.Data.Properties.Outputs != null)
            {
                // TODO: Parse outputs properly once deployment is implemented
                // foreach (var output in latestDeployment.Data.Properties.Outputs)
                // {
                //     outputs[output.Key] = output.Value.ToString() ?? string.Empty;
                // }
            }

            return new DeploymentState
            {
                Status = latestDeployment.Data.Properties.ProvisioningState?.ToString() ?? "Unknown",
                Outputs = outputs,
                LastDeployedAt = latestDeployment.Data.Properties.Timestamp?.DateTime
            };
        }
        catch
        {
            return new DeploymentState { Status = "Unknown" };
        }
    }

    private async Task<ResourceGroupResource> GetOrCreateResourceGroupAsync(
        SubscriptionResource subscription,
        string resourceGroupName,
        string location,
        CancellationToken cancellationToken)
    {
        try
        {
            return await subscription.GetResourceGroups()
                .GetAsync(resourceGroupName, cancellationToken);
        }
        catch
        {
            _logger.LogInformation("Creating resource group {ResourceGroup} in {Location}", resourceGroupName, location);

            var resourceGroupData = new ResourceGroupData(location);
            var result = await subscription.GetResourceGroups()
                .CreateOrUpdateAsync(
                    Azure.WaitUntil.Completed,
                    resourceGroupName,
                    resourceGroupData,
                    cancellationToken);

            return result.Value;
        }
    }
}
