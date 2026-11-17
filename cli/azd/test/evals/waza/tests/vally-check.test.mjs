// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import { compilePattern, evaluateCheck } from "../graders/vally-check.mjs";

const here = dirname(fileURLToPath(import.meta.url));
const script = resolve(here, "..", "graders", "vally-check.mjs");
const context = { output: "", tool_calls: [], transcript: [] };
const summaryCheck = { type: "program", name: "summary", config: { program: "node", args: ["check-summary.ts"] } };

function workspace(t) {
  const path = mkdtempSync(join(here, ".vally-check-"));
  t.after(() => rmSync(path, { recursive: true, force: true, maxRetries: 3 }));
  return path;
}

function write(root, path, content) {
  const target = join(root, path);
  mkdirSync(dirname(target), { recursive: true });
  writeFileSync(target, content);
}

function grade(root, type, config = {}, input = context, extra = {}) {
  return evaluateCheck({ type, name: "test check", config, ...extra }, input, root);
}

function completedContext(calls, extra = {}) {
  return {
    ...context,
    ...extra,
    tool_calls: calls,
    transcript: calls.map((call) => ({ type: "tool.execution_complete", tool_call_id: call.id })),
  };
}

test("converts only leading global flags and preserves regex syntax", async (t) => {
  const cases = [
    ["(?i)^AZD$", "azd", true],
    ["(?m)^Total notes: 3$", "before\nTotal notes: 3\nafter", true],
    ["(?s)^first.*last$", "first\nmiddle\nlast", true],
    ["(?is)^FIRST.*LAST$", "first\nlast", true],
    ["(?ims)^FIRST.*LAST$", "other\nfirst\nlast\nother", true],
    ["(?i)(?m)(?s)^FIRST.*LAST$", "other\nfirst\nlast\nother", true],
    ["(?ii)^azd$", "AZD", true],
    ["^AZD$", "azd", false],
    ["^first.*last$", "first\nlast", false],
    ["(?is)(?=.*\\btoolbox)(?=.*\\b(routine|inspector|connection))", "Toolbox\nINSPECTOR", true],
    ["(?s)(?=.*azure\\.yaml)(?=.*(syntax|format|indentation|parse|YAML))", "azure.yaml\nsyntax", true],
    ["azd ai agent doctor(?!\\S)(?!.*--help)", "azd ai agent doctor", true],
    ["azd ai agent doctor(?!\\S)(?!.*--help)", "azd ai agent doctor --help", false],
    ["azd (down|up|provision|deploy)(?!\\S)", "azd downgrade", false],
    ["(?<=azd )deploy", "azd deploy", true],
    [String.raw`\(\?i\)`, "(?i)", true],
    ["[()?ims:]+", "(?i:)", true],
  ];
  for (const [pattern, text, expected] of cases) {
    await t.test(pattern, () => assert.equal(compilePattern(pattern).test(text), expected));
  }
});

test("rejects unsupported scoped, embedded and unknown regex flags", () => {
  for (const pattern of ["(?i:azd)", "(?-i:azd)", "a(?i)b", "(?i)a(?m)b", "(?x)azd", "(?im-s)azd"]) {
    assert.throws(() => compilePattern(pattern), /Unsupported embedded or scoped regex flags/);
  }
  assert.throws(() => compilePattern("["), SyntaxError);
  assert.throws(() => compilePattern(undefined), /must be a string/);
});

test("output checks use actual final output, with literal case sensitivity", (t) => {
  const root = workspace(t);
  const input = { ...context, output: "Total notes: 3", transcript: "azd down" };
  assert.equal(grade(root, "output-matches", { pattern: "(?m)^Total notes: 3$" }, input).passed, true);
  assert.equal(grade(root, "output-not-matches", { pattern: "azd down" }, input).passed, true);
  assert.equal(grade(root, "output-not-matches", { pattern: "Total notes" }, input).passed, false);
  assert.equal(grade(root, "output-matches", { pattern: "^total notes:" }, input).passed, false);
  assert.equal(grade(root, "output-contains", { substring: "Total notes: 3", case_sensitive: true }, input).passed, true);
  assert.equal(grade(root, "output-contains", { substring: "total notes: 3", case_sensitive: true }, input).passed, false);
  assert.equal(grade(root, "output-contains", { substring: "total notes: 3", case_sensitive: false }, input).passed, true);
  assert.equal(grade(root, "output-contains", { substring: "notes: [3]" }, input).passed, false);
  assert.throws(() => grade(root, "output-not-matches", { pattern: "anything" }, {}), /context.output/);
});

