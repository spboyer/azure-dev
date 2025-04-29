// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"context"
	"fmt"

	"github.com/MakeNowJust/heredoc/v2"
	"github.com/azure/azure-dev/cli/azd/cmd/actions"
	"github.com/azure/azure-dev/cli/azd/internal"
	"github.com/azure/azure-dev/cli/azd/pkg/environment/azdcontext"
	"github.com/azure/azure-dev/cli/azd/pkg/input"
	"github.com/azure/azure-dev/cli/azd/pkg/lazy"
	"github.com/azure/azure-dev/cli/azd/pkg/output"
	"github.com/azure/azure-dev/cli/azd/pkg/project"
	"github.com/spf13/cobra"
	"github.com/spf13/pflag"
)

func genActions(root *actions.ActionDescriptor) *actions.ActionDescriptor {
	group := root.Add("gen", &actions.ActionDescriptorOptions{
		Command: &cobra.Command{
			Use:   "gen",
			Short: "Generate Azure Developer CLI configuration",
			Long: heredoc.Doc(`
				Generate Azure Developer CLI configuration files and settings.
				
				This command helps generate and update configuration settings for your Azure Developer CLI project.
			`),
		},
		HelpOptions: actions.ActionHelpOptions{
			Description: getCmdGenHelpDescription,
			Footer:      getCmdGenHelpFooter,
		},
		GroupingOptions: actions.CommandGroupOptions{
			RootLevelHelp: actions.CmdGroupConfig,
		},
	})

	group.Add("deps", &actions.ActionDescriptorOptions{
		Command: &cobra.Command{
			Use:   "deps [service] [dependent-service]",
			Short: "Define dependencies between services",
			Long: heredoc.Doc(`
				Define dependencies between services in your Azure Developer CLI project.
				
				This command updates your azure.yaml file to define that one service depends on another service.
				When you define service dependencies, those relationships can be used by the CLI to
				automatically order deployments correctly and to generate connection strings.
			`),
			Args: cobra.MaximumNArgs(2),
			Example: heredoc.Doc(`
				# Define 'api' service as dependent on 'database' service:
				azd gen deps api database
				
				# Interactive mode - will prompt for services and dependencies:
				azd gen deps
			`),
		},
		ActionResolver: newGenDepsAction,
		FlagsResolver:  newGenDepsFlags,
		HelpOptions: actions.ActionHelpOptions{
			Description: getCmdGenDepsHelpDescription,
			Footer:      getCmdGenDepsHelpFooter,
		},
	})

	return group
}

func getCmdGenHelpDescription(*cobra.Command) string {
	return heredoc.Doc(`
		Generate Azure Developer CLI configuration files and settings.
	`)
}

func getCmdGenHelpFooter(*cobra.Command) string {
	return ""
}

func getCmdGenDepsHelpDescription(*cobra.Command) string {
	return heredoc.Doc(`
		Define dependencies between services in your Azure Developer CLI project.
	`)
}

func getCmdGenDepsHelpFooter(*cobra.Command) string {
	return heredoc.Doc(`
		Service dependencies are used to:
		
		1. Order the deployment of services correctly
		2. Generate connection strings between services
		3. Pass environment variables from one service to another
		
		When one service depends on another, the dependency will be deployed first,
		and its connection information can be made available to dependent services.
	`)
}

type genDepsFlags struct {
	force bool
	internal.EnvFlag
	global *internal.GlobalCommandOptions
}

func newGenDepsFlags(cmd *cobra.Command, global *internal.GlobalCommandOptions) *genDepsFlags {
	flags := &genDepsFlags{}
	flags.Bind(cmd.Flags(), global)

	return flags
}

func (f *genDepsFlags) Bind(local *pflag.FlagSet, global *internal.GlobalCommandOptions) {
	f.EnvFlag.Bind(local, global)
	f.global = global
	local.BoolVar(
		&f.force,
		"force",
		false,
		"Force overwrite of existing dependencies",
	)
}

type genDepsAction struct {
	flags          *genDepsFlags
	args           []string
	console        input.Console
	lazyAzdCtx     *lazy.Lazy[*azdcontext.AzdContext]
	projectManager project.ProjectManager
	formatter      output.Formatter
}

