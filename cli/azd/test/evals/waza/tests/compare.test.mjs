// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import test from "node:test";
import { summarizeExperiments } from "../scripts/compare.mjs";

function fixture() {
    const variants = ["off--model-a", "on--model-a", "off--model-b", "on--model-b"];
    const manifest = {
        experiments: [{ id: "example", name: "matrix", baseline: { skills: "off", model: "model-a" } }],
        suites: variants.map((variant) => ({
            name: variant, variant, model: variant.split("--")[1],
            skills: variant.startsWith("on") ? ["skill"] : [],
            experiment: "example", source: "eval.yaml", source_sha256: "source-hash",
        })),
    };
    const runs = manifest.suites.map((suite, index) => ({
        suite: suite.name, source_sha256: "source-hash", actual_model: suite.model,
        finished_at: "recorded", trials_per_task: 1,
        native_summary: { success_rate: index / 4, aggregate_score: index / 4 },
        compatibility: { complete: true, score: index / 4, passed: index > 1 },
    }));
    return { manifest, runs };
}

test("full matrix deltas use the declared skill/model baseline, not per-model controls", () => {
    const { manifest, runs } = fixture();
    const summary = summarizeExperiments(manifest, runs)[0].suites[0];
    assert.deepEqual(summary.missing_variants, []);
    assert.equal(summary.baseline_variant, "off--model-a");
    assert.equal(summary.variants[3].delta_from_baseline.source_threshold_score, 0.75);
    assert.equal(summary.variants[3].trials_per_task, 1);
});

test("missing and infrastructure-failed cells are not compared as valid zero scores", () => {
    const { manifest, runs } = fixture();
    runs[1].compatibility.complete = false;
    runs.pop();
    let summary = summarizeExperiments(manifest, runs)[0].suites[0];
    assert.deepEqual(summary.missing_variants, ["on--model-b"]);
    assert.equal(summary.variants[1].delta_from_baseline, null);
    assert.equal(summary.variants[3].source_threshold_score, null);
    runs[0].compatibility.complete = false;
    summary = summarizeExperiments(manifest, runs)[0].suites[0];
    assert(summary.variants.every((variant) => variant.delta_from_baseline === null));
});

test("comparisons reject changed source definitions and missing declared baselines", () => {
    const { manifest, runs } = fixture();
    runs[0].source_sha256 = "changed";
    assert.throws(() => summarizeExperiments(manifest, runs), /Source changed/);
    runs[0].source_sha256 = "source-hash";
    manifest.experiments[0].baseline.model = "not-in-matrix";
    assert.throws(() => summarizeExperiments(manifest, runs), /Missing declared baseline/);
});