test("file checks preserve JSON backslashes, inline flags, and block-scoped semantics", (t) => {
  const root = workspace(t);
  write(root, "azure.yaml", [
    "services:",
    "  triage-agent:",
    "    host: azure.ai.agent",
    "  escalation-agent:",
    "    host: azure.ai.agent",
    "    env:",
    "      LOG_LEVEL: debug",
    "",
  ].join("\n"));
  const check = JSON.parse(String.raw`{"type":"file-matches","config":{"path":"azure.yaml","pattern":"(?m)^    host:\\s*azure\\.ai\\.agent$"}}`);
  assert.equal(evaluateCheck(check, context, root).passed, true);
  const block = String.raw`(?is)\n  escalation-agent:(?:(?:[ \t]{4,}[^\n]*|[ \t]*)\n)*?[ \t]{4,}[^\n]*LOG_LEVEL[\s\S]{0,30}?debug`;
  assert.equal(grade(root, "file-matches", { path: "azure.yaml", pattern: block }).passed, true);
  assert.equal(grade(root, "file-not-matches", { path: "azure.yaml", pattern: String.raw`(?m)^\s+config:\s*$` }).passed, true);
  assert.equal(grade(root, "file-exists", { path: "azure.yaml" }).passed, true);
  write(root, "azure.yaml", "services:\n  triage-agent:\n    LOG_LEVEL: debug\n  escalation-agent:\n    host: azure.ai.agent\n");
  assert.equal(grade(root, "file-matches", { path: "azure.yaml", pattern: block }).passed, false);
});

test("missing and nonregular artifacts fail even negative file checks", (t) => {
  const root = workspace(t);
  mkdirSync(join(root, "directory"));
  for (const type of ["file-matches", "file-not-matches", "file-exists"]) {
    for (const path of ["missing.md", "directory"]) {
      const evaluation = grade(root, type, { path, pattern: "anything" });
      assert.equal(evaluation.passed, false);
      assert.equal(evaluation.score, 0);
      assert.match(evaluation.evidence, /does not exist|Not a regular/);
    }
  }
  for (const path of ["../outside", "C:\\outside.txt", "/outside.txt", "."]) {
    assert.throws(() => grade(root, "file-exists", { path }), /relative workspace path|inside the workspace/);
  }
});

test("required command and path constraints all match recorded calls, not transcript text", (t) => {
  const root = workspace(t);
  const config = {
    required: [
      { name: "^(bash|powershell)$", command: String.raw`azd ai[^|]*(--help|\bhelp\b)` },
      { name: "^view$", path: String.raw`observe/observe\.md$` },
    ],
  };
  const input = completedContext([
    { id: "help", name: "powershell", arguments: { command: "azd ai --help" } },
    { id: "read", name: "view", arguments: { path: "/skills/observe/observe.md" } },
  ], {
    output: "azd ai --help",
    tool_events: [{ name: "view", arguments: { path: "observe/observe.md" } }],
  });
  assert.equal(grade(root, "tool-calls", config, input).passed, true);
  assert.equal(grade(root, "tool-calls", config, { ...input, tool_calls: input.tool_calls.slice(0, 1) }).passed, false);
  assert.equal(grade(root, "tool-calls", config, { ...input, tool_calls: [] }).passed, false);
  const mismatches = [
    { name: "not-powershell", arguments: { command: "azd ai --help" } },
    { name: "powershell", arguments: { path: "azd ai --help" } },
    { name: "powershell", arguments: { command: ["azd ai --help"] } },
    { name: "powershell", arguments: { command: "echo hello" } },
  ];
  for (const call of mismatches) {
    assert.equal(grade(root, "tool-calls", config, {
      ...input, tool_calls: [{ ...call, id: "help" }, input.tool_calls[1]],
    }).passed, false);
  }
  assert.throws(() => grade(root, "tool-calls", config, { output: "" }), /context.tool_calls/);
});

