// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { execFileSync } from "node:child_process";
import { lstatSync, readFileSync, readdirSync } from "node:fs";
import { isAbsolute, join, relative, resolve, sep, win32 } from "node:path";
import { fileURLToPath } from "node:url";

export function compilePattern(pattern) {
  if (typeof pattern !== "string") {
    throw new TypeError("Regex pattern must be a string");
  }
  const flags = new Set();
  let source = pattern;
  let leading;
  while ((leading = /^\(\?([ims]+)\)/.exec(source))) {
    for (const flag of leading[1]) flags.add(flag);
    source = source.slice(leading[0].length);
  }
  let inClass = false;
  for (let i = 0; i < source.length; i++) {
    if (source[i] === "\\") {
      i++;
    } else if (source[i] === "[") {
      inClass = true;
    } else if (source[i] === "]") {
      inClass = false;
    } else if (!inClass && source.startsWith("(?", i) && /^\(\?[a-zA-Z-]+[:)]/.test(source.slice(i))) {
      throw new SyntaxError(`Unsupported embedded or scoped regex flags in ${JSON.stringify(pattern)}`);
    }
  }
  return new RegExp(source, [...flags].join(""));
}

function result(name, passed, evidence) {
  return { name, passed, score: passed ? 1 : 0, evidence };
}

function workspacePath(workspace, path) {
  if (typeof path !== "string" || !path || isAbsolute(path) || win32.isAbsolute(path)) {
    throw new Error(`Expected a relative workspace path, got ${JSON.stringify(path)}`);
  }
  const target = resolve(workspace, path.replace(/[\\/]/g, sep));
  const local = relative(workspace, target);
  if (!local || local === ".." || local.startsWith(`..${sep}`) || isAbsolute(local)) {
    throw new Error(`Path must stay inside the workspace: ${JSON.stringify(path)}`);
  }
  return target;
}

function inspectPath(workspace, target, type) {
  let current = workspace;
  for (const part of relative(workspace, target).split(sep)) {
    current = join(current, part);
    const stat = lstatSync(current, { throwIfNoEntry: false });
    if (!stat) return `File does not exist: ${current}`;
    if (stat.isSymbolicLink()) return `Symbolic links are not allowed: ${current}`;
    if (current !== target && !stat.isDirectory()) return `Not a directory: ${current}`;
    if (current === target && !(type === "directory" ? stat.isDirectory() : stat.isFile())) {
      return `Not a regular ${type}: ${current}`;
    }
  }
  return undefined;
}

function toolCalls(context) {
  if (!Array.isArray(context.tool_calls)) {
    throw new TypeError("Grading context.tool_calls must be an array of recorded tool calls");
  }
  return context.tool_calls;
}

function completedToolCalls(context) {
  const calls = toolCalls(context);
  if (!Array.isArray(context.transcript)) {
    throw new TypeError("Grading context.transcript must be an array of native events for tool-call completion evidence");
  }
  const identities = new Set();
  for (const call of calls) {
    if (!call || typeof call.id !== "string" || !call.id.trim()) {
      throw new TypeError("Each recorded tool call must have a nonblank string id for completion matching");
    }
    if (identities.has(call.id)) throw new Error(`Duplicate recorded tool call id: ${call.id}`);
    identities.add(call.id);
  }
  const completed = new Set();
  for (const event of context.transcript) {
    if (!event || typeof event !== "object" || Array.isArray(event) ||
        typeof event.type !== "string" || !event.type.trim()) {
      throw new TypeError("Each grading context.transcript event must be an object with a nonblank string type");
    }
    if (event.type === "tool.execution_complete") {
      if (typeof event.tool_call_id !== "string" || !event.tool_call_id.trim()) {
        throw new TypeError("Each tool.execution_complete event must have a nonblank string tool_call_id");
      }
      completed.add(event.tool_call_id);
    }
  }
  return calls.filter((call) => completed.has(call.id));
}

function output(context) {
  if (typeof context.output !== "string") {
    throw new TypeError("Grading context.output must be a string");
  }
  return context.output;
}

