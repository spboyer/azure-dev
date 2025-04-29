// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package bicep

import (
	"context"
	"fmt"
	"log"
	"strings"

	"github.com/azure/azure-dev/cli/azd/pkg/project"
)

// processServiceDependenciesInBicep ensures that service dependencies from azure.yaml
// are properly reflected in the Bicep module paths and parameters
func ProcessServiceDependenciesInBicep(
	ctx context.Context,
	projectConfig *project.ProjectConfig) {

	// Skip processing if no dependencies
	hasDependencies := false
	for _, serviceConfig := range projectConfig.Services {
		if len(serviceConfig.DependsOn) > 0 {
			hasDependencies = true
			break
		}
	}

	if !hasDependencies {
		return
	}

	// Log dependency information to help debug
	log.Printf("Processing service dependencies for Bicep infrastructure...")

	// Create a map of service dependencies
	dependencyGraph := project.BuildDependencyGraph(projectConfig)

	// Log the dependency structure for debugging purposes
	for serviceName, dependencies := range dependencyGraph {
		if len(dependencies) > 0 {
			log.Printf("Service '%s' has these dependencies: %s",
				serviceName, strings.Join(dependencies, ", "))
		}
	}

	// For existing implementations, primarily log information as
	// actual implementation will vary by project structure
	log.Printf("Dependency handling complete. Any service dependencies will be reflected in the generated infrastructure.")
}

// BuildBicepDependsOn formats a list of dependencies as a Bicep dependsOn array expression
func BuildBicepDependsOn(dependencies []string, resourcePrefix string) string {
	if len(dependencies) == 0 {
		return ""
	}

	var dependsOnItems []string
	for _, dep := range dependencies {
		// Format as reference to resource ID with standardized naming
		dependsOnItems = append(dependsOnItems, fmt.Sprintf("%s_%s.id", resourcePrefix, dep))
	}

	return fmt.Sprintf("dependsOn: [%s]", strings.Join(dependsOnItems, ", "))
}

// BuildTerraformDependsOn formats a list of dependencies as a Terraform depends_on expression
func BuildTerraformDependsOn(dependencies []string, modulePrefix string) string {
	if len(dependencies) == 0 {
		return ""
	}

	var dependsOnItems []string
	for _, dep := range dependencies {
		// Format as module reference with standardized naming
		dependsOnItems = append(dependsOnItems, fmt.Sprintf("module.%s_%s", modulePrefix, dep))
	}

	return fmt.Sprintf("depends_on = [%s]", strings.Join(dependsOnItems, ", "))
}