test("disallowed command and path constraints inspect the same real call", (t) => {
  const root = workspace(t);
  const config = {
    required: [{ name: "^(bash|powershell)$", command: String.raw`azd ai agent doctor(?!\S)(?!.*--help)` }],
    disallowed: [
      { name: "^(bash|powershell)$", command: String.raw`azd (down|up|provision|deploy)(?!\S)` },
      { name: "^edit$", path: String.raw`(^|/)azure\.yaml$` },
    ],
  };
  const calls = [{ id: "doctor", name: "bash", arguments: { command: "azd ai agent doctor" } }];
  const evaluate = (extra) => grade(root, "tool-calls", config, completedContext([...calls, { ...extra, id: "extra" }]));
  assert.equal(evaluate({ name: "bash", arguments: { command: "azd downgrade" } }).passed, true);
  assert.equal(evaluate({ name: "bash", arguments: { command: "azd down --force" } }).passed, false);
  assert.equal(evaluate({ name: "view", arguments: { path: "/project/azure.yaml" } }).passed, true);
  assert.equal(evaluate({ name: "edit", arguments: { path: "/project/azure.yaml" } }).passed, false);
  assert.equal(grade(root, "tool-calls", config, completedContext([
    { id: "help-only", name: "bash", arguments: { command: "azd ai agent doctor --help" } },
  ])).passed, false);
});

test("required calls need matching native completion events, not invocation starts", (t) => {
  const root = workspace(t);
  const config = { required: [{ name: "^powershell$", command: "azd ai --help" }] };
  const call = { id: "help", name: "powershell", arguments: { command: "azd ai --help" } };
  const input = {
    ...context,
    tool_calls: [call],
    transcript: [
      { type: "tool.execution_start", tool_call_id: "help" },
      { type: "assistant.message", content: "tool.execution_complete help" },
      { type: "tool.execution_complete", tool_call_id: "another-call" },
    ],
    tool_events: [{ type: "tool.execution_complete", tool_call_id: "help" }],
  };
  assert.equal(grade(root, "tool-calls", config, input).passed, false);
  assert.equal(grade(root, "tool-calls", config, { ...input, transcript: [] }).passed, false);
  assert.equal(grade(root, "tool-calls", config, completedContext([call])).passed, true);
});

test("unfinished disallowed calls do not count until completion, including failed completions", (t) => {
  const root = workspace(t);
  const config = { disallowed: [{ name: "^powershell$", command: String.raw`azd down(?!\S)` }] };
  const input = {
    ...context,
    tool_calls: [{ id: "down", name: "powershell", arguments: { command: "azd down" } }],
    transcript: [{ type: "tool.execution_start", tool_call_id: "down" }],
  };
  assert.equal(grade(root, "tool-calls", config, input).passed, true);
  for (const success of [true, false]) {
    assert.equal(grade(root, "tool-calls", config, {
      ...input, transcript: [...input.transcript, { type: "tool.execution_complete", tool_call_id: "down", success }],
    }).passed, false);
  }
});

test("required calls count completed failures without requiring a successful result", (t) => {
  const root = workspace(t);
  const config = { required: [{ name: "^powershell$", command: "azd ai agent doctor" }] };
  const input = {
    ...context,
    tool_calls: [{ id: "doctor", name: "powershell", arguments: { command: "azd ai agent doctor" } }],
    transcript: [{ type: "tool.execution_complete", tool_call_id: "doctor", success: false }],
  };
  assert.equal(grade(root, "tool-calls", config, input).passed, true);
});

test("empty valid completion evidence means no completed calls", (t) => {
  const root = workspace(t);
  const rule = { name: "^powershell$", command: "azd down" };
  assert.equal(grade(root, "tool-calls", { required: [rule] }).passed, false);
  assert.equal(grade(root, "tool-calls", { disallowed: [rule] }).passed, true);
});

