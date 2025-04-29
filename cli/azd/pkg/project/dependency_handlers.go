// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package project

import (
	"fmt"
	"log"
	"strings"
)

// DependencyHandler defines the interface for IaC-specific dependency handlers
type DependencyHandler interface {
	// ProcessDependencies adds dependencies to the infrastructure code
	ProcessDependencies(config *ProjectConfig) error

	// FormatDependsOnExpression returns a string representation of the dependsOn expression
	// for the specified dependencies in the IaC-specific syntax
	FormatDependsOnExpression(service string, dependencies []string) string
}

// BicepDependencyHandler handles Bicep-specific dependency conversions
type BicepDependencyHandler struct{}

// ProcessDependencies processes service dependencies for Bicep infrastructure
func (h *BicepDependencyHandler) ProcessDependencies(config *ProjectConfig) error {
	dependencyCount := 0
	for _, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil {
			dependencyCount += len(serviceConfig.DependsOn)
		}
	}

	if dependencyCount == 0 {
		return nil // No dependencies to process
	}

	log.Printf("Processing %d service dependencies for Bicep infrastructure", dependencyCount)

	// For each service with dependencies, log how they would be represented in Bicep
	for serviceName, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil && len(serviceConfig.DependsOn) > 0 {
			expr := h.FormatDependsOnExpression(serviceName, serviceConfig.DependsOn)
			log.Printf("Bicep expression for %s: %s", serviceName, expr)
		}
	}

	return nil
}

// FormatDependsOnExpression formats a Bicep dependsOn expression
func (h *BicepDependencyHandler) FormatDependsOnExpression(service string, dependencies []string) string {
	if len(dependencies) == 0 {
		return ""
	}

	var dependsOnItems []string
	for _, dep := range dependencies {
		// Format as reference to another resource
		dependsOnItems = append(dependsOnItems, fmt.Sprintf("resource_%s.id", dep))
	}

	return fmt.Sprintf("dependsOn: [%s]", strings.Join(dependsOnItems, ", "))
}

// TerraformDependencyHandler handles Terraform-specific dependency conversions
type TerraformDependencyHandler struct{}

// ProcessDependencies processes service dependencies for Terraform infrastructure
func (h *TerraformDependencyHandler) ProcessDependencies(config *ProjectConfig) error {
	dependencyCount := 0
	for _, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil {
			dependencyCount += len(serviceConfig.DependsOn)
		}
	}

	if dependencyCount == 0 {
		return nil // No dependencies to process
	}

	log.Printf("Processing %d service dependencies for Terraform infrastructure", dependencyCount)

	// For each service with dependencies, log how they would be represented in Terraform
	for serviceName, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil && len(serviceConfig.DependsOn) > 0 {
			expr := h.FormatDependsOnExpression(serviceName, serviceConfig.DependsOn)
			log.Printf("Terraform expression for %s: %s", serviceName, expr)
		}
	}

	return nil
}

// FormatDependsOnExpression formats a Terraform depends_on expression
func (h *TerraformDependencyHandler) FormatDependsOnExpression(service string, dependencies []string) string {
	if len(dependencies) == 0 {
		return ""
	}

	var dependsOnItems []string
	for _, dep := range dependencies {
		// Format as reference to another module/resource
		dependsOnItems = append(dependsOnItems, fmt.Sprintf("module.%s", dep))
	}

	return fmt.Sprintf("depends_on = [%s]", strings.Join(dependsOnItems, ", "))
}

// GetDependencyHandlerForProvider returns the appropriate dependency handler for a given IaC provider
func GetDependencyHandlerForProvider(provider string) DependencyHandler {
	switch strings.ToLower(provider) {
	case "terraform":
		return &TerraformDependencyHandler{}
	case "bicep", "":
		return &BicepDependencyHandler{}
	default:
		// Default to Bicep
		log.Printf("Unknown infrastructure provider: %s, using bicep handler", provider)
		return &BicepDependencyHandler{}
	}
}

// ProcessDependenciesForProvider processes dependencies for the specified IaC provider
func ProcessDependenciesForProvider(config *ProjectConfig, provider string) error {
	handler := GetDependencyHandlerForProvider(provider)
	return handler.ProcessDependencies(config)
}
