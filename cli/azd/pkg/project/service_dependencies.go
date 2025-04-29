// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package project

import (
	"fmt"
	"log"
	"strings"
)

// ValidateServiceDependencies checks that all service dependencies declared in azure.yaml exist
// and returns any issues found
func ValidateServiceDependencies(config *ProjectConfig) []string {
	var issues []string

	if len(config.Services) == 0 {
		return issues // No services to validate
	}

	// First, build a map of all available services
	availableServices := make(map[string]bool)
	for serviceName := range config.Services {
		availableServices[serviceName] = true
	}

	// Then check each dependency
	for serviceName, serviceConfig := range config.Services {
		if serviceConfig.DependsOn == nil || len(serviceConfig.DependsOn) == 0 {
			continue // No dependencies to verify
		}

		for _, dependencyName := range serviceConfig.DependsOn {
			if !availableServices[dependencyName] {
				issues = append(
					issues,
					fmt.Sprintf("Service '%s' depends on '%s', but this service doesn't exist in azure.yaml",
						serviceName, dependencyName))
			}
		}
	}

	return issues
}

// DetectCyclicDependencies checks for cyclic dependencies in the service dependency graph
// and returns any cycles found as strings
func DetectCyclicDependencies(config *ProjectConfig) []string {
	var cycles []string
	visited := make(map[string]bool)
	path := make(map[string]bool)

	// Helper function for DFS traversal to detect cycles
	var dfs func(string, []string) bool
	dfs = func(current string, stack []string) bool {
		if !visited[current] {
			visited[current] = true
			path[current] = true

			stack = append(stack, current)

			// Check all dependencies of the current service
			if service, exists := config.Services[current]; exists && service.DependsOn != nil {
				for _, dep := range service.DependsOn {
					if !visited[dep] {
						if dfs(dep, stack) {
							return true
						}
					} else if path[dep] {
						// Found a cycle
						cycleStart := -1
						for i, v := range stack {
							if v == dep {
								cycleStart = i
								break
							}
						}

						if cycleStart != -1 {
							cycle := append(stack[cycleStart:], dep)
							cycles = append(cycles, fmt.Sprintf("Cyclic dependency detected: %s",
								strings.Join(cycle, " -> ")))
							return true
						}
					}
				}
			}

			path[current] = false
			stack = stack[:len(stack)-1]
		}
		return false
	}

	// Run DFS from each service
	for service := range config.Services {
		if !visited[service] {
			dfs(service, []string{})
		}
	}

	return cycles
}

// LogServiceDependencies logs information about service dependencies
func LogServiceDependencies(config *ProjectConfig) {
	// Count dependencies
	dependencyCount := 0
	for _, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil {
			dependencyCount += len(serviceConfig.DependsOn)
		}
	}

	if dependencyCount == 0 {
		return // No dependencies to log
	}

	log.Printf("Found %d service dependencies in azure.yaml", dependencyCount)

	// Log each service's dependencies
	for serviceName, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil && len(serviceConfig.DependsOn) > 0 {
			log.Printf("Service '%s' depends on: %s",
				serviceName, strings.Join(serviceConfig.DependsOn, ", "))
		}
	}

	// Check for cyclic dependencies
	if cycles := DetectCyclicDependencies(config); len(cycles) > 0 {
		log.Printf("WARNING: Cyclic dependencies detected in service configuration:")
		for _, cycle := range cycles {
			log.Printf("  - %s", cycle)
		}
	}
}

// BuildDependencyGraph builds a directed graph of service dependencies
// and returns a map where the key is a service and the value is a slice of services it depends on
func BuildDependencyGraph(config *ProjectConfig) map[string][]string {
	dependencyGraph := make(map[string][]string)

	for serviceName, serviceConfig := range config.Services {
		if serviceConfig.DependsOn != nil && len(serviceConfig.DependsOn) > 0 {
			dependencyGraph[serviceName] = serviceConfig.DependsOn
		} else {
			// Ensure every service is in the graph even if it has no dependencies
			dependencyGraph[serviceName] = []string{}
		}
	}

	return dependencyGraph
}