test("tool-call checks require well-formed native completion evidence and call identities", (t) => {
  const root = workspace(t);
  const config = { disallowed: [{ name: "^powershell$", command: "azd down" }] };
  for (const transcript of [undefined, null, {}, "tool.execution_complete"]) {
    assert.throws(() => grade(root, "tool-calls", config, { ...context, transcript }), /context.transcript must be an array/);
  }
  for (const event of [null, [], "tool.execution_complete", {}, { type: 5 }, { type: "" }]) {
    assert.throws(() => grade(root, "tool-calls", config, { ...context, transcript: [event] }), /transcript event must be/);
  }
  for (const id of [undefined, null, "", " ", 5]) {
    assert.throws(() => grade(root, "tool-calls", config, {
      ...context, tool_calls: [{ id, name: "powershell", arguments: { command: "azd down" } }],
    }), /tool call must have a nonblank string id/);
    assert.throws(() => grade(root, "tool-calls", config, {
      ...context, transcript: [{ type: "tool.execution_complete", tool_call_id: id }],
    }), /execution_complete event must have a nonblank string tool_call_id/);
  }
  assert.throws(() => grade(root, "tool-calls", config, {
    ...context, tool_calls: [null],
  }), /tool call must have a nonblank string id/);
  assert.throws(() => grade(root, "tool-calls", config, completedContext([
    { id: "duplicate", name: "view" }, { id: "duplicate", name: "powershell" },
  ])), /Duplicate recorded tool call id/);
});

test("tool-call checks reject unsupported config keys, invalid lists, and empty policies", (t) => {
  const root = workspace(t);
  const rule = { name: "^powershell$" };
  for (const key of ["sequence", "parallel", "unsupported"]) {
    assert.throws(() => grade(root, "tool-calls", { required: [rule], [key]: [] }), /Unsupported tool-calls config key/);
  }
  for (const config of [{}, { required: [] }, { disallowed: [] }, { required: [], disallowed: [] }]) {
    assert.throws(() => grade(root, "tool-calls", config), /at least one required or disallowed rule/);
  }
  for (const key of ["required", "disallowed"]) {
    for (const rules of [null, {}, "powershell"]) {
      assert.throws(() => grade(root, "tool-calls", { [key]: rules }), /must be an array/);
    }
  }
  for (const config of [[], "powershell"]) {
    assert.throws(() => grade(root, "tool-calls", config), /config must be an object/);
  }
});

test("tool-call path patterns inspect raw arguments without separator normalization", (t) => {
  const root = workspace(t);
  const config = { required: [{ name: "^view$", path: String.raw`observe/observe\.md$` }] };
  for (const [path, passed] of [
    ["/skills/observe/observe.md", true],
    [String.raw`C:\skills\observe\observe.md`, false],
  ]) {
    assert.equal(grade(root, "tool-calls", config, completedContext([
      { id: "read", name: "view", arguments: { path } },
    ])).passed, passed);
  }
});

test("fixture diff compares the complete regular-file tree including dotfiles", async (t) => {
  const baseline = [{ path: "azure.yaml", content: "original\n" }, { path: "src/.hidden", content: "secret fixture" }];
  for (const scenario of ["unchanged", "added", "removed", "changed", "dotfile", "nested", "binary"]) {
    await t.test(scenario, (t) => {
      const root = workspace(t);
      for (const entry of baseline) write(root, entry.path, entry.content);
      write(root, ".git/ignored-metadata", "not part of fixture");
      if (scenario === "added") write(root, "extra.txt", "extra");
      if (scenario === "removed") rmSync(join(root, "azure.yaml"));
      if (scenario === "changed") write(root, "azure.yaml", "original\r\n");
      if (scenario === "dotfile") write(root, ".env", "added");
      if (scenario === "nested") write(root, "new/subdir/file.txt", "added");
      if (scenario === "binary") write(root, "azure.yaml", Buffer.from([0xff, 0xfe]));
      const evaluation = grade(root, "diff-empty", {}, context, { baseline });
      assert.equal(evaluation.passed, scenario === "unchanged");
      if (scenario !== "unchanged") assert.match(evaluation.evidence, /Added file:|Removed file:|Changed file:/);
    });
  }
});

