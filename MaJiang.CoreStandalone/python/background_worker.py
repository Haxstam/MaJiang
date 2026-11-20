"""System-hosted batch supervisor. Uses only the frozen runtime beside this file."""
from __future__ import annotations

import argparse
import ctypes
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys
import time
import traceback


def utc_now():
    return datetime.now(timezone.utc).isoformat()


def save(path, value):
    temporary = path.with_suffix(".pending")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    for attempt in range(50):
        try:
            temporary.replace(path)
            return
        except PermissionError:
            if attempt == 49:
                raise
            time.sleep(0.1)


def _supervise(root):
    info_path = root / "background.json"
    info = json.loads(info_path.read_text(encoding="utf-8-sig"))
    runtime = root / "runtime"
    records = root / "records"
    # The OS task uses pythonw, so replace its absent stdout/stderr explicitly.
    with (root / "worker.stdout.log").open("a", encoding="utf-8", buffering=1) as output, (root / "worker.stderr.log").open("a", encoding="utf-8", buffering=1) as errors:
        sys.stdout, sys.stderr = output, errors
        info.update({"status": "running", "supervisorPid": __import__("os").getpid(), "lastStartedAtUtc": utc_now()})
        save(info_path, info)
        power_request = ctypes.windll.kernel32.SetThreadExecutionState
        power_request.argtypes, power_request.restype = [ctypes.c_uint], ctypes.c_uint
        awake = power_request(0x80000001)
        info["sleepInhibitionRequested"] = bool(awake)
        save(info_path, info)
        try:
            config = info["configuration"]
            command = [config["python"], "-B", "-u", str(runtime / "python/run_matches.py"),
                       "--count", str(config["count"]), "--concurrency", str(config["concurrency"]),
                       "--seed", str(config["firstSeed"]), "--python", config["python"], "--model-dir", config["modelDirectory"],
                       "--purpose", config.get("purpose", "experiment"), "--output", str(records), "--stop-file", str(root / "stop.requested"),
                       "--curse-rule-version", config.get("curseRuleVersion", "v1")]
            if (records / "batch.json").exists():
                command.append("--resume")
            print(json.dumps({"atUtc": utc_now(), "command": command}), flush=True)
            result = subprocess.run(command, stdin=subprocess.DEVNULL, stdout=output, stderr=errors, creationflags=subprocess.CREATE_NO_WINDOW)
            info["runnerExitCode"] = result.returncode
            batch = json.loads((records / "batch.json").read_text(encoding="utf-8"))
            info.update({"status": "postprocessing", "batchStatus": batch["status"], "statusCounts": batch["statusCounts"]})
            save(info_path, info)
            postprocess = []
            if batch["statusCounts"]["completed"]:
                commands = [
                    ("player-statistics", [str(runtime / "python/player_statistics.py"), "--input", str(records),
                                            "--group-by", "curse", "--purpose", config.get("purpose", "experiment"), "--output", str(records / "summary/player-statistics")]),
                    ("batch-analysis", [str(runtime / "python/analyze_batch.py"), str(records)])]
                if batch["status"] == "completed" and config.get("purpose", "experiment") == "experiment":
                    commands.append(("hypotheses", [str(runtime / "python/analyze_hypotheses.py"), str(records)]))
                for name, arguments in commands:
                    with (records / "summary" / (name + ".stdout.log")).open("w", encoding="utf-8") as analysis_out, (records / "summary" / (name + ".stderr.log")).open("w", encoding="utf-8") as analysis_errors:
                        checked = subprocess.run([config["python"], "-B"] + arguments, stdin=subprocess.DEVNULL, stdout=analysis_out, stderr=analysis_errors,
                                                 creationflags=subprocess.CREATE_NO_WINDOW)
                    postprocess.append({"name": name, "exitCode": checked.returncode})
            info["postprocess"] = postprocess
            info["status"] = batch["status"] if all(step["exitCode"] == 0 for step in postprocess) else "postprocess_failed"
            info["finishedAtUtc"] = utc_now()
            save(info_path, info)
            return 0 if info["status"] in ("completed", "stopped") else 1
        except Exception:
            traceback.print_exc(file=errors)
            info.update({"status": "supervisor_failed", "finishedAtUtc": utc_now(), "error": traceback.format_exc()})
            save(info_path, info)
            return 1
        finally:
            power_request(0x80000000)


def supervise(root):
    original_out, original_err = sys.stdout, sys.stderr
    try:
        return _supervise(root)
    finally:
        sys.stdout, sys.stderr = original_out, original_err


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--probe", action="store_true")
    args = parser.parse_args()
    if args.probe:
        args.root.mkdir(parents=True, exist_ok=True)
        save(args.root / "probe.json", {"status": "started", "atUtc": utc_now(), "pid": __import__("os").getpid()})
        time.sleep(8)
        save(args.root / "probe.json", {"status": "completed", "atUtc": utc_now(), "pid": __import__("os").getpid()})
    else:
        try:
            result = supervise(args.root.resolve())
        except Exception:
            (args.root / "fatal-error.log").write_text(traceback.format_exc(), encoding="utf-8")
            result = 1
        raise SystemExit(result)