function evaluateToolCalls(name, config, context) {
  if (!config || typeof config !== "object" || Array.isArray(config)) {
    throw new TypeError("tool-calls config must be an object");
  }
  for (const key of Object.keys(config)) {
    if (!["required", "disallowed"].includes(key)) throw new Error(`Unsupported tool-calls config key: ${key}`);
  }
  const rulesByKind = ["required", "disallowed"].map((key) => {
    const rules = config[key] === undefined ? [] : config[key];
    if (!Array.isArray(rules)) throw new TypeError(`tool-calls config.${key} must be an array`);
    return rules;
  });
  if (rulesByKind.every((rules) => rules.length === 0)) {
    throw new Error("tool-calls requires at least one required or disallowed rule");
  }
  const calls = completedToolCalls(context);
  const details = [];
  for (const [index, key] of ["required", "disallowed"].entries()) {
    const required = key === "required";
    const rules = rulesByKind[index];
    for (const rule of rules) {
      if (!rule || typeof rule !== "object" || typeof rule.name !== "string") {
        throw new TypeError(`tool-calls ${key} rule must have a name pattern`);
      }
      const patterns = Object.entries(rule).map(([field, pattern]) => {
        if (!["name", "command", "path"].includes(field)) {
          throw new Error(`Unsupported tool-call rule field: ${field}`);
        }
        return [field, compilePattern(pattern)];
      });
      const matched = calls.some((call) => patterns.every(([field, pattern]) => {
        const value = field === "name" ? call?.name : call?.arguments?.[field];
        return typeof value === "string" && pattern.test(value);
      }));
      details.push(result(`${key}: ${JSON.stringify(rule)}`, matched === required,
        `${matched ? "Completed call matched" : "No completed call matched"} ${JSON.stringify(rule)}`));
    }
  }
  return {
    ...result(name, details.every((detail) => detail.passed),
      `${details.filter((detail) => detail.passed).length} of ${details.length} tool-call constraints passed`),
    details,
  };
}

function evaluateFixtureDiff(name, baseline, workspace) {
  if (!Array.isArray(baseline)) throw new TypeError("diff-empty baseline must be an array");
  const expected = new Map();
  for (const entry of baseline) {
    const target = workspacePath(workspace, entry.path);
    const key = relative(workspace, target).split(sep).join("/");
    if (key.split("/").includes(".git")) throw new Error("Baseline must not include .git metadata");
    if (typeof entry.content !== "string") throw new TypeError(`Baseline content must be a string: ${key}`);
    if (expected.has(key)) throw new Error(`Duplicate baseline path: ${key}`);
    expected.set(key, Buffer.from(entry.content, "utf8"));
  }
  const changes = [];
  function walk(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name);
      const key = relative(workspace, path).split(sep).join("/");
      if (entry.isSymbolicLink()) {
        changes.push(`Symbolic links are not allowed: ${key}`);
      } else if (entry.name === ".git") {
        continue;
      } else if (entry.isDirectory()) {
        walk(path);
      } else if (entry.isFile()) {
        const original = expected.get(key);
        if (original === undefined) changes.push(`Added file: ${key}`);
        else if (!readFileSync(path).equals(original)) changes.push(`Changed file: ${key}`);
        expected.delete(key);
      } else {
        changes.push(`Not a regular file: ${key}`);
      }
    }
  }
  walk(workspace);
  for (const key of expected.keys()) changes.push(`Removed file: ${key}`);
  return result(name, changes.length === 0, changes.length ? changes.join("\n") : "Workspace matches the fixture baseline");
}

function evaluateGitDiff(name, repository, workspace) {
  const path = workspacePath(workspace, repository);
  const error = inspectPath(workspace, path, "directory");
  if (error) return result(name, false, error);
  const metadata = lstatSync(join(path, ".git"), { throwIfNoEntry: false });
  if (!metadata || metadata.isSymbolicLink() || !(metadata.isDirectory() || metadata.isFile())) {
    return result(name, false, `Repository has no regular .git metadata: ${path}`);
  }
  const status = execFileSync("git", ["status", "--porcelain", "--untracked-files=all"], {
    cwd: path,
    encoding: "utf8",
    maxBuffer: 16 * 1024 * 1024,
    stdio: ["ignore", "pipe", "pipe"],
  });
  return result(name, status.trim().length === 0, status || "Repository working tree is clean");
}