test("empty fixture diff requires an actually empty file tree", (t) => {
  const root = workspace(t);
  assert.equal(grade(root, "diff-empty", {}, context, { baseline: [] }).passed, true);
  write(root, ".git", "gitdir: ignored metadata");
  assert.equal(grade(root, "diff-empty", {}, context, { baseline: [] }).passed, true);
  write(root, ".unexpected", "");
  assert.equal(grade(root, "diff-empty", {}, context, { baseline: [] }).passed, false);
});

test("symlinks and directory junctions cannot hide fixture mutations or escape file checks", (t) => {
  const root = workspace(t);
  write(root, "target/file.txt", "content");
  symlinkSync(join(root, "target"), join(root, "linked"), process.platform === "win32" ? "junction" : "dir");
  const baseline = [{ path: "target/file.txt", content: "content" }];
  const evaluation = grade(root, "diff-empty", {}, context, { baseline });
  assert.equal(evaluation.passed, false);
  assert.match(evaluation.evidence, /Symbolic links are not allowed: linked/);
  assert.equal(grade(root, "file-exists", { path: "linked/file.txt" }).passed, false);
  assert.equal(grade(root, "file-not-matches", { path: "linked/file.txt", pattern: "absent" }).passed, false);
});

test("git diff uses only the original repository and reports tracked, staged and untracked changes", async (t) => {
  const root = workspace(t);
  const repository = join(root, "repository");
  mkdirSync(repository);
  const git = (...args) => execFileSync("git", args, { cwd: repository, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
  git("init", "--quiet");
  git("config", "--local", "core.autocrlf", "false");
  git("config", "--local", "core.safecrlf", "false");
  write(repository, "tracked.txt", "original\n");
  git("add", ".");
  git("-c", "user.name=Grader Test", "-c", "user.email=grader@example.invalid", "commit", "--quiet", "-m", "fixture");
  const evaluate = (extra = {}) => grade(root, "diff-empty", {}, context, extra);
  assert.equal(evaluate().passed, true);
  write(root, "waza-runtime-file.json", "outer runtime file");
  assert.equal(evaluate({ repository: "repository" }).passed, true);
  for (const change of ["tracked", "staged", "untracked"]) {
    await t.test(change, () => {
      if (change === "tracked") write(repository, "tracked.txt", "changed\n");
      if (change === "staged") {
        write(repository, "tracked.txt", "staged\n");
        git("add", "tracked.txt");
      }
      if (change === "untracked") write(repository, "nested/untracked.txt", "new\n");
      const evaluation = evaluate({ repository: "repository" });
      assert.equal(evaluation.passed, false);
      assert.match(evaluation.evidence, /tracked\.txt/);
      if (change === "untracked") assert.match(evaluation.evidence, /nested\/untracked\.txt/);
      git("reset", "--hard", "--quiet", "HEAD");
    });
  }
});

test("git diff rejects missing repositories and ordinary folders inside the outer worktree", (t) => {
  const root = workspace(t);
  assert.equal(grade(root, "diff-empty").passed, false);
  mkdirSync(join(root, "repository"));
  const evaluation = grade(root, "diff-empty");
  assert.equal(evaluation.passed, false);
  assert.match(evaluation.evidence, /no regular .git metadata/);
  assert.throws(() => grade(root, "diff-empty", {}, context, { repository: ".." }), /inside the workspace/);
});

test("git diff supports real worktrees with .git files and explicit repository paths", (t) => {
  const root = workspace(t);
  const source = join(root, "source");
  mkdirSync(source);
  const git = (...args) => execFileSync("git", args, { cwd: source, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"] });
  git("init", "--quiet");
  git("config", "--local", "core.autocrlf", "false");
  git("config", "--local", "core.safecrlf", "false");
  write(source, "tracked.txt", "original\n");
  git("add", ".");
  git("-c", "user.name=Grader Test", "-c", "user.email=grader@example.invalid", "commit", "--quiet", "-m", "fixture");
  const worktree = join(root, "original-repository");
  git("worktree", "add", "--quiet", "--detach", worktree);
  const check = { type: "diff-empty", repository: "original-repository", baseline: [] };
  assert.equal(evaluateCheck(check, context, root).passed, true);
  write(worktree, "new/nested-file.txt", "new\n");
  const evaluation = evaluateCheck(check, context, root);
  assert.equal(evaluation.passed, false);
  assert.match(evaluation.evidence, /new\/nested-file\.txt/);
});

test("summary faithfully retains fractional scores and the original subcheck set", async (t) => {
  const cases = [
    { label: "complete", content: "Total notes: 2\n- one\n- two\n", tools: true, score: 1, passed: true },
    { label: "blank lines and CRLF", content: "\r\n \r\nTotal notes: 2  \r\n\r\n- one\r\n- two\r\n", tools: true, score: 1, passed: true },
    { label: "zero notes", content: "Total notes: 0\n", tools: true, score: 1, passed: true },
    { label: "no recorded tools", content: "Total notes: 1\n- one\n", tools: false, score: 3 / 4 },
    { label: "wrong bullet count", content: "Total notes: 2\n- one\n", tools: true, score: 3 / 4 },
    { label: "invalid header", content: "Notes: 2\n- one\n- two\n", tools: true, score: 2 / 4 },
    { label: "indented header is invalid", content: " Total notes: 1\n- one\n", tools: true, score: 2 / 4 },
    { label: "header must be first", content: "Notes\nTotal notes: 1\n- one\n", tools: true, score: 2 / 4 },
    { label: "negative total", content: "Total notes: -1\n", tools: true, score: 2 / 4 },
    { label: "empty summary", content: "\n \n", tools: false, score: 1 / 4 },
    { label: "not every hyphen is a bullet", content: "Total notes: 2\n-one\n- \n  - indented\n- valid\n", tools: true, score: 3 / 4 },
    { label: "missing summary with tools", tools: true, score: 1 / 2 },
    { label: "missing summary without tools", tools: false, score: 0 },
  ];
  for (const item of cases) {
    await t.test(item.label, (t) => {
      const root = workspace(t);
      if (item.content !== undefined) write(root, "notes-summary.md", item.content);
      const evaluation = evaluateCheck(summaryCheck, {
        output: "Total notes: 2\n- one\n- two\n",
        tool_calls: item.tools ? [{ name: "view", arguments: { path: "team-notes.txt" } }] : [],
        trajectory: { metrics: { toolCallCount: 999 } },
      }, root);
      assert.equal(evaluation.name, "summary");
      assert.equal(evaluation.passed, item.passed ?? false);
      assert.equal(evaluation.score, item.score);
      assert.equal(evaluation.details.length, item.content === undefined ? 2 : 4);
      assert.equal(evaluation.details[0].name, "summary-file-exists");
      assert.equal(evaluation.details.at(-1).name, "agent-used-tools");
      assert.equal(evaluation.details.at(-1).passed, item.tools);
    });
  }
});

test("summary agent-used-tools still counts unfinished invocation starts", (t) => {
  const root = workspace(t);
  write(root, "notes-summary.md", "Total notes: 1\n- one\n");
  const input = {
    ...context,
    tool_calls: [{ id: "read", name: "view", arguments: { path: "team-notes.txt" } }],
    transcript: [{ type: "tool.execution_start", tool_call_id: "read" }],
  };
  const evaluation = evaluateCheck(summaryCheck, input, root);
  assert.equal(evaluation.passed, true);
  assert.equal(evaluation.score, 1);
  assert.equal(evaluation.details.at(-1).name, "agent-used-tools");
  assert.equal(evaluation.details.at(-1).passed, true);
});

test("invalid inputs and unsupported graders never silently succeed", (t) => {
  const root = workspace(t);
  assert.throws(() => evaluateCheck({}, context, root), /with a type/);
  assert.throws(() => evaluateCheck({ type: "output-not-matches" }, null, root), /context must be/);
  assert.throws(() => grade(root, "skill-invocation"), /Unsupported source grader/);
  assert.throws(() => grade(root, "program", { program: "node", args: ["other.ts"] }), /Only the source/);
  assert.throws(() => grade(root, "tool-calls", { required: [{ command: "azd" }] }), /name pattern/);
  assert.throws(() => grade(root, "diff-empty", {}, context, { baseline: null }), /must be an array/);
  assert.throws(() => grade(root, "diff-empty", {}, context, { baseline: [{ path: "../outside", content: "" }] }), /inside/);
  assert.throws(() => evaluateCheck({ type: "output-matches", config: { pattern: "" } }, context, ""), /root is required/);
});

function invoke(root, check, input = context, changes = {}) {
  const contextPath = join(root, "grading-context.json");
  writeFileSync(contextPath, JSON.stringify(input));
  const env = { ...process.env, WAZA_WORKSPACE_DIR: root, WAZA_GRADING_CONTEXT: contextPath };
  for (const [key, value] of Object.entries(changes)) {
    if (value === undefined) delete env[key];
    else env[key] = value;
  }
  return spawnSync(process.execPath, [script, typeof check === "string" ? check : JSON.stringify(check)], {
    cwd: resolve(here, ".."),
    env,
    input: "stdin is not the final output",
    encoding: "utf8",
  });
}

test("CLI implements native Waza JSON, exit status, and final-output contracts", (t) => {
  const root = workspace(t);
  const check = { type: "output-matches", name: "native check", config: { pattern: "^real final output$" } };
  const passed = invoke(root, check, { ...context, output: "real final output" });
  assert.equal(passed.error, undefined);
  assert.equal(passed.status, 0);
  assert.equal(passed.stderr, "");
  assert.deepEqual(JSON.parse(passed.stdout), {
    name: "native check", passed: true, score: 1, evidence: 'Output matches "^real final output$"',
  });
  const failed = invoke(root, check);
  assert.equal(failed.status, 1);
  assert.equal(JSON.parse(failed.stdout).passed, false);
  const missing = invoke(root, { type: "file-not-matches", config: { path: "missing", pattern: "forbidden" } });
  assert.equal(missing.status, 1);
  assert.equal(missing.stderr, "");
  assert.match(JSON.parse(missing.stdout).evidence, /does not exist/);
  write(root, "notes-summary.md", "Total notes: 1\n- one\n");
  const partial = invoke(root, summaryCheck);
  assert.equal(partial.status, 1);
  assert.equal(JSON.parse(partial.stdout).score, 3 / 4);
});

test("CLI infrastructure failures are explicit on stderr and nonzero", (t) => {
  const root = workspace(t);
  const check = { type: "output-not-matches", config: { pattern: "forbidden" } };
  for (const env of [
    { WAZA_WORKSPACE_DIR: undefined },
    { WAZA_GRADING_CONTEXT: undefined },
    { WAZA_GRADING_CONTEXT: join(root, "absent.json") },
    { WAZA_WORKSPACE_DIR: join(root, "absent-directory") },
  ]) {
    const failure = invoke(root, check, context, env);
    assert.equal(failure.status, 1);
    assert.equal(failure.stdout, "");
    assert.match(failure.stderr, /vally-check:/);
  }
  for (const [specification, input] of [["{invalid", context], [check, null], [check, {}]]) {
    const failure = invoke(root, specification, input);
    assert.equal(failure.status, 1);
    assert.equal(failure.stdout, "");
    assert.match(failure.stderr, /vally-check:/);
  }
  write(root, "broken-context.json", "{not JSON");
  const malformed = invoke(root, check, context, { WAZA_GRADING_CONTEXT: join(root, "broken-context.json") });
  assert.equal(malformed.status, 1);
  assert.equal(malformed.stdout, "");
  assert.match(malformed.stderr, /vally-check:/);
  const missingCompletions = invoke(root, {
    type: "tool-calls", config: { disallowed: [{ name: "^powershell$", command: "azd down" }] },
  }, { output: "", tool_calls: [] });
  assert.equal(missingCompletions.status, 1);
  assert.equal(missingCompletions.stdout, "");
  assert.match(missingCompletions.stderr, /vally-check: Grading context.transcript must be an array/);
});

test("importing the evaluator does not execute its CLI", () => {
  const env = { ...process.env };
  delete env.WAZA_WORKSPACE_DIR;
  delete env.WAZA_GRADING_CONTEXT;
  const imported = spawnSync(process.execPath, ["--input-type=module", "-e",
    'await import("./graders/vally-check.mjs")'], {
    cwd: resolve(here, ".."), env, encoding: "utf8",
  });
  assert.equal(imported.status, 0);
  assert.equal(imported.stdout, "");
  assert.equal(imported.stderr, "");
});
