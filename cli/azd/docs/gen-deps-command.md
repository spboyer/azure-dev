# Azure Developer CLI: Service Dependencies (Deprecated)

> **DEPRECATED**: This command is deprecated. Please use [`azd dep add`](./dep-commands.md) instead.

The `azd gen deps` command helps you define dependencies between services in your Azure Developer CLI project. When services depend on each other, Azure Developer CLI can automatically handle deployment order and connection information.

## Overview

When building multi-service applications, services often depend on other services. For example, a web application might depend on an API service, which in turn might depend on a database. The `azd gen deps` command makes it easy to define these relationships in your `azure.yaml` configuration file.

By defining service dependencies, you get the following benefits:

1. **Correct deployment order** - Dependent services are deployed only after their dependencies
2. **Automatic connection strings** - Connection information from dependency services can be passed to dependent services
3. **Environment variables** - Environment variables can be shared from one service to another

## Usage

You can use the `azd gen deps` command in two ways:

### Interactive Mode

```powershell
azd gen deps
```

This mode will:
1. Prompt you to select a source service (the service that will depend on another)
2. Prompt you to select a dependency service (the service that will be deployed first)
3. Update your `azure.yaml` file with the dependency relationship

### Direct Mode

```powershell
azd gen deps <source-service> <dependency-service>
```

For example:
```powershell
azd gen deps api database
```

This defines that the `api` service depends on the `database` service.

## Configuration

The dependencies are stored in your `azure.yaml` file under each service's `dependsOn` property. For example:

```yaml
services:
  api:
    project: ./src/api
    language: node
    host: appservice
    dependsOn:
      - database
  
  database:
    project: ./src/database
    language: sql
    host: azure-sql
```

This configuration ensures that:
1. The `database` service is deployed first
2. The `api` service is deployed after the `database` service
3. Connection information from the `database` can be made available to the `api`

## Options

The `azd gen deps` command supports the following option:

* `--force`: Overwrite an existing dependency if one already exists

Example:
```powershell
azd gen deps api database --force
```

## Examples

### Define a web app that depends on an API

```powershell
azd gen deps webapp api
```

### Define that an API depends on a database

```powershell
azd gen deps api database
```

### Create a chain of dependencies

```powershell
azd gen deps webapp api
azd gen deps api database
```

This creates a chain: `webapp` → `api` → `database`

### Define multiple dependencies for a single service

```powershell
azd gen deps webapp api
azd gen deps webapp database
```

This results in `webapp` depending on both `api` and `database`:

```yaml
services:
  webapp:
    project: ./src/webapp
    language: js
    host: appservice
    dependsOn:
      - api
      - database
  
  api:
    # ...service configuration...
  
  database:
    # ...service configuration...
```

## Advanced Features

### Dependency Validation

Azure Developer CLI validates service dependencies during operations like `azd provision` and `azd infra synth` to ensure that:

1. **All referenced services exist** - If a service depends on a non-existent service, warnings are displayed
2. **No cyclic dependencies** - Dependencies that create loops (like A → B → C → A) are detected and reported

When validation issues are found, Azure Developer CLI will:
1. Display detailed warnings about the problematic dependencies
2. Allow the operation to continue while informing you about potential issues
3. Log information about correct dependencies that will be processed

### Infrastructure Provider Integration

Different infrastructure providers handle service dependencies differently:

#### Bicep Provider

For Bicep templates, dependencies between services are expressed using the Bicep `dependsOn` property. The Azure Developer CLI automatically:

1. Formats dependency relationships in Bicep syntax
2. Ensures correct resource deployment order
3. Passes connection information between dependent resources

#### Terraform Provider

For Terraform configurations, dependencies between services are handled using Terraform references. Azure Developer CLI:

1. Formats dependencies using Terraform's dependency syntax
2. Ensures proper module dependencies in the generated code
3. Manages variable flow between dependent resources

### Troubleshooting Dependencies

If you encounter issues with service dependencies:

1. Check the Azure Developer CLI logs for warnings about invalid services or cyclic dependencies
2. Ensure all services referenced in `dependsOn` are correctly defined in `azure.yaml`
3. Avoid circular dependencies between services
4. Validate that infrastructure code properly references the dependencies

## Notes

* You need at least two services defined in your Azure Developer CLI project to use this command
* Dependencies are directional - if service A depends on service B, that doesn't mean service B depends on service A
* You can define multiple dependencies for a service
* The command prevents you from creating circular dependencies
* Use `--force` if you need to redefine an existing dependency
* Dependencies are validated during `azd infra synth` and `azd provision` operations
* Invalid dependencies will generate warnings but won't block operations
* Infrastructure synthesis automatically formats dependencies in provider-specific syntax

## Related Commands

* `azd provision` - Provisions Azure resources with dependency validation
* `azd infra synth` - Synthesizes infrastructure code with dependency validation
* `azd up` - Performs end-to-end deployment respecting service dependencies

## Related Commands

- `azd init`: Initialize a new Azure Developer CLI project
- `azd env`: Manage Azure Developer CLI environments
- `azd up`: Provision resources and deploy services, respecting defined dependencies
