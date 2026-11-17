// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { spawn, spawnSync } from "node:child_process";
import { createHash } from "node:crypto";
import { createWriteStream, existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { finished } from "node:stream/promises";
import { dirname, isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { parseArgs } from "node:util";
import { writeExperimentSummary } from "./compare.mjs";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");

function readJson(path) {
    return JSON.parse(readFileSync(path, "utf8"));
}

function git(args) {
    const result = spawnSync("git", args, { cwd: root, encoding: "utf8" });
    if (result.error) {
        throw result.error;
    }
    if (result.status !== 0) {
        throw new Error(`git ${args.join(" ")}: ${result.stderr}`);
    }
    return result.stdout.trim();
}

export function compatibilitySummary(result, suite) {
    const trials = result.tasks.flatMap((task) => task.runs.map((run) => {
        const validations = [
            ...Object.values(run.validations ?? {}),
            ...(run.checkpoints ?? []).flatMap((checkpoint) => Object.values(checkpoint.validations)),
        ];
        const expected = suite.graders[task.test_id];
        if (!expected) {
            throw new Error(`Unexpected result task: ${task.test_id}`);
        }
        const checks = expected.map((name) => {
            const grade = validations.find((item) => item.identifier === name);
            if (!grade) {
                return { name, score: 0, missing: true };
            }
            if (grade.details?.operational_error) {
                return { name, score: 0, operational_error: true };
            }
            let score = grade.score;
            const programOutput = grade.details?.stdout ?? grade.feedback;
            if (grade.type === "program") {
                let feedback;
                try {
                    feedback = JSON.parse(programOutput);
                } catch (error) {
                    if (!(error instanceof SyntaxError)) {
                        throw error;
                    }
                    return { name, score: 0, operational_error: true };
                }
                if (!feedback || feedback.name !== name || typeof feedback.passed !== "boolean" ||
                    feedback.passed !== grade.passed || !Number.isFinite(feedback.score) ||
                    feedback.score < 0 || feedback.score > 1) {
                    return { name, score: 0, operational_error: true };
                }
                score = feedback.score;
            }
            if (!Number.isFinite(score) || score < 0 || score > 1) {
                return { name, score: 0, operational_error: true };
            }
            return { name, score, passed: grade.passed };
        });
        return {
            task: task.test_id,
            trial: run.run_number,
            native_status: run.status,
            execution_error: run.error_msg ?? null,
            missing_checks: checks.some((check) => check.missing),
            grader_errors: checks.some((check) => check.operational_error),
            score: checks.reduce((sum, check) => sum + check.score, 0) / checks.length,
            checks,
        };
    }));
    const missingTasks = suite.task_ids.filter((id) => !result.tasks.some((task) => task.test_id === id));
    const expectedTrials = suite.expected_trials ?? suite.trials ?? 1;
    const trialCountsComplete = suite.task_ids.every((id) =>
        result.tasks.find((task) => task.test_id === id)?.runs.length === expectedTrials);
    const complete = trials.length > 0 && !missingTasks.length && trialCountsComplete &&
        trials.every((trial) => !trial.execution_error && !trial.missing_checks && !trial.grader_errors &&
            ["passed", "failed"].includes(trial.native_status));
    const score = trials.length ? trials.reduce((sum, trial) => sum + trial.score, 0) / trials.length : 0;
    return {
        scoring: "mean of original per-stimulus grader scores, then trials; original suite threshold",
        threshold: suite.threshold,
        score,
        passed: complete && score + 1e-9 >= suite.threshold,
        complete,
        missing_tasks: missingTasks,
        trials,
    };
}

export function assertNewOutputDirectory(directory) {
    if (existsSync(directory) && readdirSync(directory).length) {
        throw new Error(`Output directory is not empty; choose a fresh destination: ${directory}`);
    }
}

export function requireJudgeAuthentication(suites, environment) {
    if (suites.some((suite) => suite.requires_independent_judge) &&
        !["COPILOT_GITHUB_TOKEN", "GH_TOKEN", "GITHUB_TOKEN"].some((key) => environment[key]?.trim())) {
        throw new Error("Independent judges require COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN; configure authentication before starting paid actor runs.");
    }
}

async function invoke(binary, args, logPath, environment) {
    const log = createWriteStream(logPath);
    log.write(`${JSON.stringify({ executable: binary, args, cwd: root })}\n`);
    const child = spawn(binary, args, { cwd: root, env: { ...process.env, ...environment } });
    child.stdout.pipe(log, { end: false });
    child.stderr.pipe(log, { end: false });
    const code = await new Promise((resolveExit, reject) => {
        child.once("error", reject);
        child.once("close", (status, signal) => {
            if (signal) {
                reject(new Error(`Waza terminated by ${signal}`));
            } else {
                resolveExit(status);
            }
        });
    });
    log.end();
    await finished(log);
    return code;
}

export async function main() {
    const { values } = parseArgs({
        options: {
            preflight: { type: "boolean", default: false },
            suite: { type: "string", multiple: true },
            experiment: { type: "string" },
            "include-experiments": { type: "boolean", default: false },
            trials: { type: "string" },
            workers: { type: "string", default: "2" },
            "model-alias": { type: "string", multiple: true, default: [] },
            "task": { type: "string" },
            output: { type: "string" },
        },
    });
    for (const key of ["trials", "workers"]) {
        if (values[key] !== undefined && !/^[1-9]\d*$/.test(values[key])) {
            throw new Error(`--${key} must be a positive integer`);
        }
    }
    const aliases = new Map(values["model-alias"].map((entry) => {
        const parts = entry.split("=");
        if (parts.length !== 2 || parts.some((part) => !part)) {
            throw new Error(`Invalid --model-alias: ${entry}; expected original=available`);
        }
        return parts;
    }));
    const release = readJson(join(root, "release.json"));
    const binary = process.env.WAZA_BINARY ?? "waza";
    const version = spawnSync(binary, ["--version", "--no-update-check"], { cwd: root, encoding: "utf8" });
    if (version.error) {
        throw version.error;
    }
    if (version.status !== 0 || version.stdout.trim() !== `waza version ${release.version}`) {
        throw new Error(`Expected Waza ${release.version}; got ${version.stdout} ${version.stderr}`);
    }
    let binarySha256 = null;
    if (isAbsolute(binary)) {
        binarySha256 = createHash("sha256").update(readFileSync(binary)).digest("hex");
        if (process.platform === "win32" && process.arch === "x64" && binarySha256 !== release.windowsAmd64.sha256) {
            throw new Error("Waza binary does not match the pinned official Windows amd64 release checksum");
        }
    }
    const manifest = readJson(join(root, "manifest.json"));
    let suites = manifest.suites.filter((suite) => values.experiment ?
        suite.experiment === values.experiment : values["include-experiments"] || !suite.experiment);
    if (values.suite) {
        suites = suites.filter((suite) => values.suite.includes(suite.name));
    }
    if (!suites.length) {
        throw new Error("No matching suites; use a name from manifest.json");
    }
    if (!values.preflight) {
        requireJudgeAuthentication(suites, process.env);
    }
    const directory = resolve(root, values.output ?? join("results", new Date().toISOString().replaceAll(":", "-")));
    assertNewOutputDirectory(directory);
    for (const subdirectory of ["logs", "dashboard", "transcripts", "compatibility"]) {
        mkdirSync(join(directory, subdirectory), { recursive: true });
    }
    const provenance = {
        release,
        binary_sha256: binarySha256,
        upstream_commit: git(["rev-parse", "HEAD"]),
        branch: git(["branch", "--show-current"]),
        started_at: new Date().toISOString(),
        mode: values.preflight ? "preflight" : "real-copilot-sdk",
        trials_override: values.trials ? Number(values.trials) : null,
        model_aliases: Object.fromEntries(aliases),
        external_skill: existsSync(resolve(root, "..", "skills", ".external", ".skill-ref")) ?
            readFileSync(resolve(root, "..", "skills", ".external", ".skill-ref"), "utf8") : null,
        runs: [],
    };
    const save = () => writeFileSync(join(directory, "run-manifest.json"), `${JSON.stringify(provenance, null, 2)}\n`);
    save();
    console.log(`${version.stdout.trim()}; ${suites.length} configurations; results: ${directory}`);
    if (aliases.size) {
        console.log(`Explicit model aliases: ${JSON.stringify(Object.fromEntries(aliases))}`);
    }
    let failed = false;
    for (const suite of suites) {
        const args = [values.preflight ? "preflight" : "run", suite.spec, "--no-update-check"];
        const resultFile = join(directory, "dashboard", `${suite.name}.json`);
        const actualModel = aliases.get(suite.model) ?? suite.model;
        const actualJudge = aliases.get(suite.judge_model) ?? suite.judge_model;
        if (values.preflight) {
            args.push("--format", "json");
        } else {
            args.push("--output", resultFile, "--model", actualModel, "--judge-model", actualJudge,
                "--transcript-dir", join(directory, "transcripts", suite.name),
                "--keep-workspace", "--parallel", "--workers", values.workers);
            if (values.trials) {
                args.push("--trials", values.trials);
            }
            if (values.task) {
                args.push("--task", values.task);
            }
        }
        const record = {
            suite: suite.name, source: suite.source, source_sha256: suite.source_sha256,
            experiment: suite.experiment, variant: suite.variant,
            requested_model: suite.model, actual_model: actualModel,
            requested_judge: suite.judge_model, actual_judge: actualJudge,
            trials_per_task: Number(values.trials ?? suite.trials),
            args, started_at: new Date().toISOString(),
        };
        provenance.runs.push(record);
        save();
        console.log(`Starting ${suite.name}`);
        record.exit_code = await invoke(binary, args, join(directory, "logs", `${suite.name}.log`), {
            NO_COLOR: "1", AZURE_DEV_COLLECT_TELEMETRY: "no", ...suite.environment,
        });
        record.finished_at = new Date().toISOString();
        if (!values.preflight && existsSync(resultFile)) {
            const result = readJson(resultFile);
            record.native_summary = result.summary;
            const selectedSuite = {
                ...suite,
                expected_trials: Number(values.trials ?? suite.trials),
                task_ids: values.task ? result.tasks.map((task) => task.test_id) : suite.task_ids,
            };
            record.compatibility = compatibilitySummary(result, selectedSuite);
            writeFileSync(join(directory, "compatibility", `${suite.name}.json`),
                `${JSON.stringify(record.compatibility, null, 2)}\n`);
        }
        failed ||= record.exit_code !== 0;
        save();
        console.log(`Finished ${suite.name}: exit ${record.exit_code}${record.native_summary ?
            `; native ${record.native_summary.succeeded}/${record.native_summary.total_tests} passed; source-threshold ${record.compatibility.passed ? "PASS" : "FAIL"}` : ""}`);
    }
    provenance.finished_at = new Date().toISOString();
    save();
    if (suites.some((suite) => suite.experiment)) {
        writeExperimentSummary(directory, manifest, provenance);
    }
    process.exitCode = failed ? 1 : 0;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    await main();
}
