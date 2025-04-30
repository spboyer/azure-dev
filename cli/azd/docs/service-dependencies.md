# Service Dependencies in Azure Developer CLI

Azure Developer CLI (azd) allows you to define and manage dependencies between services in your project. This document explains how to use the service dependency commands effectively.

## Overview

In a multi-service application, services often need to be deployed in a specific order and may require connection information from other services. The service dependency features in azd enable you to:

1. Define which services depend on others
2. Ensure correct deployment order
3. Pass connection information between dependent services
4. Generate appropriate infrastructure code that reflects these dependencies

## Managing Service Dependencies

Service dependencies are defined in your `azure.yaml` file. You can manage them using the `azd dep` commands:

### Adding Dependencies

Use the `azd dep add` command to define that one service depends on another:

```bash
# Define that the 'api' service depends on the 'database' service
azd dep add api database
```

If run without arguments, the command will prompt you to select the services interactively:

```bash
azd dep add
```

This adds a `dependsOn` property to the `api` service in your `azure.yaml` file:

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

You can define multiple dependencies for a single service:

```bash
azd dep add webapp api
azd dep add webapp database
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
```

### Listing Dependencies

To view all service dependencies in your project:

```bash
azd dep list
```

This command shows:
- Each service in your project
- Services it depends on
- Services that require it

To view dependencies for a specific service:

```bash
azd dep list api
```

### Removing Dependencies

To remove a dependency relationship:

```bash
# Remove 'database' dependency from 'api' service
azd dep remove api database
```

Like the add command, if run without arguments, it will prompt you interactively:

```bash
azd dep remove
```

## Validation and Error Checking

When running `azd provision` or `azd infra synth`, Azure Developer CLI validates your service dependencies:

1. **Existence Check** - Ensures all referenced services exist
2. **Cycle Detection** - Detects and warns about circular dependencies
3. **Information Logging** - Logs details about the dependencies being processed

If any issues are found, warnings are displayed, but the operation continues to allow for flexibility.

## Infrastructure Integration

Azure Developer CLI handles service dependencies differently based on the infrastructure provider:

### Bicep Provider

For Bicep, dependencies are expressed using the `dependsOn` property:

```bicep
resource apiResource 'Microsoft.Web/sites@2022-03-01' = {
  name: 'api'
  // Other properties...
  dependsOn: [
    resource_database.id
  ]
}
```

### Terraform Provider

For Terraform, dependencies are handled using the `depends_on` attribute:

```terraform
module "api" {
  source = "./modules/api"
  // Other properties...
  depends_on = [
    module.database
  ]
}
```

## Debugging Dependencies

If you encounter issues with service dependencies:

1. Check azd logs for warnings about missing services or cyclic dependencies
2. Verify that your service names are consistent throughout the configuration
3. Run `azd infra synth` to see how dependencies are translated into infrastructure code
4. Use `azd dep list` to visualize the dependency relationships

## Best Practices

1. **Keep Dependencies Simple** - Avoid complex dependency chains when possible
2. **Validate Early** - Run `azd infra synth` to validate dependencies before provisioning
3. **Review Generated Code** - Check the generated infrastructure code to ensure dependencies are reflected correctly
4. **Document Service Relationships** - Document your service dependencies as part of your project documentation

## Related Commands

- `azd dep add` - Define dependencies between services
- `azd dep list` - List dependencies between services
- `azd dep remove` - Remove dependencies between services
- `azd provision` - Provisions Azure resources with dependency validation
- `azd infra synth` - Synthesizes infrastructure code with dependency validation
- `azd up` - Performs end-to-end deployment respecting service dependencies
