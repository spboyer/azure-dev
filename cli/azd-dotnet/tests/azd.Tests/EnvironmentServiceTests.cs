// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Azure.Dev.Tests;

/// <summary>
/// Tests for EnvironmentService
/// </summary>
public class EnvironmentServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly IEnvironmentService _environmentService;
    private readonly IConfigService _configService;

    public EnvironmentServiceTests()
    {
        // Create a unique test directory for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), "azd-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);

        // Change to test directory for the duration of tests
        System.Environment.CurrentDirectory = _testDirectory;

        // Create logger for ConfigService
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<ConfigService>();
        
        _configService = new ConfigService(logger);
        _environmentService = new EnvironmentService(_configService);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    [Fact]
    public async Task CreateAsync_CreatesEnvironment()
    {
        // Arrange
        var envName = "test-env";

        // Act
        var environment = await _environmentService.CreateAsync(envName);

        // Assert
        Assert.NotNull(environment);
        Assert.Equal(envName, environment.Name);
        Assert.Contains(EnvironmentService.EnvNameEnvVarName, environment.Values.Keys);
        Assert.Equal(envName, environment.Values[EnvironmentService.EnvNameEnvVarName]);
        
        // Verify it was persisted
        var retrieved = await _environmentService.GetAsync(envName);
        Assert.NotNull(retrieved);
        Assert.Equal(envName, retrieved.Name);
    }

    [Fact]
    public async Task CreateAsync_WithSubscriptionAndLocation_SetsProperties()
    {
        // Arrange
        var envName = "test-env";
        var subscription = "sub-123";
        var location = "eastus";

        // Act
        var environment = await _environmentService.CreateAsync(envName, subscription, location);

        // Assert
        Assert.Equal(subscription, environment.SubscriptionId);
        Assert.Equal(location, environment.Location);
        Assert.Equal(subscription, environment.Values[EnvironmentService.SubscriptionIdEnvVarName]);
        Assert.Equal(location, environment.Values[EnvironmentService.LocationEnvVarName]);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsException()
    {
        // Arrange
        var envName = "test-env";
        await _environmentService.CreateAsync(envName);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _environmentService.CreateAsync(envName));
    }

    [Fact]
    public async Task CreateAsync_InvalidName_ThrowsException()
    {
        // Arrange
        var invalidName = "test env with spaces";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _environmentService.CreateAsync(invalidName));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task CreateAsync_EmptyName_ThrowsException(string? name)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _environmentService.CreateAsync(name!));
    }

    [Fact]
    public async Task GetAsync_ExistingEnvironment_ReturnsEnvironment()
    {
        // Arrange
        var envName = "test-env";
        await _environmentService.CreateAsync(envName);

        // Act
        var environment = await _environmentService.GetAsync(envName);

        // Assert
        Assert.NotNull(environment);
        Assert.Equal(envName, environment.Name);
    }

    [Fact]
    public async Task GetAsync_NonExistentEnvironment_ReturnsNull()
    {
        // Act
        var environment = await _environmentService.GetAsync("non-existent");

        // Assert
        Assert.Null(environment);
    }

    [Fact]
    public async Task ListAsync_NoEnvironments_ReturnsEmptyList()
    {
        // Act
        var environments = await _environmentService.ListAsync();

        // Assert
        Assert.NotNull(environments);
        Assert.Empty(environments);
    }

    [Fact]
    public async Task ListAsync_MultipleEnvironments_ReturnsAll()
    {
        // Arrange
        await _environmentService.CreateAsync("env1");
        await _environmentService.CreateAsync("env2");
        await _environmentService.CreateAsync("env3");

        // Act
        var environments = await _environmentService.ListAsync();

        // Assert
        Assert.Equal(3, environments.Count);
        Assert.Contains(environments, e => e.Name == "env1");
        Assert.Contains(environments, e => e.Name == "env2");
        Assert.Contains(environments, e => e.Name == "env3");
    }

    [Fact]
    public async Task ListAsync_IncludesDefaultFlag()
    {
        // Arrange
        await _environmentService.CreateAsync("env1");
        await _environmentService.CreateAsync("env2");
        await _environmentService.SetDefaultEnvironmentAsync("env2");

        // Act
        var environments = await _environmentService.ListAsync();

        // Assert
        var env1 = environments.First(e => e.Name == "env1");
        var env2 = environments.First(e => e.Name == "env2");
        Assert.False(env1.IsDefault);
        Assert.True(env2.IsDefault);
    }

    [Fact]
    public async Task SaveAsync_UpdatesEnvironment()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);
        
        // Modify environment
        environment.Values["CUSTOM_KEY"] = "custom_value";
        environment.Location = "westus";

        // Act
        await _environmentService.SaveAsync(environment);

        // Assert
        var retrieved = await _environmentService.GetAsync(envName);
        Assert.NotNull(retrieved);
        Assert.Equal("custom_value", retrieved.Values["CUSTOM_KEY"]);
        Assert.Equal("westus", retrieved.Location);
    }

    [Fact]
    public async Task DeleteAsync_RemovesEnvironment()
    {
        // Arrange
        var envName = "test-env";
        await _environmentService.CreateAsync(envName);

        // Act
        await _environmentService.DeleteAsync(envName);

        // Assert
        var retrieved = await _environmentService.GetAsync(envName);
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentEnvironment_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await _environmentService.DeleteAsync("non-existent"));
    }

    [Fact]
    public async Task DeleteAsync_DefaultEnvironment_ClearsDefault()
    {
        // Arrange
        var envName = "test-env";
        await _environmentService.CreateAsync(envName);
        await _environmentService.SetDefaultEnvironmentAsync(envName);

        // Act
        await _environmentService.DeleteAsync(envName);

        // Assert
        var defaultEnv = await _environmentService.GetDefaultEnvironmentNameAsync();
        Assert.Null(defaultEnv);
    }

    [Fact]
    public async Task GetDefaultEnvironmentNameAsync_NoDefault_ReturnsNull()
    {
        // Act
        var defaultEnv = await _environmentService.GetDefaultEnvironmentNameAsync();

        // Assert
        Assert.Null(defaultEnv);
    }

    [Fact]
    public async Task SetDefaultEnvironmentAsync_SetsDefault()
    {
        // Arrange
        var envName = "test-env";
        await _environmentService.CreateAsync(envName);

        // Act
        await _environmentService.SetDefaultEnvironmentAsync(envName);

        // Assert
        var defaultEnv = await _environmentService.GetDefaultEnvironmentNameAsync();
        Assert.Equal(envName, defaultEnv);
    }

    [Fact]
    public async Task SetDefaultEnvironmentAsync_NonExistentEnvironment_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await _environmentService.SetDefaultEnvironmentAsync("non-existent"));
    }

    [Fact]
    public async Task GetValuesAsync_ReturnsAllValues()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName, "sub-123", "eastus");
        environment.Values["CUSTOM_KEY"] = "custom_value";
        await _environmentService.SaveAsync(environment);

        // Act
        var values = await _environmentService.GetValuesAsync(envName);

        // Assert
        Assert.Contains(EnvironmentService.EnvNameEnvVarName, values.Keys);
        Assert.Contains(EnvironmentService.SubscriptionIdEnvVarName, values.Keys);
        Assert.Contains(EnvironmentService.LocationEnvVarName, values.Keys);
        Assert.Contains("CUSTOM_KEY", values.Keys);
        Assert.Equal("custom_value", values["CUSTOM_KEY"]);
    }

    [Fact]
    public async Task GetValuesAsync_NonExistentEnvironment_ThrowsException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            async () => await _environmentService.GetValuesAsync("non-existent"));
    }

    [Fact]
    public async Task ReloadAsync_RefreshesEnvironmentData()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);
        
        // Modify and save directly through service
        var updatedEnv = await _environmentService.GetAsync(envName);
        updatedEnv!.Values["NEW_KEY"] = "new_value";
        await _environmentService.SaveAsync(updatedEnv);

        // Act - Reload original environment object
        await _environmentService.ReloadAsync(environment);

        // Assert
        Assert.Contains("NEW_KEY", environment.Values.Keys);
        Assert.Equal("new_value", environment.Values["NEW_KEY"]);
    }

    [Theory]
    [InlineData("valid-env", true)]
    [InlineData("valid_env", true)]
    [InlineData("valid.env", true)]
    [InlineData("valid(env)", true)]
    [InlineData("ValidEnv123", true)]
    [InlineData("123-env", true)]
    [InlineData("env with spaces", false)]
    [InlineData("env@special", false)]
    [InlineData("", false)]
    [InlineData("a", true)]
    public void IsValidEnvironmentName_ValidatesCorrectly(string name, bool expected)
    {
        // Act
        var isValid = _environmentService.IsValidEnvironmentName(name);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void IsValidEnvironmentName_TooLong_ReturnsFalse()
    {
        // Arrange - Create a 65 character name
        var longName = new string('a', 65);

        // Act
        var isValid = _environmentService.IsValidEnvironmentName(longName);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void IsValidEnvironmentName_MaxLength_ReturnsTrue()
    {
        // Arrange - Create a 64 character name
        var maxLengthName = new string('a', 64);

        // Act
        var isValid = _environmentService.IsValidEnvironmentName(maxLengthName);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public async Task GetEnvPath_ReturnsCorrectPath()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);

        // Act
        var envPath = _environmentService.GetEnvPath(environment);

        // Assert
        Assert.Contains(".azure", envPath);
        Assert.Contains(envName, envPath);
        Assert.EndsWith(".env", envPath);
    }

    [Fact]
    public async Task GetConfigPath_ReturnsCorrectPath()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);

        // Act
        var configPath = _environmentService.GetConfigPath(environment);

        // Assert
        Assert.Contains(".azure", configPath);
        Assert.Contains(envName, configPath);
        Assert.EndsWith("config.json", configPath);
    }

    [Theory]
    [InlineData("my env", "my-env")]
    [InlineData("my@env", "my-env")]
    [InlineData("my env with spaces", "my-env-with-spaces")]
    [InlineData("valid-env", "valid-env")]
    [InlineData("env_123", "env_123")]
    public void CleanName_ReplacesInvalidCharacters(string input, string expected)
    {
        // Act
        var cleaned = EnvironmentService.CleanName(input);

        // Assert
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public async Task ServiceProperties_GetAndSet_WorkCorrectly()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);
        var serviceName = "api-service";
        var propertyName = "ENDPOINT_URL";
        var propertyValue = "https://api.example.com";

        // Act
        EnvironmentService.SetServiceProperty(environment, serviceName, propertyName, propertyValue);
        await _environmentService.SaveAsync(environment);

        // Reload and verify
        var reloaded = await _environmentService.GetAsync(envName);
        var retrievedValue = EnvironmentService.GetServiceProperty(reloaded!, serviceName, propertyName);

        // Assert
        Assert.Equal(propertyValue, retrievedValue);
    }

    [Fact]
    public async Task DotEnvFile_PreservesNumericValues()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);
        
        // Set a value with leading zeros
        environment.Values["NUMERIC_VALUE"] = "0123456";
        await _environmentService.SaveAsync(environment);

        // Act - Reload
        var reloaded = await _environmentService.GetAsync(envName);

        // Assert - Leading zeros should be preserved
        Assert.Equal("0123456", reloaded!.Values["NUMERIC_VALUE"]);
    }

    [Fact]
    public async Task DotEnvFile_HandlesSpecialCharacters()
    {
        // Arrange
        var envName = "test-env";
        var environment = await _environmentService.CreateAsync(envName);
        
        // Set values with special characters
        environment.Values["VALUE_WITH_SPACES"] = "value with spaces";
        environment.Values["VALUE_WITH_QUOTES"] = "value \"with\" quotes";
        await _environmentService.SaveAsync(environment);

        // Act - Reload
        var reloaded = await _environmentService.GetAsync(envName);

        // Assert
        Assert.Equal("value with spaces", reloaded!.Values["VALUE_WITH_SPACES"]);
        // Note: Quotes are escaped in storage
        Assert.Contains("quotes", reloaded.Values["VALUE_WITH_QUOTES"]);
    }
}
