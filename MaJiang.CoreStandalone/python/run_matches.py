"""Run a bounded batch of full CUDA matches, with durable scheduling and logs.

The first failed/truncated match stops new launches; already running matches finish.
Resume continues pending jobs without overwriting any prior match or process log.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import itertools
import json
from pathlib import Path
import subprocess
import sys
import time

from benchmark_mortal import available_memory_mib, gpu_sample


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def save(path, value):
    temporary = path.with_suffix(path.suffix + ".pending")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")
    for attempt in range(50):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            if attempt == 49:
                raise
            time.sleep(0.1)


def make_schedule(count, first_seed):
    # Six cyclic orbits, each containing all four rotations. Every multiple
    # of four games is seat-balanced, including 100 and 1000.
    permutations = []
    for value in itertools.permutations((1, 2, 3, 4)):
        if value in permutations:
            continue
        permutations.extend(value[offset:] + value[:offset] for offset in range(4))
    return [{"number": index + 1, "seedNumber": first_seed + index, "cursesBySeat": list(permutations[index % 24]),
             "status": "pending", "attempts": []} for index in range(count)]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--count", type=int, default=100)
    parser.add_argument("--concurrency", type=int, choices=(1, 2), default=2)
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--python", default=sys.executable)
    parser.add_argument("--model-dir", default=r"G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42")
    parser.add_argument("--output", required=True)
    parser.add_argument("--purpose", choices=("experiment", "validation"), default="experiment")
    parser.add_argument("--resume", action="store_true")
    parser.add_argument("--match-timeout", type=int, default=600)
    parser.add_argument("--stop-file", help="Graceful stop flag; default is OUTPUT/stop.requested")
    parser.add_argument("--curse-rule-version", type=str.lower, choices=("v1", "v2"), default="v1")
    args = parser.parse_args()
    if args.count < 1 or args.match_timeout < 30 or not -(2**63) <= args.seed <= args.seed + args.count - 1 < 2**63:
        parser.error("Invalid match count, seed range, or timeout.")
    project = Path(__file__).resolve().parent.parent
    output = Path(args.output).resolve()
    stop_file = Path(args.stop_file).resolve() if args.stop_file else output / "stop.requested"
    host = project / "examples/MaJiang.MortalConsole/bin/Release/net10.0/MaJiang.MortalConsole.dll"
    bridge = project / "python/mortal_bridge.py"
    tag = subprocess.check_output([args.python, "-B", "-c", 'import sys; print("cp%d%d-win_amd64" % sys.version_info[:2])'], text=True).strip()
    native = project / ".utmp/python-native" / tag
    artifacts = [host, host.parent / "MaJiang.Core.dll", host.parent / "MaJiang.Mortal.dll", bridge, native / "libriichi.pyd"]
    fingerprints = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in artifacts}
    configuration = {"count": args.count, "concurrency": args.concurrency, "firstSeed": args.seed,
                     "scheduleVersion": "cyclic-orbits-v2",
                     "curseRuleVersion": args.curse_rule_version,
                     "python": args.python, "modelDirectory": args.model_dir, "purpose": args.purpose,
                     "device": "cuda", "threadsPerModel": 2, "roundLimit": 100, "detailedAudit": True,
                     "kokushiRobClosedKanPolicy": "skip", "matchTimeoutSeconds": args.match_timeout,
                     "artifactSha256": fingerprints}
    batch_path = output / "batch.json"
    if args.resume:
        batch = json.loads(batch_path.read_text(encoding="utf-8"))
        if batch["configuration"] != configuration:
            raise ValueError("Resume configuration/artifacts differ from the original batch.")
        if any(job["status"] == "running" for job in batch["jobs"]):
            raise ValueError("Batch has interrupted/running jobs. Inspect its recorded PIDs before resuming.")
        if any(job["status"] in ("failed", "truncated", "interrupted") for job in batch["jobs"]):
            raise ValueError("Inspect failed attempts before continuing; they are never silently replaced.")
    else:
        output.mkdir(parents=True, exist_ok=False)
        (output / "runs").mkdir()
        (output / "matches").mkdir()
        batch = {"schemaVersion": 1, "batchId": output.name, "createdAtUtc": utc_now(),
                 "configuration": configuration, "status": "running", "jobs": make_schedule(args.count, args.seed)}
    active = []
    halted = False
    stopped = False
    last_resource = 0
    origin = time.perf_counter()
    batch["lastStartedAtUtc"] = utc_now()
    batch["runnerPid"] = __import__("os").getpid()
    batch["status"] = "running"

    def persist():
        batch["updatedAtUtc"] = utc_now()
        batch["statusCounts"] = {status: sum(job["status"] == status for job in batch["jobs"])
                                 for status in ("pending", "running", "completed", "failed", "truncated", "interrupted")}
        batch["currentRunElapsedSeconds"] = time.perf_counter() - origin
        save(batch_path, batch)

    persist()
    try:
        while True:
            if stop_file.exists():
                halted = stopped = True
            while not halted and len(active) < args.concurrency:
                job = next((item for item in batch["jobs"] if item["status"] == "pending"), None)
                if job is None:
                    break
                prefix = output / "runs" / f"{job['number']:04d}-attempt-{len(job['attempts']) + 1}"
                stdout = prefix.with_suffix(".stdout.log").open("x", encoding="utf-8")
                stderr = prefix.with_suffix(".stderr.log").open("x", encoding="utf-8")
                command = ["dotnet", str(host), "--python", args.python, "--model-dir", args.model_dir,
                           "--bridge", str(bridge), "--native-dir", str(native), "--device", "cuda", "--threads", "2",
                           "--seats", "0,1,2,3", "--curses", ",".join(map(str, job["cursesBySeat"])),
                           "--audit", "--purpose", args.purpose, "--rounds", "100", "--seed", str(job["seedNumber"]),
                           "--output", str(output / "matches"), "--curse-rule-version", args.curse_rule_version]
                process = subprocess.Popen(command, stdout=stdout, stderr=stderr,
                                           creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
                attempt = {"startedAtUtc": utc_now(), "pid": process.pid, "command": command,
                           "stdout": str(prefix.with_suffix(".stdout.log")), "stderr": str(prefix.with_suffix(".stderr.log"))}
                job["status"] = "running"
                job["attempts"].append(attempt)
                active.append({"job": job, "attempt": attempt, "process": process, "stdout": stdout, "stderr": stderr, "start": time.perf_counter()})
                persist()
            for worker in active[:]:
                process, job, attempt = worker["process"], worker["job"], worker["attempt"]
                if process.poll() is None and time.perf_counter() - worker["start"] > args.match_timeout:
                    subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True,
                                   creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), check=False)
                    process.wait()
                    attempt["error"] = "Host match timeout; no game operation was synthesized."
                if process.poll() is None:
                    continue
                worker["stdout"].close()
                worker["stderr"].close()
                lines = Path(attempt["stdout"]).read_text(encoding="utf-8-sig").splitlines()
                summary = next((json.loads(line) for line in reversed(lines) if line.startswith('{"result":')), None)
                attempt.update({"finishedAtUtc": utc_now(), "exitCode": process.returncode,
                                "wallSeconds": time.perf_counter() - worker["start"], "summary": summary})
                complete = bool(process.returncode == 0 and summary and summary.get("result") == "pass" and
                                summary.get("status") == "completed" and summary.get("matchEnded"))
                job["status"] = "completed" if complete else "truncated" if summary and summary.get("status") == "truncated" else "failed"
                if not complete:
                    halted = True
                active.remove(worker)
                persist()
                print(json.dumps({"job": job["number"], "status": job["status"], "counts": batch["statusCounts"],
                                  "wallSeconds": attempt["wallSeconds"], "rounds": summary.get("rounds") if summary else None}, ensure_ascii=False), flush=True)
            if not active and (halted or not any(job["status"] == "pending" for job in batch["jobs"])):
                break
            if time.perf_counter() - last_resource >= 10:
                try:
                    resource = {"atUtc": utc_now(), **gpu_sample(), "availableRamMiB": available_memory_mib()}
                    with (output / "resources.jsonl").open("a", encoding="utf-8") as handle:
                        handle.write(json.dumps(resource) + "\n")
                    batch["latestResources"] = resource
                except Exception as error:
                    batch["resourceMonitorError"] = str(error)
                last_resource = time.perf_counter()
                persist()
            time.sleep(0.5)
    finally:
        for worker in active:
            process = worker["process"]
            if process.poll() is None:
                subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True,
                               creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0), check=False)
                process.wait()
            worker["stdout"].close()
            worker["stderr"].close()
            worker["job"]["status"] = "interrupted"
        batch["status"] = "completed" if all(job["status"] == "completed" for job in batch["jobs"]) else "stopped" if stopped and not any(job["status"] in ("failed", "truncated", "interrupted") for job in batch["jobs"]) else "needs_attention"
        batch["finishedAtUtc"] = utc_now()
        persist()
        if any((output / "matches").glob("match-*/match.json")):
            subprocess.run([args.python, "-B", str(project / "python/summarize_matches.py"),
                            "--input", str(output / "matches"), "--output", str(output / "summary")], check=True)
    print(json.dumps({"batchDirectory": str(output), "status": batch["status"], "counts": batch["statusCounts"]}), flush=True)
    return 0 if batch["status"] == "completed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