function evaluateSummary(check, context, workspace) {
  const config = check.config;
  if (config?.program !== "node" || !Array.isArray(config.args) ||
      config.args.length !== 1 || !/^(?:graders[/\\])?check-summary\.ts$/.test(config.args[0])) {
    throw new Error("Only the source check-summary.ts program grader is supported");
  }
  const details = [];
  const add = (name, passed, evidence) => details.push({ ...result(name, passed, evidence), kind: "code" });
  const path = workspacePath(workspace, "notes-summary.md");
  const error = inspectPath(workspace, path, "file");
  add("summary-file-exists", !error, error ?? `${path} exists`);
  if (!error) {
    const lines = readFileSync(path, "utf8").split(/\r?\n/).filter((line) => line.trim().length > 0);
    const header = /^Total notes:\s*(\d+)\s*$/.exec(lines[0] ?? "");
    add("summary-has-total-line", header !== null, header
      ? `First line reports "${lines[0]}"`
      : `Expected first line to match "Total notes: <N>", got ${JSON.stringify(lines[0] ?? "")}`);
    const bullets = lines.slice(1).filter((line) => /^-\s+\S/.test(line)).length;
    const total = header ? Number(header[1]) : undefined;
    add("bullet-count-matches-total", total !== undefined && bullets === total,
      `Found ${bullets} bullet line(s); header reported ${total ?? "<none>"}`);
  }
  const count = toolCalls(context).length;
  add("agent-used-tools", count > 0, `Trajectory recorded ${count} tool call(s)`);
  const passedCount = details.filter((detail) => detail.passed).length;
  const passed = passedCount === details.length;
  return {
    name: check.name ?? "fixture-summary-check",
    passed,
    score: passedCount / details.length,
    evidence: passed ? `All ${details.length} checks passed` : `${details.length - passedCount} of ${details.length} check(s) failed`,
    details,
  };
}

export function evaluateCheck(check, context, workspace) {
  if (!check || typeof check !== "object" || Array.isArray(check) || typeof check.type !== "string") {
    throw new TypeError("Check specification must be an object with a type");
  }
  if (!context || typeof context !== "object" || Array.isArray(context)) {
    throw new TypeError("Grading context must be a JSON object");
  }
  if (typeof workspace !== "string" || !workspace) throw new Error("Workspace root is required");
  workspace = resolve(workspace);
  if (!lstatSync(workspace).isDirectory()) throw new Error(`Workspace root is not a regular directory: ${workspace}`);
  const name = check.name ?? check.type;
  const config = check.config ?? {};
  switch (check.type) {
    case "output-matches":
    case "output-not-matches": {
      const matched = compilePattern(config.pattern).test(output(context));
      return result(name, matched === (check.type === "output-matches"),
        `Output ${matched ? "matches" : "does not match"} ${JSON.stringify(config.pattern)}`);
    }
    case "output-contains": {
      if (typeof config.substring !== "string") throw new TypeError("output-contains requires config.substring");
      const text = output(context);
      const passed = config.case_sensitive === false
        ? text.toLowerCase().includes(config.substring.toLowerCase())
        : text.includes(config.substring);
      return result(name, passed, `Output ${passed ? "contains" : "does not contain"} ${JSON.stringify(config.substring)}`);
    }
    case "file-exists":
    case "file-matches":
    case "file-not-matches": {
      const path = workspacePath(workspace, config.path);
      const error = inspectPath(workspace, path, "file");
      if (error) return result(name, false, error);
      if (check.type === "file-exists") return result(name, true, `${config.path} exists`);
      const matched = compilePattern(config.pattern).test(readFileSync(path, "utf8"));
      return result(name, matched === (check.type === "file-matches"),
        `${config.path} ${matched ? "matches" : "does not match"} ${JSON.stringify(config.pattern)}`);
    }
    case "tool-calls":
      return evaluateToolCalls(name, config, context);
    case "diff-empty":
      return Object.hasOwn(check, "baseline") && !Object.hasOwn(check, "repository")
        ? evaluateFixtureDiff(name, check.baseline, workspace)
        : evaluateGitDiff(name, check.repository ?? "repository", workspace);
    case "program":
      return evaluateSummary(check, context, workspace);
    default:
      throw new Error(`Unsupported source grader type: ${check.type}`);
  }
}

function main() {
  const workspace = process.env.WAZA_WORKSPACE_DIR;
  const contextPath = process.env.WAZA_GRADING_CONTEXT;
  if (!workspace || !contextPath) {
    throw new Error("WAZA_WORKSPACE_DIR and WAZA_GRADING_CONTEXT are required");
  }
  if (!process.argv[2]) throw new Error("A JSON check specification is required as argv[2]");
  const check = JSON.parse(process.argv[2]);
  const context = JSON.parse(readFileSync(contextPath, "utf8"));
  const evaluation = evaluateCheck(check, context, workspace);
  process.stdout.write(`${JSON.stringify(evaluation)}\n`);
  process.exitCode = evaluation.passed ? 0 : 1;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  try {
    main();
  } catch (error) {
    process.stderr.write(`vally-check: ${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  }
}
