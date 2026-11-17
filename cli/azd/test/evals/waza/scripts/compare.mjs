// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");

export function summarizeExperiments(manifest, runs) {
    return manifest.experiments.map((experiment) => {
        const configurations = manifest.suites.filter((suite) => suite.experiment === experiment.id);
        const sources = [...new Set(configurations.map((suite) => suite.source))];
        const baselineVariant = `${experiment.baseline.skills}--${experiment.baseline.model}`;
        return {
            experiment: experiment.id,
            name: experiment.name,
            baseline: experiment.baseline,
            interpretation: "Descriptive paired-task deltas only; not a significance test or a claim that one trial is reliable.",
            suites: sources.map((source) => {
                const expected = configurations.filter((suite) => suite.source === source);
                const records = expected.map((suite) => {
                    const run = runs.find((record) => record.suite === suite.name);
                    if (run && run.source_sha256 !== suite.source_sha256) {
                        throw new Error(`Source changed since execution: ${suite.name}`);
                    }
                    return { suite, run };
                });
                const baseline = records.find(({ suite }) => suite.variant === baselineVariant);
                if (!baseline) {
                    throw new Error(`Missing declared baseline: ${experiment.id}/${source}/${baselineVariant}`);
                }
                const baselineComplete = baseline.run?.compatibility?.complete === true;
                return {
                    source,
                    baseline_variant: baselineVariant,
                    missing_variants: records.filter(({ run }) => !run?.finished_at).map(({ suite }) => suite.variant),
                    variants: records.map(({ suite, run }) => {
                        const complete = run?.compatibility?.complete === true;
                        const comparable = complete && baselineComplete;
                        return {
                            variant: suite.variant,
                            requested_model: suite.model,
                            actual_model: run?.actual_model ?? null,
                            skills: suite.skills,
                            trials_per_task: run?.trials_per_task ?? null,
                            native_pass_rate: run?.native_summary?.success_rate ?? null,
                            native_score: run?.native_summary?.aggregate_score ?? null,
                            source_threshold_score: run?.compatibility?.score ?? null,
                            source_threshold_passed: run?.compatibility?.passed ?? null,
                            complete,
                            comparable_to_baseline: comparable,
                            delta_from_baseline: comparable ? {
                                native_pass_rate: run.native_summary.success_rate - baseline.run.native_summary.success_rate,
                                source_threshold_score: run.compatibility.score - baseline.run.compatibility.score,
                            } : null,
                        };
                    }),
                };
            }),
        };
    });
}

export function writeExperimentSummary(directory, manifest, provenance) {
    const experiments = summarizeExperiments(manifest, provenance.runs);
    writeFileSync(join(directory, "experiment-summary.json"), `${JSON.stringify({
        waza_release: provenance.release.version,
        upstream_commit: provenance.upstream_commit,
        experiments,
    }, null, 2)}\n`);
    return experiments;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    if (process.argv.length !== 3) {
        throw new Error("Usage: node scripts\\compare.mjs <results-directory>");
    }
    const directory = resolve(process.argv[2]);
    const manifest = JSON.parse(readFileSync(join(root, "manifest.json"), "utf8"));
    const provenance = JSON.parse(readFileSync(join(directory, "run-manifest.json"), "utf8"));
    writeExperimentSummary(directory, manifest, provenance);
    console.log(`Saved baseline comparisons: ${join(directory, "experiment-summary.json")}`);
}
