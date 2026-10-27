---
name: unity-optimize
description: Measure first, fix the top hotspots one at a time, re-measure and report the delta
disable-model-invocation: true
model: sonnet
argument-hint: "[scene, system or file]"
allowed-tools: mcp__UnityMCP__manage_profiler, mcp__UnityMCP__read_console, mcp__UnityMCP__refresh_unity, Bash(ai-engine/.venv-ai/Scripts/python.exe Tools/run_perf_probe.py:*), Grep, Read, Edit
---

Never optimise without a measurement.

1. Measure a baseline:
   - `mcp__UnityMCP__manage_profiler` (CPU ms, GC alloc per frame, draw calls) on the target; or
   - with no Editor, `Tools/run_perf_probe.py` (FPS, p95 ms, GC bytes). Record the numbers.
2. Find the top 3 hotspots in this order of suspicion:
   - GC allocation in per-frame code (LINQ, closures, string concatenation, `new` in Update);
   - Update loops that could be events;
   - Find or GetComponent per frame;
   - physics (non-alloc queries, layers);
   - draw calls.
   Grep first and read only the ranges you need.
3. Fix one hotspot with the smallest change, then re-measure. Keep the fix only if the delta is positive and the behaviour is unchanged.
4. Repeat for the next hotspot only if you are asked to, or if the first one is done and verified.
5. Output ≤ 15 lines: a before/after table (metric | before | after | delta), plus the files changed. Mark each number as measured; never estimate.
