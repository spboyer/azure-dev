// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Dev.Cli.Services;

public interface IUpdateCheckService
{
    Task<Version> GetLatestVersionAsync(CancellationToken cancellationToken = default);
}

public class UpdateCheckService : IUpdateCheckService
{
    private readonly HttpClient _httpClient;
    private const string LatestVersionUrl = "https://aka.ms/azure-dev/versions/cli/latest";

    public UpdateCheckService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<Version> GetLatestVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(LatestVersionUrl, cancellationToken);
            var versionString = response.Trim();
            return Version.Parse(versionString);
        }
        catch
        {
            // Return current version if check fails
            return VersionService.GetCurrentVersion();
        }
    }
}
