# Azure Developer CLI: Service Dependencies

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

## Notes

- You need at least two services defined in your Azure Developer CLI project to use this command
- Dependencies are directional - if service A depends on service B, that doesn't mean service B depends on service A
- You can define multiple dependencies for a service
- The command prevents you from creating circular dependencies
- Use `--force` if you need to redefine an existing dependency

## Related Commands

- `azd init`: Initialize a new Azure Developer CLI project
- `azd env`: Manage Azure Developer CLI environments
- `azd up`: Provision resources and deploy services, respecting defined dependencies
