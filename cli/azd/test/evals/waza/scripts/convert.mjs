// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { createHash } from "node:crypto";
import { existsSync, mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import { dirname, join, relative, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import YAML from "yaml";

export const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
export const sourceRoot = dirname(root);
const repositoryRoot = resolve(root, "..", "..", "..", "..", "..");

export function timeoutSeconds(value) {
    const match = /^(\d+)(s|m|h)$/.exec(value);
    if (!match) {
        throw new Error(`Unsupported Vally timeout: ${value}`);
    }
    return Number(match[1]) * { s: 1, m: 60, h: 3600 }[match[2]];
}

function fixtureFiles(environment) {
    return (environment.files ?? []).map(({ src, dest }) => ({
        path: dest,
        content: readFileSync(resolve(sourceRoot, src), "utf8"),
    }));
}

export function convertGrader(grader, stimulus, files, environment) {
    const config = grader.config ?? {};
    if (grader.type === "prompt") {
        return {
            type: "prompt",
            name: grader.name,
            config: {
                mode: "independent",
                continue_session: false,
                prompt: [
                    "Judge the recorded agent work, not the quality of these evaluation instructions.",
                    "Use the supplied recorded task, final output, and conversation/tool evidence. Do not execute commands or change files.",
                    config.prompt ?? "",
                    ...(stimulus.rubric ?? []).map((criterion, index) => `${index + 1}. ${criterion}`),
                    "Call exactly one of set_waza_grade_pass or set_waza_grade_fail once, with a specific explanation.",
                ].filter(Boolean).join("\n\n"),
                evidence_context: {},
            },
        };
    }
    if (grader.type === "skill-invocation") {
        return {
            type: "skill_invocation",
            name: grader.name,
            config: { required_skills: config.required, mode: "any_order", allow_extra: true },
        };
    }
    if (grader.type === "file-exists") {
        return { type: "file", name: grader.name, config: { must_exist: [config.path] } };
    }
    if (grader.type === "output-contains") {
        return {
            type: "text",
            name: grader.name,
            config: { [config.case_sensitive ? "contains_cs" : "contains"]: [config.substring] },
        };
    }
    const supported = new Set([
        "output-matches", "output-not-matches", "file-matches", "file-not-matches",
        "tool-calls", "diff-empty", "program",
    ]);
    if (!supported.has(grader.type)) {
        throw new Error(`Unsupported Vally grader: ${grader.type}`);
    }
    const check = { ...grader };
    if (grader.type === "diff-empty") {
        if (environment.git) {
            check.repository = "repository";
        } else {
            check.baseline = files;
        }
    }
    if (grader.type === "program" &&
        (config.program !== "node" || config.args?.join(" ") !== "check-summary.ts")) {
        throw new Error(`Unsupported custom program: ${JSON.stringify(config)}`);
    }
    return {
        type: "program",
        name: grader.name,
        config: {
            command: "node",
            args: [join("graders", "vally-check.mjs"), JSON.stringify(check)],
            timeout: 30,
        },
    };
}

export function convertTask(stimulus, suiteEnvironment = {}) {
    const environment = { ...suiteEnvironment, ...stimulus.environment };
    const files = fixtureFiles(environment);
    const prompts = stimulus.turns ?? [stimulus.prompt];
    if (!prompts.length || prompts.some((prompt) => typeof prompt !== "string" || !prompt.trim())) {
        throw new Error(`Invalid stimulus prompts: ${stimulus.name}`);
    }
    const task = {
        id: stimulus.name,
        name: stimulus.name,
        inputs: { prompt: prompts[0] },
        graders: [],
    };
    if (files.length) {
        task.inputs.files = files;
    }
    if (prompts.length > 1) {
        task.inputs.follow_up_prompts = prompts.slice(1);
    }
    if (environment.git) {
        if (environment.git.type !== "worktree") {
            throw new Error(`Unsupported repository fixture: ${environment.git.type}`);
        }
        task.inputs.repos = [{
            type: "worktree",
            source: relative(root, repositoryRoot),
            commit: environment.git.ref,
            dest: "repository",
        }];
        // Native worktrees require a child destination; starter fixture files
        // remain at the outer root, whereas repository-only cases run inside it.
        if (!files.length) {
            task.inputs.workdir = "repository";
        }
    }
    for (const grader of stimulus.graders) {
        const converted = convertGrader(grader, stimulus, files, environment);
        if (grader.turn !== undefined) {
            if (!Number.isInteger(grader.turn) || grader.turn < 0 || grader.turn >= prompts.length) {
                throw new Error(`Invalid grader turn in ${stimulus.name}: ${grader.turn}`);
            }
            task.checkpoints ??= [];
            let checkpoint = task.checkpoints.find((item) => item.after_turn === grader.turn + 1);
            if (!checkpoint) {
                checkpoint = { after_turn: grader.turn + 1, on_failure: "continue", graders: [] };
                task.checkpoints.push(checkpoint);
            }
            checkpoint.graders.push(converted);
        } else {
            task.graders.push(converted);
        }
    }
    return task;
}

function writeIfChanged(path, content) {
    mkdirSync(dirname(path), { recursive: true });
    if (!existsSync(path) || readFileSync(path, "utf8") !== content) {
        writeFileSync(path, content);
    }
}

function writeYaml(path, value) {
    const document = new YAML.Document(value);
    YAML.visit(document, {
        Scalar(_, node) {
            if (typeof node.value === "string" && /[ \t]+(?:\r?\n|$)/.test(node.value)) {
                node.type = YAML.Scalar.QUOTE_DOUBLE;
            }
        },
    });
    writeIfChanged(path, document.toString({ lineWidth: 0, doubleQuotedAsJSON: true }));
}

function buildSuite(sourceFile, source, folder, overrides = {}) {
    const directory = join(root, "suites", folder);
    const skills = overrides.skills ?? source.environment?.skills ?? [];
    const spec = {
        schemaVersion: "2.0",
        name: overrides.name ?? source.name,
        scenario: "azure-dev-vally-migration",
        description: source.description ?? `Waza conversion of ${sourceFile}`,
        config: {
            executor: source.defaults.executor,
            model: overrides.model ?? source.defaults.model,
            judge_model: source.defaults.judge_model,
            trials_per_task: source.defaults.runs,
            timeout_seconds: timeoutSeconds(source.defaults.timeout),
            parallel: false,
            inject_skill_body: false,
            skill_directories: skills.map((skill) => relative(directory, resolve(sourceRoot, skill))),
        },
        metrics: [{
            name: "task_completion",
            weight: 1,
            threshold: 1,
            description: "Native strict grader verdict; original suite thresholds are reported separately.",
        }],
        tasks: source.stimuli.map((stimulus) => join("tasks", `${stimulus.name}.yaml`)),
    };
    if (!skills.length) {
        spec.config.disabled_skills = ["*"];
    }
    for (const stimulus of source.stimuli) {
        writeYaml(join(directory, "tasks", `${stimulus.name}.yaml`), convertTask(stimulus, source.environment));
    }
    writeYaml(join(directory, "eval.yaml"), spec);
    return {
        name: spec.name,
        source: sourceFile,
        source_sha256: createHash("sha256").update(readFileSync(join(sourceRoot, sourceFile))).digest("hex"),
        spec: relative(root, join(directory, "eval.yaml")),
        model: spec.config.model,
        judge_model: spec.config.judge_model,
        skills,
        trials: spec.config.trials_per_task,
        threshold: source.scoring.threshold,
        task_ids: source.stimuli.map((stimulus) => stimulus.name),
        graders: Object.fromEntries(source.stimuli.map((stimulus) => [
            stimulus.name, stimulus.graders.map((grader) => grader.name),
        ])),
        requires_independent_judge: source.stimuli.some((stimulus) =>
            stimulus.graders.some((grader) => grader.type === "prompt")),
        environment: source.environment?.env ?? {},
        experiment: overrides.experiment ?? null,
        variant: overrides.variant ?? null,
    };
}

export function convertAll() {
    const sourceFiles = readdirSync(sourceRoot).filter((file) =>
        /^eval(?:-.*)?\.yaml$/.test(file) && !file.endsWith(".experiments.yaml"));
    const suites = [];
    const sources = new Map();
    for (const file of sourceFiles.sort()) {
        const source = YAML.parse(readFileSync(join(sourceRoot, file), "utf8"));
        sources.set(file, source);
        suites.push(buildSuite(file, source, source.name));
    }
    const experiments = [];
    for (const [id, file] of [
        ["azd", "eval-azd.experiments.yaml"], ["foundry", "eval-foundry.experiments.yaml"],
    ]) {
        const experiment = YAML.parse(readFileSync(join(sourceRoot, file), "utf8"));
        experiments.push({ id, name: experiment.name, source: file, baseline: experiment.baseline });
        for (const skillsEntry of experiment.matrix.skills.values) {
            for (const modelEntry of experiment.matrix.model.values) {
                const [[skillsName, skills]] = Object.entries(skillsEntry);
                const [[modelName, model]] = Object.entries(modelEntry);
                const variant = `${skillsName}--${modelName}`;
                for (const sourceFile of experiment.evals) {
                    const source = sources.get(sourceFile);
                    if (!source) {
                        throw new Error(`Unknown experiment suite: ${sourceFile}`);
                    }
                    suites.push(buildSuite(sourceFile, source, `${id}--${variant}--${source.name}`, {
                        skills, model, name: `${experiment.name}--${variant}--${source.name}`,
                        experiment: id, variant,
                    }));
                }
            }
        }
    }
    const manifest = { release: "0.41.0", experiments, suites };
    writeIfChanged(join(root, "manifest.json"), `${JSON.stringify(manifest, null, 2)}\n`);
    return manifest;
}

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
    const manifest = convertAll();
    console.log(`Converted ${manifest.suites.filter((suite) => !suite.experiment).length} suites and ${manifest.experiments.length} experiment matrices (${manifest.suites.length} executable configurations).`);
}
