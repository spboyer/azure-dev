// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Services;
using FluentAssertions;
using Xunit;

namespace Azure.Dev.Cli.Tests;

public class VersionServiceTests
{
    [Fact]
    public void GetVersion_ReturnsExpectedVersion()
    {
        // Arrange
        var versionService = new VersionService();

        // Act
        var version = versionService.GetVersion();

        // Assert
        version.Should().NotBeNullOrEmpty();
        version.Should().Contain("dotnet");
    }

    [Fact]
    public void GetCurrentVersion_ReturnsValidVersion()
    {
        // Act
        var version = VersionService.GetCurrentVersion();

        // Assert
        version.Should().NotBeNull();
        version.Major.Should().BeGreaterThan(0);
    }
}
