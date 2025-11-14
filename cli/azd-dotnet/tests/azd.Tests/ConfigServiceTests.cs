// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Azure.Dev.Cli.Tests;

public class ConfigServiceTests : IDisposable
{
    private readonly string _tempConfigDir;
    private readonly Mock<ILogger<ConfigService>> _mockLogger;
    private readonly ConfigService _configService;

    public ConfigServiceTests()
    {
        _tempConfigDir = Path.Combine(Path.GetTempPath(), "azd-config-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempConfigDir);
        Environment.SetEnvironmentVariable("AZD_CONFIG_DIR", _tempConfigDir);

        _mockLogger = new Mock<ILogger<ConfigService>>();
        _configService = new ConfigService(_mockLogger.Object);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AZD_CONFIG_DIR", null);
        if (Directory.Exists(_tempConfigDir))
        {
            Directory.Delete(_tempConfigDir, true);
        }
    }

    [Fact]
    public void IsEmpty_NewConfig_ReturnsTrue()
    {
        // Arrange & Act
        var isEmpty = _configService.IsEmpty;

        // Assert
        Assert.True(isEmpty);
    }

    [Fact]
    public async Task SetAsync_SimpleValue_StoresValue()
    {
        // Arrange
        const string path = "test.key";
        const string value = "test-value";

        // Act
        await _configService.SetAsync(path, value);
        var retrieved = _configService.GetString(path);

        // Assert
        Assert.Equal(value, retrieved);
        Assert.False(_configService.IsEmpty);
    }

    [Fact]
    public async Task SetAsync_NestedValue_CreatesNestedStructure()
    {
        // Arrange
        const string path = "level1.level2.level3";
        const string value = "deep-value";

        // Act
        await _configService.SetAsync(path, value);
        var retrieved = _configService.GetString(path);

        // Assert
        Assert.Equal(value, retrieved);
    }

    [Fact]
    public async Task GetMap_ExistingPath_ReturnsMap()
    {
        // Arrange
        await _configService.SetAsync("parent.child1", "value1");
        await _configService.SetAsync("parent.child2", "value2");

        // Act
        var map = _configService.GetMap("parent");

        // Assert
        Assert.NotNull(map);
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public async Task UnsetAsync_ExistingValue_RemovesValue()
    {
        // Arrange
        const string path = "test.key";
        await _configService.SetAsync(path, "value");

        // Act
        await _configService.UnsetAsync(path);
        var retrieved = _configService.Get(path);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_PersistsData()
    {
        // Arrange
        await _configService.SetAsync("persisted.key", "persisted-value");
        await _configService.SaveAsync();

        // Act - Create new instance and load
        var newConfigService = new ConfigService(_mockLogger.Object);
        await newConfigService.LoadAsync();
        var retrieved = newConfigService.GetString("persisted.key");

        // Assert
        Assert.Equal("persisted-value", retrieved);
    }

    [Fact]
    public async Task SetSecretAsync_AndGet_StoresAndRetrievesSecret()
    {
        // Arrange
        const string path = "secrets.api-key";
        const string secretValue = "super-secret-value";

        // Act
        await _configService.SetSecretAsync(path, secretValue);
        var retrieved = _configService.GetString(path);

        // Assert
        Assert.Equal(secretValue, retrieved);
    }

    [Fact]
    public async Task SetSecretAsync_CreatesVaultReference()
    {
        // Arrange
        const string path = "secrets.password";
        const string secretValue = "my-password";

        // Act
        await _configService.SetSecretAsync(path, secretValue);
        var rawValue = _configService.Raw["secrets"];

        // Assert
        Assert.NotNull(rawValue);
        Assert.True(_configService.Raw.ContainsKey("vault"));
    }

    [Fact]
    public async Task GetSection_WithValidPath_DeserializesToType()
    {
        // Arrange
        await _configService.SetAsync("database.host", "localhost");
        await _configService.SetAsync("database.port", 5432);
        await _configService.SetAsync("database.name", "testdb");

        // Act
        var section = _configService.GetSection<DatabaseConfig>("database");

        // Assert
        Assert.NotNull(section);
        Assert.Equal("localhost", section.Host);
        Assert.Equal(5432, section.Port);
        Assert.Equal("testdb", section.Name);
    }

    [Fact]
    public void Get_NonExistentPath_ReturnsNull()
    {
        // Act
        var value = _configService.Get("non.existent.path");

        // Assert
        Assert.Null(value);
    }

    [Fact]
    public async Task ResolvedRaw_ExcludesVaultKey()
    {
        // Arrange
        await _configService.SetSecretAsync("secret.value", "test");
        await _configService.SetAsync("normal.value", "test");

        // Act
        var resolvedRaw = _configService.ResolvedRaw;

        // Assert
        Assert.False(resolvedRaw.ContainsKey("vault"));
        Assert.True(resolvedRaw.ContainsKey("secret"));
        Assert.True(resolvedRaw.ContainsKey("normal"));
    }

    // Helper class for testing GetSection
    private class DatabaseConfig
    {
        public string? Host { get; set; }
        public int Port { get; set; }
        public string? Name { get; set; }
    }
}
