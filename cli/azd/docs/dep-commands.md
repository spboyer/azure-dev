# Azure Developer CLI: Service Dependencies

The `azd dep` commands help you manage dependencies between services in your Azure Developer CLI project. When services depend on each other, Azure Developer CLI can automatically handle deployment order and connection information.

## Overview

When building multi-service applications, services often depend on other services. For example, a web application might depend on an API service, which in turn might depend on a database. The `azd dep` commands make it easy to define, list, and remove these relationships in your `azure.yaml` configuration file.

By defining service dependencies, you get the following benefits:

1. **Correct deployment order** - Dependent services are deployed only after their dependencies
2. **Automatic connection strings** - Connection information from dependency services can be passed to dependent services
3. **Environment variables** - Environment variables can be shared from one service to another

## Commands

### Add Dependencies

```powershell
azd dep add [service] [dependent-service]
```

This command updates your azure.yaml file to define that one service depends on another service.

#### Examples

```powershell
# Define 'api' service as dependent on 'database' service:
azd dep add api database

# Interactive mode - will prompt for services and dependencies:
azd dep add

# Define multiple dependencies:
azd dep add webapp api
azd dep add webapp database
```

#### Options

* `--force`: Overwrite an existing dependency if one already exists

### List Dependencies

```powershell
azd dep list [service-name]
```

This command displays the dependency relationships defined in your azure.yaml file.

#### Examples

```powershell
# List all service dependencies:
azd dep list

# List dependencies for a specific service:
azd dep list api
```

### Remove Dependencies

```powershell
azd dep remove [service] [dependent-service]
```

This command updates your azure.yaml file to remove a dependency relationship between services.

#### Examples

```powershell
# Remove 'database' dependency from 'api' service:
azd dep remove api database

# Interactive mode - will prompt for services and dependencies:
azd dep remove
```

#### Options

* `--force`: Remove dependency without confirmation prompt

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
  
  # A service with multiple dependencies
  webapp:
    project: ./src/webapp
    language: js
    host: appservice
    dependsOn:
      - api
      - database
  
  database:
    project: ./src/database
    language: sql
    host: azure-sql
```

This configuration ensures that:
1. The `database` service is deployed first
2. The `api` service is deployed after the `database` service
3. The `webapp` service is deployed after both `api` and `database` services
4. Connection information from the services can be made available to their dependent services

## Notes

* You need at least two services defined in your Azure Developer CLI project to use these commands
* Dependencies are directional - if service A depends on service B, that doesn't mean service B depends on service A
* You can define multiple dependencies for a service
* The commands prevent you from creating circular dependencies
* Dependencies are validated during `azd infra synth` and `azd provision` operations
* Invalid dependencies will generate warnings but won't block operations
* Infrastructure synthesis automatically formats dependencies in provider-specific syntax

## Related Commands

* `azd provision` - Provisions Azure resources with dependency validation
* `azd infra synth` - Synthesizes infrastructure code with dependency validation
* `azd up` - Performs end-to-end deployment respecting service dependencies
