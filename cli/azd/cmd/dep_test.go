// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd_test

import (
	"path/filepath"
	"testing"

	"github.com/azure/azure-dev/cli/azd/cmd"
	"github.com/azure/azure-dev/cli/azd/internal/tracing"
	"github.com/azure/azure-dev/cli/azd/test"
	"github.com/azure/azure-dev/cli/azd/test/mocks"
	"github.com/stretchr/testify/require"
)

func TestDepCommands(t *testing.T) {
	ctx := tracing.StartTest(t)
	testCtx := test.NewContext(t)

	projectPath := filepath.Join(testCtx.WorkingDir, "test-project")
	err := testCtx.PrepareProjectWithEnv(projectPath, "test-env")
	require.NoError(t, err)

	projectYaml := `
services:
  api:
    project: ./api
    language: js
    host: appservice
  web:
    project: ./web
    language: js
    host: appservice
  database:
    project: ./database
    language: sql
    host: azure-sql
`
	err = testCtx.WriteAzureYaml(projectPath, projectYaml)
	require.NoError(t, err)

	t.Run("DepAdd", func(t *testing.T) {
		// Test adding a dependency
		console := mocks.NewMockConsole()

		rootCmd := cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "database", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Verify the dependency was added
		projectConfig, err := testCtx.LoadProjectConfig(projectPath)
		require.NoError(t, err)
		require.Contains(t, projectConfig.Services, "api")
		require.Contains(t, projectConfig.Services["api"].DependsOn, "database")

		// Test adding a dependency with the force flag
		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "database", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Test adding another dependency to the same service
		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "web", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Verify both dependencies exist
		projectConfig, err = testCtx.LoadProjectConfig(projectPath)
		require.NoError(t, err)
		require.Contains(t, projectConfig.Services, "api")
		require.Contains(t, projectConfig.Services["api"].DependsOn, "database")
		require.Contains(t, projectConfig.Services["api"].DependsOn, "web")
	})

	t.Run("DepList", func(t *testing.T) {
		// First add some dependencies
		rootCmd := cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "database", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "web", "api", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Test listing all dependencies
		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "list", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Test listing dependencies for a specific service
		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "list", "api", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)
	})

	t.Run("DepRemove", func(t *testing.T) {
		// First add some dependencies
		rootCmd := cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "database", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "web", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Verify both dependencies exist
		projectConfig, err := testCtx.LoadProjectConfig(projectPath)
		require.NoError(t, err)
		require.Contains(t, projectConfig.Services, "api")
		require.Contains(t, projectConfig.Services["api"].DependsOn, "database")
		require.Contains(t, projectConfig.Services["api"].DependsOn, "web")

		// Test removing a dependency
		console := mocks.NewMockConsole()
		console.MockConfirm(true) // Simulate user confirmation

		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "remove", "api", "database", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Verify the dependency was removed
		projectConfig, err = testCtx.LoadProjectConfig(projectPath)
		require.NoError(t, err)
		require.Contains(t, projectConfig.Services, "api")
		require.NotContains(t, projectConfig.Services["api"].DependsOn, "database")
		require.Contains(t, projectConfig.Services["api"].DependsOn, "web")
	})

	t.Run("DepRemoveAll", func(t *testing.T) {
		// First add some dependencies
		rootCmd := cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "database", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "add", "api", "web", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Remove all remaining dependencies with force flag
		rootCmd = cmd.NewRootCmd(false, nil, nil)
		rootCmd.SetArgs([]string{"dep", "remove", "api", "web", "--force", "--cwd", projectPath})
		err = rootCmd.Execute()
		require.NoError(t, err)

		// Verify all dependencies were removed
		projectConfig, err := testCtx.LoadProjectConfig(projectPath)
		require.NoError(t, err)
		require.Contains(t, projectConfig.Services, "api")
		require.Empty(t, projectConfig.Services["api"].DependsOn)
	})
	// The legacy gen deps command test has been removed as the command is no longer supported
}
