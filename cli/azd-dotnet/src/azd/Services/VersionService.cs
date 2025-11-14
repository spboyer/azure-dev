// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace Azure.Dev.Cli.Services;

public interface IVersionService
{
    string GetVersion();
}

public class VersionService : IVersionService
{
    private const string Version = "1.0.0-dotnet";

    public string GetVersion() => Version;

    public static Version GetCurrentVersion() => new Version(1, 0, 0);
}
