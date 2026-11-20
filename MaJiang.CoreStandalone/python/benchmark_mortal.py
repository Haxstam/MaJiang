"""Measure complete CUDA matches, including logging, without creating formal samples.

Only Python's standard library is used. GPU memory/utilization includes other apps.
Run after building MaJiang.MortalConsole in Release configuration.
"""
from __future__ import annotations

import argparse
import ctypes
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys
import time


def gpu_sample():
    result = subprocess.run(
        ["nvidia-smi", "--query-gpu=memory.total,memory.used,utilization.gpu", "--format=csv,noheader,nounits"],
        capture_output=True, text=True, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), check=True)
    total, used, utilization = map(int, result.stdout.strip().splitlines()[0].split(","))
    return {"totalMiB": total, "usedMiB": used, "utilizationPercent": utilization}


def available_memory_mib():
    class MemoryStatus(ctypes.Structure):
        _fields_ = [("length", ctypes.c_ulong), ("load", ctypes.c_ulong)] + [
            (name, ctypes.c_ulonglong) for name in ("totalPhysical", "availablePhysical", "totalPageFile",
                                                   "availablePageFile", "totalVirtual", "availableVirtual", "extended")]
    status = MemoryStatus()
    status.length = ctypes.sizeof(status)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(status))
    return status.availablePhysical // (1024 * 1024)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--concurrency", type=int, choices=(1, 2, 3), default=1)
    parser.add_argument("--rounds", type=int, default=100)
    parser.add_argument("--seed", type=int, default=2)
    parser.add_argument("--python", default=sys.executable)
    parser.add_argument("--model-dir", default=r"G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42")
    parser.add_argument("--output", required=True)
    parser.add_argument("--timeout", type=int, default=1200)
    args = parser.parse_args()
    project = Path(__file__).resolve().parent.parent
    output = Path(args.output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    native_tag = f"cp{sys.version_info.major}{sys.version_info.minor}-win_amd64"
    common = ["dotnet", str(project / "examples/MaJiang.MortalConsole/bin/Release/net10.0/MaJiang.MortalConsole.dll"),
              "--python", args.python, "--model-dir", args.model_dir,
              "--bridge", str(project / "python/mortal_bridge.py"),
              "--native-dir", str(project / ".utmp/python-native" / native_tag),
              "--device", "cuda", "--threads", "2", "--seats", "0,1,2,3", "--curses", "1,2,3,4",
              "--audit", "--purpose", "benchmark", "--rounds", str(args.rounds), "--output", str(output / "matches")]
    baseline = gpu_sample()
    started = datetime.now(timezone.utc).isoformat()
    available = available_memory_mib()
    origin = time.perf_counter()
    workers, samples = [], []
    try:
        for index in range(args.concurrency):
            stdout = open(output / f"worker-{index}.stdout.log", "w", encoding="utf-8")
            stderr = open(output / f"worker-{index}.stderr.log", "w", encoding="utf-8")
            command = common + ["--seed", str(args.seed + index)]
            process = subprocess.Popen(command, stdout=stdout, stderr=stderr, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            workers.append({"process": process, "stdout": stdout, "stderr": stderr, "command": command,
                            "startSeconds": time.perf_counter() - origin, "endSeconds": None})
        while True:
            elapsed = time.perf_counter() - origin
            for worker in workers:
                if worker["endSeconds"] is None and worker["process"].poll() is not None:
                    worker["endSeconds"] = elapsed
            if all(worker["endSeconds"] is not None for worker in workers):
                break
            if elapsed > args.timeout:
                raise TimeoutError("Benchmark exceeded its host timeout.")
            samples.append({"elapsedSeconds": elapsed, **gpu_sample(), "availableRamMiB": available_memory_mib()})
            time.sleep(0.5)
    finally:
        for worker in workers:
            if worker["process"].poll() is None:
                subprocess.run(["taskkill", "/PID", str(worker["process"].pid), "/T", "/F"], capture_output=True,
                               creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), check=False)
                worker["process"].wait()
            worker["stdout"].close()
            worker["stderr"].close()
    elapsed = time.perf_counter() - origin
    summaries = []
    for index, worker in enumerate(workers):
        lines = (output / f"worker-{index}.stdout.log").read_text(encoding="utf-8-sig").splitlines()
        summary = next((json.loads(line) for line in reversed(lines) if line.startswith('{"result":')), None)
        summaries.append({"index": index, "exitCode": worker["process"].returncode,
                          "startSeconds": worker["startSeconds"], "endSeconds": worker["endSeconds"],
                          "wallSeconds": worker["endSeconds"] - worker["startSeconds"],
                          "command": worker["command"], "summary": summary})
    valid = [worker for worker in summaries if worker["summary"] is not None and worker["summary"]["result"] == "pass"]
    total_decisions = sum(worker["summary"]["modelDecisions"] for worker in valid)
    # Match startup is excluded here; other workers may still be loading during this interval.
    ready = [worker["startSeconds"] + worker["summary"]["timing"]["startupSeconds"] for worker in valid]
    finished_play = [worker["startSeconds"] + worker["summary"]["timing"]["startupSeconds"] + worker["summary"]["timing"]["playSeconds"] for worker in valid]
    play_span = max(finished_play) - min(ready) if valid else 0
    report = {
        "schemaVersion": 1, "purpose": "benchmark", "startedAtUtc": started,
        "concurrency": args.concurrency, "python": args.python, "roundLimit": args.rounds,
        "baselineGpu": baseline, "baselineAvailableRamMiB": available,
        "wallSecondsIncludingStartupAndCleanup": elapsed,
        "successfulWorkers": len(valid), "completedMatches": sum(worker["summary"]["matchEnded"] for worker in valid),
        "totalModelDecisions": total_decisions,
        "aggregateDecisionsPerWallSecond": total_decisions / elapsed,
        "playSpanSeconds": play_span, "aggregateDecisionsPerPlaySpanSecond": total_decisions / play_span if play_span else 0,
        "peakGlobalGpuMiB": max((sample["usedMiB"] for sample in samples), default=baseline["usedMiB"]),
        "averageSampledGpuUtilizationPercent": sum(sample["utilizationPercent"] for sample in samples) / len(samples) if samples else None,
        "minimumAvailableRamMiB": min((sample["availableRamMiB"] for sample in samples), default=available),
        "workers": summaries, "resources": samples,
        "recordingBytes": sum(path.stat().st_size for path in (output / "matches").rglob("*") if path.is_file())
    }
    (output / "benchmark.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({key: value for key, value in report.items() if key not in ("workers", "resources")}, ensure_ascii=False), flush=True)
    return 0 if len(valid) == args.concurrency else 1


if __name__ == "__main__":
    raise SystemExit(main())
