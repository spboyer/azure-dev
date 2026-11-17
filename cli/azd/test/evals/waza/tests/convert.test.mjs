// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import YAML from "yaml";
import { convertAll, convertTask, root, sourceRoot, timeoutSeconds } from "../scripts/convert.mjs";
import { assertNewOutputDirectory, compatibilitySummary, requireJudgeAuthentication } from "../scripts/run.mjs";

test("all seven suites and both full experiment matrices preserve prompts, checks, and trial counts", () => {
    const manifest = convertAll();
    assert.equal(manifest.suites.filter((suite) => !suite.experiment).length, 7);
    assert.equal(manifest.experiments.length, 2);
    assert.equal(manifest.suites.length, 19);
    for (const suite of manifest.suites) {
        const source = YAML.parse(readFileSync(join(sourceRoot, suite.source), "utf8"));
        const spec = YAML.parse(readFileSync(join(root, suite.spec), "utf8"));
        assert.equal(spec.schemaVersion, "2.0");
        assert.equal(spec.config.executor, "copilot-sdk");
        assert.equal(spec.config.trials_per_task, source.defaults.runs);
        assert.equal(suite.threshold, source.scoring.threshold);
        assert.equal(spec.config.judge_model, source.defaults.judge_model);
        assert.equal(spec.tasks.length, source.stimuli.length);
        assert.equal(Boolean(spec.config.disabled_skills), suite.skills.length === 0);
        source.stimuli.forEach((stimulus, index) => {
            const taskYaml = readFileSync(join(root, "suites", suite.spec.split(/[\\/]/)[1], spec.tasks[index]), "utf8");
            assert(!/[ \t]+$/m.test(taskYaml), "Serialized fixture content must not introduce trailing whitespace");
            const task = YAML.parse(taskYaml);
            const environment = { ...source.environment, ...stimulus.environment };
            assert.deepEqual(task.inputs.files ?? [], (environment.files ?? []).map(({ src, dest }) => ({
                path: dest, content: readFileSync(join(sourceRoot, src), "utf8"),
            })));
            const prompts = [task.inputs.prompt, ...(task.inputs.follow_up_prompts ?? [])];
            assert.deepEqual(prompts, stimulus.turns ?? [stimulus.prompt]);
            const checks = [...task.graders, ...(task.checkpoints ?? []).flatMap((checkpoint) => checkpoint.graders)];
            assert.deepEqual(checks.map((check) => check.name).sort(), stimulus.graders.map((check) => check.name).sort());
            for (const grader of stimulus.graders.filter((grader) => grader.turn !== undefined)) {
                assert(task.checkpoints.some((checkpoint) =>
                    checkpoint.after_turn === grader.turn + 1 && checkpoint.graders.some((check) => check.name === grader.name)));
            }
        });
    }
});

test("timeouts reject unsupported syntax instead of silently defaulting", () => {
    assert.equal(timeoutSeconds("8m"), 480);
    assert.equal(timeoutSeconds("30s"), 30);
    assert.throws(() => timeoutSeconds("forever"), /Unsupported/);
});

test("unknown graders and invalid turn references fail conversion", () => {
    assert.throws(() => convertTask({ name: "bad", prompt: "hello", graders: [{ type: "unknown" }] }), /Unsupported/);
    assert.throws(() => convertTask({
        name: "bad-turn", prompt: "hello",
        graders: [{ name: "bad", type: "output-matches", config: { pattern: "x" }, turn: 1 }],
    }), /Invalid grader turn/);
});

test("compatibility aggregation preserves fractional program scores and checkpoint grades", () => {
    const suite = { threshold: 0.7, task_ids: ["a"], graders: { a: ["summary", "turn"] } };
    const result = { tasks: [{ test_id: "a", runs: [{
        run_number: 1, status: "failed",
        validations: { summary: {
            identifier: "summary", type: "program", score: 0, passed: false,
            feedback: "Program exited with code 1",
            details: { stdout: '{"name":"summary","passed":false,"score":0.5}' },
        } },
        checkpoints: [{ validations: { turn: { identifier: "turn", type: "text", score: 1, passed: true } } }],
    }] }] };
    const summary = compatibilitySummary(result, suite);
    assert.equal(summary.score, 0.75);
    assert.equal(summary.passed, true);
    assert.equal(compatibilitySummary(result, { ...suite, expected_trials: 2 }).complete, false);
    result.tasks[0].runs[0].error_msg = "execution failed";
    assert.equal(compatibilitySummary(result, suite).passed, false);
    assert.equal(compatibilitySummary({ tasks: [] }, suite).complete, false);
});

test("grader operational errors and invalid adapter verdicts never satisfy a permissive source threshold", () => {
    const suite = { threshold: 0.1, task_ids: ["a"], graders: { a: ["broken", "good"] } };
    for (const broken of [
        { type: "program", details: { operational_error: true } },
        { type: "program", details: { stdout: "adapter crashed" } },
        { type: "program", details: { stdout: '{"name":"broken","passed":true,"score":1}' }, passed: false },
        { type: "program", details: { stdout: '{"name":"other","passed":true,"score":1}' }, passed: true },
    ]) {
        const summary = compatibilitySummary({ tasks: [{ test_id: "a", runs: [{
            status: "failed", run_number: 1,
            validations: {
                broken: { identifier: "broken", score: 0, passed: false, ...broken },
                good: { identifier: "good", type: "text", score: 1, passed: true },
            },
        }] }] }, suite);
        assert.equal(summary.complete, false);
        assert.equal(summary.passed, false);
        assert.equal(summary.trials[0].grader_errors, true);
    }
});

test("a previous result directory cannot be reused as fresh execution evidence", (t) => {
    const directory = mkdtempSync(join(tmpdir(), "azd-waza-output-"));
    t.after(() => rmSync(directory, { recursive: true, force: true }));
    assertNewOutputDirectory(directory);
    writeFileSync(join(directory, "old-result.json"), "{}");
    assert.throws(() => assertNewOutputDirectory(directory), /not empty/);
});

test("missing independent-judge authentication fails before paid actors run", () => {
    const suites = [{ requires_independent_judge: true }];
    assert.throws(() => requireJudgeAuthentication(suites, {}), /Independent judges require/);
    assert.throws(() => requireJudgeAuthentication(suites, { GH_TOKEN: " " }), /Independent judges require/);
    for (const key of ["COPILOT_GITHUB_TOKEN", "GH_TOKEN", "GITHUB_TOKEN"]) {
        requireJudgeAuthentication(suites, { [key]: "synthetic-test-value" });
    }
    requireJudgeAuthentication([{ requires_independent_judge: false }], {});
});
