// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Azure.Dev.Cli.Core.Models;
using Azure.Dev.Cli.Core.Parsers;
using Azure.Dev.Cli.Core.Services;
using Azure.Dev.Cli.Core.Validation;
using Microsoft.Extensions.Logging;
using Xunit;
using Moq;

namespace Azure.Dev.Cli.Tests;

public class ProjectServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ProjectService _projectService;

    public ProjectServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"azd-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);

        var mockLogger = new Mock<ILogger<ProjectService>>();
        _projectService = new ProjectService(mockLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithValidYaml_ReturnsProjectConfig()
    {
        // Arrange
        var yamlContent = @"name: test-app
services:
  web:
    project: ./src/web
    language: typescript
    host: containerapp
  api:
    project: ./src/api
    language: dotnet
    host: appservice
";
        var yamlPath = Path.Combine(_testDirectory, "azure.yaml");
        await File.WriteAllTextAsync(yamlPath, yamlContent);

        // Act
        var config = await _projectService.LoadAsync(_testDirectory);

        // Assert
        Assert.Equal("test-app", config.Name);
        Assert.Equal(2, config.Services.Count);
        Assert.True(config.Services.ContainsKey("web"));
        Assert.True(config.Services.ContainsKey("api"));
        Assert.Equal("typescript", config.Services["web"].Language);
        Assert.Equal("containerapp", config.Services["web"].Host);
        Assert.Equal("./src/web", config.Services["web"].RelativePath);
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            async () => await _projectService.LoadAsync(_testDirectory)
        );
    }

    [Fact]
    public async Task SaveAsync_CreatesValidYamlFile()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "my-app",
            Services = new Dictionary<string, ServiceConfig>
            {
                ["web"] = new ServiceConfig
                {
                    RelativePath = "./src/web",
                    Language = "typescript",
                    Host = "containerapp"
                }
            }
        };

        // Act
        await _projectService.SaveAsync(config, _testDirectory);

        // Assert
        var yamlPath = Path.Combine(_testDirectory, "azure.yaml");
        Assert.True(File.Exists(yamlPath));

        // Verify we can read it back
        var loaded = await _projectService.LoadAsync(_testDirectory);
        Assert.Equal(config.Name, loaded.Name);
        Assert.Equal(config.Services.Count, loaded.Services.Count);
    }

    [Fact]
    public async Task InitializeAsync_CreatesNewProject()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "new-project");
        var templateName = "todo-python-mongo@1.0";

        // Act
        var config = await _projectService.InitializeAsync(templateName, projectPath);

        // Assert
        Assert.Equal("new-project", config.Name);
        Assert.NotNull(config.Metadata);
        Assert.Equal(templateName, config.Metadata.Template);
        Assert.True(Directory.Exists(projectPath));
        Assert.True(File.Exists(Path.Combine(projectPath, "azure.yaml")));
    }

    [Fact]
    public async Task DetectAsync_DetectsDotNetProject()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "dotnet-project");
        Directory.CreateDirectory(projectPath);
        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "test.csproj"),
            @"<Project Sdk=""Microsoft.NET.Sdk.Web"">
                <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                </PropertyGroup>
              </Project>"
        );

        // Act
        var detected = await _projectService.DetectAsync(projectPath);

        // Assert
        Assert.Equal("dotnet", detected.Language);
        Assert.Equal("aspnet", detected.Framework);
    }

    [Fact]
    public async Task DetectAsync_DetectsNodeProject()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "node-project");
        Directory.CreateDirectory(projectPath);
        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "package.json"),
            @"{
                ""name"": ""test-app"",
                ""dependencies"": {
                    ""express"": ""^4.18.0""
                }
              }"
        );

        // Act
        var detected = await _projectService.DetectAsync(projectPath);

        // Assert
        Assert.Equal("js", detected.Language);
        Assert.Equal("express", detected.Framework);
    }

    [Fact]
    public async Task DetectAsync_DetectsPythonProject()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "python-project");
        Directory.CreateDirectory(projectPath);
        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "requirements.txt"),
            "Flask==2.3.0\ngunicorn==20.1.0"
        );

        // Act
        var detected = await _projectService.DetectAsync(projectPath);

        // Assert
        Assert.Equal("python", detected.Language);
        Assert.Equal("flask", detected.Framework);
    }
}

public class AzureYamlParserTests
{
    private readonly AzureYamlParser _parser;

    public AzureYamlParserTests()
    {
        _parser = new AzureYamlParser();
    }

    [Fact]
    public void Parse_WithBasicYaml_ReturnsProjectConfig()
    {
        // Arrange
        var yaml = @"name: test-app
services:
  web:
    project: ./src/web
    host: containerapp
";

        // Act
        var config = _parser.Parse(yaml, "/test/path");

        // Assert
        Assert.Equal("test-app", config.Name);
        Assert.Equal("/test/path", config.Path);
        Assert.Single(config.Services);
        Assert.Equal("web", config.Services.First().Value.Name);
    }

