// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

package cmd

import (
	"github.com/MakeNowJust/heredoc/v2"
	"github.com/azure/azure-dev/cli/azd/cmd/actions"
	"github.com/spf13/cobra"
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

	// Note: The 'gen deps' command has been removed and functionality moved to the 'dep' command group

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
