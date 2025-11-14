// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Core;
using Azure.Identity;

namespace Azure.Dev.Cli.Auth;

/// <summary>
/// Service for Azure authentication
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Login to Azure interactively
    /// </summary>
    Task<AuthResult> LoginAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Logout from Azure
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current access token
    /// </summary>
    Task<AccessToken> GetAccessTokenAsync(string[] scopes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user is authenticated
    /// </summary>
    Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get credential for Azure SDK
    /// </summary>
    TokenCredential GetCredential();
}

/// <summary>
/// Authentication result
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? UserId { get; set; }
    public string? TenantId { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Azure authentication service implementation
/// </summary>
public class AuthService : IAuthService
{
    private readonly DefaultAzureCredentialOptions _credentialOptions;
    private TokenCredential? _credential;

    public AuthService()
    {
        _credentialOptions = new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeWorkloadIdentityCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeSharedTokenCacheCredential = false,
            ExcludeVisualStudioCredential = false,
            ExcludeVisualStudioCodeCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeAzurePowerShellCredential = false,
            ExcludeAzureDeveloperCliCredential = false,
            ExcludeInteractiveBrowserCredential = false
        };
    }

    public async Task<AuthResult> LoginAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use interactive browser credential for login
            var credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
            {
                TenantId = "common",
                ClientId = "04b07795-8ddb-461a-bbee-02f9e1bf7b46" // Azure CLI client ID
            });

            // Test the credential by getting a token
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                cancellationToken);

            _credential = credential;

            return new AuthResult
            {
                Success = true
            };
        }
        catch (Exception ex)
        {
            return new AuthResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _credential = null;
        // TODO: Clear token cache
        return Task.CompletedTask;
    }

    public async Task<AccessToken> GetAccessTokenAsync(string[] scopes, CancellationToken cancellationToken = default)
    {
        var credential = GetCredential();
        return await credential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken);
    }

    public async Task<bool> IsAuthenticatedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = GetCredential();
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                cancellationToken);
            return !string.IsNullOrEmpty(token.Token);
        }
        catch
        {
            return false;
        }
    }

    public TokenCredential GetCredential()
    {
        return _credential ?? new DefaultAzureCredential(_credentialOptions);
    }
}
