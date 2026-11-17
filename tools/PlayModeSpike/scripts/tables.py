# Builds the markdown tables of the report from the runs' stats files.
import json, os, sys
root = sys.argv[1]
def load(p):
    return json.load(open(p)) if os.path.exists(p) else None
def f(x, d=2):
    return "-" if x is None else f"{x:.{d}f}"
out = []
for g in ["FpsGame", "RpgGame"]:
    s = load(f"{root}/{g}-session/stats.json")
    if not s:
        continue
    picks = open(f"{root}/{g}-session/picked-entities.txt").read().strip().splitlines()
    out.append(f"\n### {g}: picked entities\n")
    for p in picks:
        out.append(f"- {p}")
    W = {w["label"]: w for w in s["windows"]}
    # Per entity, both formats, from the every-frame windows.
    out.append(f"\n### {g}: snapshot cost per entity (agent side, every-frame window)\n")
    out.append("| Entity | Binary mean µs | Binary p95 µs | Binary alloc B | Binary size B | YAML mean µs | YAML p95 µs | YAML alloc B | YAML size B | YAML/binary time |")
    out.append("|---|---|---|---|---|---|---|---|---|---|")
    names = [p.split(": ",1)[1].rsplit(" ",3)[0] for p in picks]
    for name in names:
        b = W["binary every frame"]["series"].get(f"entity Binary {name}")
        y = W["yaml every frame"]["series"].get(f"entity Yaml {name}")
        if not b or not y:
            continue
        out.append(f"| {name} | {f(b['us']['mean'],1)} | {f(b['us']['p95'],1)} | {b['allocatedBytes']['mean']:.0f} | {b['bytes']['mean']:.0f} | {f(y['us']['mean'],1)} | {f(y['us']['p95'],1)} | {y['allocatedBytes']['mean']:.0f} | {y['bytes']['mean']:.0f} | {y['us']['mean']/b['us']['mean']:.0f}x |")
    out.append(f"\n### {g}: whole snapshot of the {len(names)} entities\n")
    out.append("| Window | Snapshots | Mean µs | p95 µs | p99 µs | Max µs | Alloc B per snapshot | Message B |")
    out.append("|---|---|---|---|---|---|---|---|")
    for label in ["binary at 5 Hz", "binary every frame", "yaml at 5 Hz", "yaml every frame"]:
        ser = [v for k, v in W[label]["series"].items() if k.startswith("snapshot ")]
        if ser:
            v = ser[0]
            out.append(f"| {label} | {v['us']['count']} | {f(v['us']['mean'],0)} | {f(v['us']['p95'],0)} | {f(v['us']['p99'],0)} | {f(v['us']['max'],0)} | {v['allocatedBytes']['mean']:.0f} | {v['bytes']['mean']:.0f} |")
    out.append(f"\n### {g}: frames\n")
    out.append("| Condition | Frames | Frame mean ms | Frame p95 ms | Update phase mean ms | Update p95 ms | Draw phase mean ms | Agent per frame mean µs | Agent p95 µs | Agent max µs | Allocated KB/s | Gen0 GCs |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|---|")
    rows = []
    for b in ["baseline-1", "baseline-2"]:
        bs = load(f"{root}/{g}-{b}/baseline-stats.json")
        if bs:
            rows.append((f"no agent (meter only), run {b[-1]}", bs))
    for label in ["connected, idle", "binary at 5 Hz", "binary every frame", "yaml at 5 Hz", "yaml every frame"]:
        rows.append((label, W[label]))
    for label, w in rows:
        sec = w["seconds"]
        out.append(f"| {label} | {w['frameMs']['count']} | {f(w['frameMs'].get('mean'),1)} | {f(w['frameMs'].get('p95'),1)} | {f(w['updatePhaseMs'].get('mean'))} | {f(w['updatePhaseMs'].get('p95'))} | {f(w['drawPhaseMs'].get('mean'),1)} | {f(w['agentUs'].get('mean'),1)} | {f(w['agentUs'].get('p95'),1)} | {f(w['agentUs'].get('max'),0)} | {w['allocatedBytes']/1024/sec:.0f} | {w['gen0']} |")
    out.append(f"\n### {g}: components, mean µs per snapshot (every-frame windows)\n")
    out.append("| Component type | Binary µs | YAML µs |")
    out.append("|---|---|---|")
    comps = {}
    for fmt in ["Binary", "Yaml"]:
        for k, v in W[f"{fmt.lower()} every frame"]["series"].items():
            if k.startswith(f"component {fmt} "):
                comps.setdefault(k.split(" ", 2)[2], {})[fmt] = v["us"]["mean"]
    for c, v in sorted(comps.items(), key=lambda kv: -kv[1].get("Yaml", 0)):
        out.append(f"| {c} | {f(v.get('Binary'),1)} | {f(v.get('Yaml'),1)} |")
    out.append(f"\n### {g}: editor side, decoding the snapshots\n")
    out.append("| Format | Snapshots | Mean ms | p95 ms | Failed components |")
    out.append("|---|---|---|---|---|")
    for k, v in s["decode"].items():
        if k != "failures":
            out.append(f"| {k} | {v['snapshots']} | {v['meanMs']} | {v['p95Ms']} | {v['failedComponents']} |")
    # Startup
    out.append(f"\n### {g}: start\n")
    hello = load(f"{root}/{g}-session/hello.json")
    b1 = load(f"{root}/{g}-baseline-1/baseline-stats.json"); b2 = load(f"{root}/{g}-baseline-2/baseline-stats.json")
    con = open(f"{root}/{g}-session.console.txt").read().splitlines()
    out.append(f"- process start to first frame: meter only {f(b1 and b1['startToFirstFrameMs'],0)} / {f(b2 and b2['startToFirstFrameMs'],0)} ms; with the agent {f(W['connected, idle']['startToFirstFrameMs'],0)} ms")
    out.append(f"- startup hook to Game.GameStarted: {f(hello['hookToGameStartedMs'],0)} ms; console: {'; '.join(l for l in con if l.startswith(('Connected','Tree')))}")
    out.append(f"- assemblies loaded from the agent's folder: {', '.join(W['yaml every frame']['resolvedFromAgent'])}")
    a = load(f"{root}/{g}-activity/stats.json")
    if a:
        for w in a["windows"]:
            out.append(f"- activity ({w['label']}): {w['treeBatches']} batches, {w['treeOps']} ops sent, {w['treeOpsCoalesced']} coalesced, {w['idCollisions']} id collisions, {w['sharedIdEntities']} live entities sharing an id at the last poll, {w['droppedMessages']} dropped messages")
print("\n".join(out))