func newGenDepsAction(
	flags *genDepsFlags,
	args []string,
	console input.Console,
	lazyAzdCtx *lazy.Lazy[*azdcontext.AzdContext],
	projectManager project.ProjectManager,
	formatter output.Formatter,
) actions.Action {
	return &genDepsAction{
		flags:          flags,
		args:           args,
		console:        console,
		lazyAzdCtx:     lazyAzdCtx,
		projectManager: projectManager,
		formatter:      formatter,
	}
}

func (a *genDepsAction) Run(ctx context.Context) (*actions.ActionResult, error) {
	azdCtx, err := a.lazyAzdCtx.GetValue()
	if err != nil {
		return nil, err
	}
	// Load project config
	projectConfig, err := project.Load(ctx, azdCtx.ProjectPath())
	if err != nil {
		return nil, fmt.Errorf("failed to load project configuration: %w", err)
	}

	// If no services are defined, show an error
	if len(projectConfig.Services) == 0 {
		return nil, fmt.Errorf("no services defined in project. Add services to azure.yaml first")
	}

	var serviceNames []string
	for name := range projectConfig.Services {
		serviceNames = append(serviceNames, name)
	}

	var srcService, destService string

	// Handle command arguments or prompt for inputs
	if len(a.args) >= 2 {
		srcService = a.args[0]
		destService = a.args[1]
	} else { // Prompt user to select the dependent service
		if len(a.args) == 1 {
			srcService = a.args[0]
		} else {
			// Prompt for the dependent service
			srcServiceIndex, err := a.console.Select(ctx, input.ConsoleOptions{
				Message: "Select a service",
				Options: serviceNames,
			})
			if err != nil {
				return nil, err
			}
			srcService = serviceNames[srcServiceIndex]
		}

		// Validate the service name
		if _, exists := projectConfig.Services[srcService]; !exists {
			return nil, fmt.Errorf("service '%s' not found in project", srcService)
		}

		// Create a list of possible dependencies (all services except the source)
		var possibleDeps []string
		for name := range projectConfig.Services {
			if name != srcService {
				possibleDeps = append(possibleDeps, name)
			}
		}

		if len(possibleDeps) == 0 {
			return nil, fmt.Errorf("no other services available to depend on. Add more services first")
		}
		// Prompt for the dependency
		destServiceIndex, err := a.console.Select(ctx, input.ConsoleOptions{
			Message: fmt.Sprintf("Select a service that %s depends on", srcService),
			Options: possibleDeps,
		})
		if err != nil {
			return nil, err
		}
		destService = possibleDeps[destServiceIndex]
	}

	// Validate both service names
	if _, exists := projectConfig.Services[srcService]; !exists {
		return nil, fmt.Errorf("service '%s' not found in project", srcService)
	}
	if _, exists := projectConfig.Services[destService]; !exists {
		return nil, fmt.Errorf("service '%s' not found in project", destService)
	}

	// Check if the dependency relationship already exists
	existingDeps := projectConfig.Services[srcService].DependsOn
	for _, dep := range existingDeps {
		if dep == destService {
			if !a.flags.force {
				return nil, fmt.Errorf("service '%s' already depends on '%s'. Use --force to overwrite", srcService, destService)
			}
			// If force is specified, we'll just re-add the dependency (which is a no-op)
		}
	}

	// Add dependency relationship
	if projectConfig.Services[srcService].DependsOn == nil {
		projectConfig.Services[srcService].DependsOn = []string{destService}
	} else {
		// Check for duplicates
		hasDep := false
		for _, dep := range projectConfig.Services[srcService].DependsOn {
			if dep == destService {
				hasDep = true
				break
			}
		}
		if !hasDep {
			projectConfig.Services[srcService].DependsOn = append(projectConfig.Services[srcService].DependsOn, destService)
		}
	}
	// Save the project configuration
	err = project.Save(ctx, projectConfig, azdCtx.ProjectPath())
	if err != nil {
		return nil, fmt.Errorf("failed to save project configuration: %w", err)
	} // Success message
	a.console.Message(ctx, fmt.Sprintf("Dependency created: '%s' now depends on '%s'", srcService, destService))

	return &actions.ActionResult{
		Message: &actions.ResultMessage{
			Header: fmt.Sprintf("Dependency created: '%s' now depends on '%s'", srcService, destService),
		},
		Data: map[string]interface{}{
			"service":      srcService,
			"dependsOn":    destService,
			"dependencies": projectConfig.Services[srcService].DependsOn,
		},
	}, nil
}
