// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"context"
	"fmt"
	"io"
	"sort"

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

func depActions(root *actions.ActionDescriptor) *actions.ActionDescriptor {
	group := root.Add("dep", &actions.ActionDescriptorOptions{
		Command: &cobra.Command{
			Use:   "dep",
			Short: "Manage service dependencies",
			Long: heredoc.Doc(`
				Manage dependencies between services in your Azure Developer CLI project.
				
				This command helps you add, list, and remove dependencies between services
				in your Azure Developer CLI project.
			`),
		},
		HelpOptions: actions.ActionHelpOptions{
			Description: getCmdDepHelpDescription,
			Footer:      getCmdDepHelpFooter,
		},
		GroupingOptions: actions.CommandGroupOptions{
			RootLevelHelp: actions.CmdGroupConfig,
		},
	})

	group.Add("add", &actions.ActionDescriptorOptions{
		Command: &cobra.Command{
			Use:   "add [service] [dependent-service]",
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
				azd dep add api database
				
				# Interactive mode - will prompt for services and dependencies:
				azd dep add
			`),
		},
		ActionResolver: newDepAddAction,
		FlagsResolver:  newDepAddFlags,
		HelpOptions: actions.ActionHelpOptions{
			Description: getCmdDepAddHelpDescription,
			Footer:      getCmdDepAddHelpFooter,
		},
	})

	group.Add("list", &actions.ActionDescriptorOptions{
		Command: &cobra.Command{
			Use:   "list [service-name]",
			Short: "List dependencies between services",
			Long: heredoc.Doc(`
				List dependencies between services in your Azure Developer CLI project.
				
				This command displays the dependency relationships defined in your azure.yaml file.
				If a service name is provided, it will only show dependencies for that service.
				Otherwise, it will show dependencies for all services.
			`),
			Args: cobra.MaximumNArgs(1),
			Example: heredoc.Doc(`
				# List all service dependencies:
				azd dep list
				
				# List dependencies for a specific service:
				azd dep list api
			`),
		},
		ActionResolver: newDepListAction,
		FlagsResolver:  newDepListFlags,
		OutputFormats:  []output.Format{output.TableFormat, output.JsonFormat},
		DefaultFormat:  output.TableFormat,
		HelpOptions: actions.ActionHelpOptions{
			Description: getCmdDepListHelpDescription,
			Footer:      getCmdDepListHelpFooter,
		},
	})

	group.Add("remove", &actions.ActionDescriptorOptions{
		Command: &cobra.Command{
			Use:   "remove [service] [dependent-service]",
			Short: "Remove dependencies between services",
			Long: heredoc.Doc(`
				Remove dependencies between services in your Azure Developer CLI project.
				
				This command updates your azure.yaml file to remove a dependency relationship
				between services.
			`),
			Args: cobra.MaximumNArgs(2),
			Example: heredoc.Doc(`
				# Remove 'database' dependency from 'api' service:
				azd dep remove api database
				
				# Interactive mode - will prompt for services and dependencies:
				azd dep remove
			`),
		},
		ActionResolver: newDepRemoveAction,
		FlagsResolver:  newDepRemoveFlags,
		HelpOptions: actions.ActionHelpOptions{
			Description: getCmdDepRemoveHelpDescription,
			Footer:      getCmdDepRemoveHelpFooter,
		},
	})

	return group
}

func getCmdDepHelpDescription(*cobra.Command) string {
	return heredoc.Doc(`
		Manage dependencies between services in your Azure Developer CLI project.
	`)
}

func getCmdDepHelpFooter(*cobra.Command) string {
	return heredoc.Doc(`
		Service dependencies are used to:
		
		1. Order the deployment of services correctly
		2. Generate connection strings between services
		3. Pass environment variables from one service to another
		
		When one service depends on another, the dependency will be deployed first,
		and its connection information can be made available to dependent services.
	`)
}

func getCmdDepAddHelpDescription(*cobra.Command) string {
	return heredoc.Doc(`
		Define dependencies between services in your Azure Developer CLI project.
	`)
}

func getCmdDepAddHelpFooter(*cobra.Command) string {
	return heredoc.Doc(`
		Service dependencies are used to:
		
		1. Order the deployment of services correctly
		2. Generate connection strings between services
		3. Pass environment variables from one service to another
		
		When one service depends on another, the dependency will be deployed first,
		and its connection information can be made available to dependent services.
	`)
}

func getCmdDepListHelpDescription(*cobra.Command) string {
	return heredoc.Doc(`
		List dependencies between services in your Azure Developer CLI project.
	`)
}

func getCmdDepListHelpFooter(*cobra.Command) string {
	return heredoc.Doc(`
		This command shows how services depend on each other in your project.
		Understanding these relationships helps visualize the deployment order
		and connection flow in your application.
	`)
}

func getCmdDepRemoveHelpDescription(*cobra.Command) string {
	return heredoc.Doc(`
		Remove dependencies between services in your Azure Developer CLI project.
	`)
}

func getCmdDepRemoveHelpFooter(*cobra.Command) string {
	return heredoc.Doc(`
		Removing service dependencies affects:
		
		1. The order in which services are deployed
		2. Connection string generation between services
		3. Environment variables shared between services
		
		Make sure removing dependencies won't break your application's functionality.
	`)
}

// Dependencies flags and actions - Add

type depAddFlags struct {
	force bool
	internal.EnvFlag
	global *internal.GlobalCommandOptions
}

func newDepAddFlags(cmd *cobra.Command, global *internal.GlobalCommandOptions) *depAddFlags {
	flags := &depAddFlags{}
	flags.Bind(cmd.Flags(), global)

	return flags
}

func (f *depAddFlags) Bind(local *pflag.FlagSet, global *internal.GlobalCommandOptions) {
	f.EnvFlag.Bind(local, global)
	f.global = global
	local.BoolVar(
		&f.force,
		"force",
		false,
		"Force overwrite of existing dependencies",
	)
}

type depAddAction struct {
	flags          *depAddFlags
	args           []string
	console        input.Console
	lazyAzdCtx     *lazy.Lazy[*azdcontext.AzdContext]
	projectManager project.ProjectManager
	formatter      output.Formatter
	writer         io.Writer
}

func newDepAddAction(
	flags *depAddFlags,
	args []string,
	console input.Console,
	lazyAzdCtx *lazy.Lazy[*azdcontext.AzdContext],
	projectManager project.ProjectManager,
	formatter output.Formatter,
) actions.Action {
	// Use writer from console
	writer := console.GetWriter()

	return &depAddAction{
		flags:          flags,
		args:           args,
		console:        console,
		lazyAzdCtx:     lazyAzdCtx,
		projectManager: projectManager,
		formatter:      formatter,
		writer:         writer,
	}
}

func (a *depAddAction) Run(ctx context.Context) (*actions.ActionResult, error) {
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
	}

	// Success message
	a.console.Message(ctx, fmt.Sprintf("Dependency created: '%s' now depends on '%s'", srcService, destService))

	return &actions.ActionResult{
		Message: &actions.ResultMessage{
			Header: fmt.Sprintf("Dependency created: '%s' now depends on '%s'", srcService, destService),
		},
	}, nil
}

// Dependencies flags and actions - List

type depListFlags struct {
	internal.EnvFlag
	global *internal.GlobalCommandOptions
}

func newDepListFlags(cmd *cobra.Command, global *internal.GlobalCommandOptions) *depListFlags {
	flags := &depListFlags{}
	flags.Bind(cmd.Flags(), global)

	return flags
}

func (f *depListFlags) Bind(local *pflag.FlagSet, global *internal.GlobalCommandOptions) {
	f.EnvFlag.Bind(local, global)
	f.global = global
}

// ServiceDependencyView represents a service dependency for display purposes
type ServiceDependencyView struct {
	Service    string   `json:"service"`
	DependsOn  []string `json:"dependsOn,omitempty"`
	RequiredBy []string `json:"requiredBy,omitempty"`
}

type depListAction struct {
	flags          *depListFlags
	args           []string
	console        input.Console
	lazyAzdCtx     *lazy.Lazy[*azdcontext.AzdContext]
	projectManager project.ProjectManager
	formatter      output.Formatter
	writer         io.Writer
}

func newDepListAction(
	flags *depListFlags,
	args []string,
	console input.Console,
	lazyAzdCtx *lazy.Lazy[*azdcontext.AzdContext],
	projectManager project.ProjectManager,
	formatter output.Formatter,
) actions.Action {
	// Use os.Stdout as the default writer
	writer := console.GetWriter()

	return &depListAction{
		flags:          flags,
		args:           args,
		console:        console,
		lazyAzdCtx:     lazyAzdCtx,
		projectManager: projectManager,
		formatter:      formatter,
		writer:         writer,
	}
}

func (a *depListAction) Run(ctx context.Context) (*actions.ActionResult, error) {
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

	// Create a map of service dependencies
	serviceViews := make(map[string]*ServiceDependencyView)

	// Initialize the service views
	for name := range projectConfig.Services {
		serviceViews[name] = &ServiceDependencyView{
			Service:    name,
			DependsOn:  []string{},
			RequiredBy: []string{},
		}
	}

	// Populate dependencies and required-by relationships
	for serviceName, serviceConfig := range projectConfig.Services {
		// If the service has dependencies, add them to the service view
		if serviceConfig.DependsOn != nil && len(serviceConfig.DependsOn) > 0 {
			serviceViews[serviceName].DependsOn = serviceConfig.DependsOn

			// Add the "required by" relationship to the dependent services
			for _, dependencyName := range serviceConfig.DependsOn {
				if depView, exists := serviceViews[dependencyName]; exists {
					depView.RequiredBy = append(depView.RequiredBy, serviceName)
				}
			}
		}
	}

	// Convert to slice for display and sort alphabetically by service name
	var result []*ServiceDependencyView

	// If a service name was provided, just show that one
	if len(a.args) == 1 {
		serviceName := a.args[0]
		if view, exists := serviceViews[serviceName]; exists {
			result = append(result, view)
		} else {
			return nil, fmt.Errorf("service '%s' not found in project", serviceName)
		}
	} else {
		// Otherwise show all services
		for _, view := range serviceViews {
			result = append(result, view)
		}

		// Sort for consistent display
		sort.Slice(result, func(i, j int) bool {
			return result[i].Service < result[j].Service
		})
	}

	// Format the dependencies for display
	for _, view := range result {
		// Sort the lists for consistent display
		sort.Strings(view.DependsOn)
		sort.Strings(view.RequiredBy)
	}

	if a.formatter.Kind() == output.TableFormat {
		columns := []output.Column{
			{
				Heading:       "SERVICE",
				ValueTemplate: "{{.Service}}",
			},
			{
				Heading:       "DEPENDS ON",
				ValueTemplate: "{{if .DependsOn}}{{.DependsOn}}{{else}}-{{end}}",
			},
			{
				Heading:       "REQUIRED BY",
				ValueTemplate: "{{if .RequiredBy}}{{.RequiredBy}}{{else}}-{{end}}",
			},
		}

		err = a.formatter.Format(result, a.writer, output.TableFormatterOptions{
			Columns: columns,
		})

		if err != nil {
			return nil, err
		}
	} else {
		if err := a.formatter.Format(result, a.writer, nil); err != nil {
			return nil, err
		}
	}

	return &actions.ActionResult{}, nil
}

// Dependencies flags and actions - Remove

type depRemoveFlags struct {
	force bool
	internal.EnvFlag
	global *internal.GlobalCommandOptions
}

func newDepRemoveFlags(cmd *cobra.Command, global *internal.GlobalCommandOptions) *depRemoveFlags {
	flags := &depRemoveFlags{}
	flags.Bind(cmd.Flags(), global)

	return flags
}

func (f *depRemoveFlags) Bind(local *pflag.FlagSet, global *internal.GlobalCommandOptions) {
	f.EnvFlag.Bind(local, global)
	f.global = global
	local.BoolVar(
		&f.force,
		"force",
		false,
		"Remove dependency without confirmation prompt",
	)
}

type depRemoveAction struct {
	flags          *depRemoveFlags
	args           []string
	console        input.Console
	lazyAzdCtx     *lazy.Lazy[*azdcontext.AzdContext]
	projectManager project.ProjectManager
	formatter      output.Formatter
	writer         io.Writer
}

func newDepRemoveAction(
	flags *depRemoveFlags,
	args []string,
	console input.Console,
	lazyAzdCtx *lazy.Lazy[*azdcontext.AzdContext],
	projectManager project.ProjectManager,
	formatter output.Formatter,
) actions.Action {
	// Use writer from console
	writer := console.GetWriter()

	return &depRemoveAction{
		flags:          flags,
		args:           args,
		console:        console,
		lazyAzdCtx:     lazyAzdCtx,
		projectManager: projectManager,
		formatter:      formatter,
		writer:         writer,
	}
}

func (a *depRemoveAction) Run(ctx context.Context) (*actions.ActionResult, error) {
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
	} else {
		if len(a.args) == 1 {
			srcService = a.args[0]
		} else {
			// Prompt for the source service
			srcServiceIndex, err := a.console.Select(ctx, input.ConsoleOptions{
				Message: "Select a service to remove dependencies from",
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

		// Get existing dependencies for the source service
		existingDeps := projectConfig.Services[srcService].DependsOn
		if existingDeps == nil || len(existingDeps) == 0 {
			return nil, fmt.Errorf("service '%s' has no dependencies to remove", srcService)
		}

		// Prompt for the dependency to remove
		destServiceIndex, err := a.console.Select(ctx, input.ConsoleOptions{
			Message: fmt.Sprintf("Select a dependency to remove from %s", srcService),
			Options: existingDeps,
		})
		if err != nil {
			return nil, err
		}
		destService = existingDeps[destServiceIndex]
	}

	// Validate both service names
	if _, exists := projectConfig.Services[srcService]; !exists {
		return nil, fmt.Errorf("service '%s' not found in project", srcService)
	}

	// Check if the dependency relationship exists
	existingDeps := projectConfig.Services[srcService].DependsOn
	if existingDeps == nil || len(existingDeps) == 0 {
		return nil, fmt.Errorf("service '%s' has no dependencies to remove", srcService)
	}

	// Find the dependency in the list
	foundIndex := -1
	for i, dep := range existingDeps {
		if dep == destService {
			foundIndex = i
			break
		}
	}

	if foundIndex == -1 {
		return nil, fmt.Errorf("service '%s' does not depend on '%s'", srcService, destService)
	}

	// Ask for confirmation unless force is specified
	if !a.flags.force {
		confirmed, err := a.console.Confirm(ctx, input.ConsoleOptions{
			Message:      fmt.Sprintf("Are you sure you want to remove the dependency from '%s' to '%s'?", srcService, destService),
			DefaultValue: false,
		})
		if err != nil {
			return nil, err
		}
		if !confirmed {
			return &actions.ActionResult{
				Message: &actions.ResultMessage{
					Header: "Dependency removal cancelled",
				},
			}, nil
		}
	}

	// Remove the dependency
	projectConfig.Services[srcService].DependsOn = append(existingDeps[:foundIndex], existingDeps[foundIndex+1:]...)

	// If no dependencies left, set to nil
	if len(projectConfig.Services[srcService].DependsOn) == 0 {
		projectConfig.Services[srcService].DependsOn = nil
	}

	// Save the project configuration
	err = project.Save(ctx, projectConfig, azdCtx.ProjectPath())
	if err != nil {
		return nil, fmt.Errorf("failed to save project configuration: %w", err)
	}

	// Success message
	a.console.Message(ctx, fmt.Sprintf("Dependency removed: '%s' no longer depends on '%s'", srcService, destService))

	return &actions.ActionResult{
		Message: &actions.ResultMessage{
			Header: fmt.Sprintf("Dependency removed: '%s' no longer depends on '%s'", srcService, destService),
		},
	}, nil
}
