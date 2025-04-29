# Service Dependencies in Azure Developer CLI

Azure Developer CLI (azd) allows you to define and manage dependencies between services in your project. This document explains how to use this feature effectively.

## Overview

In a multi-service application, services often need to be deployed in a specific order and may require connection information from other services. The service dependency feature in azd enables you to:

1. Define which services depend on others
2. Ensure correct deployment order
3. Pass connection information between dependent services
4. Generate appropriate infrastructure code that reflects these dependencies

## Defining Service Dependencies

Service dependencies are defined in your `azure.yaml` file. You can define them manually or use the `azd gen deps` command:

### Using the Command Line

```bash
# Define that the 'api' service depends on the 'database' service
azd gen deps api database
```

This adds a `dependsOn` property to the `api` service in your `azure.yaml` file.

### Editing azure.yaml Directly

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

## Best Practices

1. **Keep Dependencies Simple** - Avoid complex dependency chains when possible
2. **Validate Early** - Run `azd infra synth` to validate dependencies before provisioning
3. **Review Generated Code** - Check the generated infrastructure code to ensure dependencies are reflected correctly
4. **Document Service Relationships** - Document your service dependencies as part of your project documentation

## Related Commands

* `azd gen deps` - Add service dependencies to your azure.yaml file
* `azd provision` - Provision resources with dependency validation
* `azd infra synth` - Generate infrastructure code with resolved dependencies