    [Fact]
    public void Parse_WithComplexYaml_ReturnsFullConfig()
    {
        // Arrange
        var yaml = @"name: complex-app
metadata:
  template: todo-python-mongo@1.0
services:
  web:
    project: ./src/web
    language: typescript
    host: containerapp
    docker:
      path: ./Dockerfile
      context: .
    hooks:
      predeploy:
        - run: npm run build
  api:
    project: ./src/api
    language: dotnet
    host: appservice
    uses:
      - web
resources:
  redis:
    type: db.redis
infra:
  provider: bicep
  path: ./infra
pipeline:
  provider: github
  secrets:
    - AZURE_CREDENTIALS
";

        // Act
        var config = _parser.Parse(yaml, "/test/path");

        // Assert
        Assert.Equal("complex-app", config.Name);
        Assert.NotNull(config.Metadata);
        Assert.Equal("todo-python-mongo@1.0", config.Metadata.Template);
        Assert.Equal(2, config.Services.Count);
        Assert.NotNull(config.Resources);
        Assert.Single(config.Resources);
        Assert.NotNull(config.Infra);
        Assert.Equal("bicep", config.Infra.Provider);
        Assert.NotNull(config.Pipeline);
        Assert.Equal("github", config.Pipeline.Provider);
    }

    [Fact]
    public void Serialize_WithConfig_ReturnsValidYaml()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "test-app",
            MetaSchemaVersion = "v1.0",
            Services = new Dictionary<string, ServiceConfig>
            {
                ["web"] = new ServiceConfig
                {
                    RelativePath = "./src/web",
                    Language = "typescript",
                    Host = "containerapp"
                }
            }
        };

        // Act
        var yaml = _parser.Serialize(config);

        // Assert
        Assert.Contains("name: test-app", yaml);
        Assert.Contains("yaml-language-server", yaml); // Schema annotation
        Assert.Contains("services:", yaml);
        Assert.Contains("web:", yaml);
        Assert.Contains("host: containerapp", yaml);
    }

    [Fact]
    public void TryValidateYaml_WithValidYaml_ReturnsTrue()
    {
        // Arrange
        var yaml = "name: test-app\nservices: {}";

        // Act
        var isValid = _parser.TryValidateYaml(yaml, out var errorMessage);

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void TryValidateYaml_WithInvalidYaml_ReturnsFalse()
    {
        // Arrange
        var yaml = "name: test-app\n  invalid: indentation";

        // Act
        var isValid = _parser.TryValidateYaml(yaml, out var errorMessage);

        // Assert
        Assert.False(isValid);
        Assert.NotNull(errorMessage);
    }
}

public class ProjectValidatorTests
{
    private readonly ProjectValidator _validator;

    public ProjectValidatorTests()
    {
        _validator = new ProjectValidator();
    }

    [Fact]
    public void Validate_WithValidProject_ReturnsSuccess()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "test-app",
            Services = new Dictionary<string, ServiceConfig>
            {
                ["web"] = new ServiceConfig
                {
                    Name = "web",
                    RelativePath = "./src/web",
                    Language = "typescript",
                    Host = "containerapp"
                }
            }
        };

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithMissingProjectName_ReturnsError()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "",
            Services = new Dictionary<string, ServiceConfig>()
        };

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Project name is required"));
    }

    [Fact]
    public void Validate_WithServiceMissingPathAndImage_ReturnsError()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "test-app",
            Services = new Dictionary<string, ServiceConfig>
            {
                ["web"] = new ServiceConfig
                {
                    Name = "web",
                    Host = "containerapp"
                }
            }
        };

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Must specify either 'project' path or 'image'"));
    }

    [Fact]
    public void Validate_WithCircularDependency_ReturnsError()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "test-app",
            Services = new Dictionary<string, ServiceConfig>
            {
                ["web"] = new ServiceConfig
                {
                    Name = "web",
                    RelativePath = "./web",
                    Uses = new List<string> { "api" }
                },
                ["api"] = new ServiceConfig
                {
                    Name = "api",
                    RelativePath = "./api",
                    Uses = new List<string> { "web" }
                }
            }
        };

        foreach (var service in config.Services.Values)
        {
            service.Project = config;
        }

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Circular dependency"));
    }

    [Fact]
    public void Validate_WithMissingDependency_ReturnsError()
    {
        // Arrange
        var config = new ProjectConfig
        {
            Name = "test-app",
            Services = new Dictionary<string, ServiceConfig>
            {
                ["web"] = new ServiceConfig
                {
                    Name = "web",
                    RelativePath = "./web",
                    Uses = new List<string> { "nonexistent" }
                }
            }
        };

        config.Services["web"].Project = config;

        // Act
        var result = _validator.Validate(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Dependency 'nonexistent' not found"));
    }
}
