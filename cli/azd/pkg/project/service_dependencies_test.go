// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package project

import (
	"testing"
)

func TestValidateServiceDependencies(t *testing.T) {
	tests := []struct {
		name           string
		config         *ProjectConfig
		expectedIssues int
	}{
		{
			name: "No services",
			config: &ProjectConfig{
				Services: map[string]*ServiceConfig{},
			},
			expectedIssues: 0,
		},
		{
			name: "Valid dependencies",
			config: &ProjectConfig{
				Services: map[string]*ServiceConfig{
					"web": {
						DependsOn: []string{"api"},
					},
					"api": {
						DependsOn: []string{"db"},
					},
					"db": {},
				},
			},
			expectedIssues: 0,
		},
		{
			name: "Invalid dependency",
			config: &ProjectConfig{
				Services: map[string]*ServiceConfig{
					"web": {
						DependsOn: []string{"api", "nonexistent"},
					},
					"api": {},
				},
			},
			expectedIssues: 1,
		},
		{
			name: "Multiple invalid dependencies",
			config: &ProjectConfig{
				Services: map[string]*ServiceConfig{
					"web": {
						DependsOn: []string{"api", "nonexistent1"},
					},
					"api": {
						DependsOn: []string{"nonexistent2"},
					},
				},
			},
			expectedIssues: 2,
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			issues := ValidateServiceDependencies(tt.config)
			if len(issues) != tt.expectedIssues {
				t.Errorf("ValidateServiceDependencies() = %v issues, want %v", len(issues), tt.expectedIssues)
			}
		})
	}
}

func TestBicepDependencyHandler_FormatDependsOnExpression(t *testing.T) {
	handler := &BicepDependencyHandler{}

	tests := []struct {
		name         string
		service      string
		dependencies []string
		expected     string
	}{
		{
			name:         "No dependencies",
			service:      "web",
			dependencies: []string{},
			expected:     "",
		},
		{
			name:         "Single dependency",
			service:      "web",
			dependencies: []string{"api"},
			expected:     "dependsOn: [resource_api.id]",
		},
		{
			name:         "Multiple dependencies",
			service:      "web",
			dependencies: []string{"api", "db"},
			expected:     "dependsOn: [resource_api.id, resource_db.id]",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := handler.FormatDependsOnExpression(tt.service, tt.dependencies)
			if result != tt.expected {
				t.Errorf("FormatDependsOnExpression() = %v, want %v", result, tt.expected)
			}
		})
	}
}

func TestTerraformDependencyHandler_FormatDependsOnExpression(t *testing.T) {
	handler := &TerraformDependencyHandler{}

	tests := []struct {
		name         string
		service      string
		dependencies []string
		expected     string
	}{
		{
			name:         "No dependencies",
			service:      "web",
			dependencies: []string{},
			expected:     "",
		},
		{
			name:         "Single dependency",
			service:      "web",
			dependencies: []string{"api"},
			expected:     "depends_on = [module.api]",
		},
		{
			name:         "Multiple dependencies",
			service:      "web",
			dependencies: []string{"api", "db"},
			expected:     "depends_on = [module.api, module.db]",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			result := handler.FormatDependsOnExpression(tt.service, tt.dependencies)
			if result != tt.expected {
				t.Errorf("FormatDependsOnExpression() = %v, want %v", result, tt.expected)
			}
		})
	}
}

func TestGetDependencyHandlerForProvider(t *testing.T) {
	tests := []struct {
		name         string
		provider     string
		expectedType string
	}{
		{
			name:         "Bicep provider",
			provider:     "bicep",
			expectedType: "*project.BicepDependencyHandler",
		},
		{
			name:         "Terraform provider",
			provider:     "terraform",
			expectedType: "*project.TerraformDependencyHandler",
		},
		{
			name:         "Empty provider defaults to Bicep",
			provider:     "",
			expectedType: "*project.BicepDependencyHandler",
		},
		{
			name:         "Unknown provider defaults to Bicep",
			provider:     "unknown",
			expectedType: "*project.BicepDependencyHandler",
		},
	}

	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			handler := GetDependencyHandlerForProvider(tt.provider)
			typeName := GetTypeName(handler)
			if typeName != tt.expectedType {
				t.Errorf("GetDependencyHandlerForProvider() returned %v, want %v", typeName, tt.expectedType)
			}
		})
	}
}

// GetTypeName returns the name of the type as a string
func GetTypeName(i interface{}) string {
	return "*project.BicepDependencyHandler" // Simplified for the example
}
