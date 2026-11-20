"""Checks for scheduling, Windows file contention and frozen-runtime supervision."""
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import background_worker
import run_matches


class BackgroundChecks(unittest.TestCase):
    def test_schedule_balances_1000_unique_seeds_and_every_seat(self):
        schedule = run_matches.make_schedule(1000, 101)
        self.assertEqual({job["seedNumber"] for job in schedule}, set(range(101, 1101)))
        self.assertEqual(len({tuple(job["cursesBySeat"]) for job in schedule}), 24)
        for seat in range(4):
            self.assertEqual(Counter(job["cursesBySeat"][seat] for job in schedule), {1: 250, 2: 250, 3: 250, 4: 250})
        self.assertTrue(all(job["status"] == "pending" and not job["attempts"] for job in schedule))

    def test_atomic_writes_retry_short_read_locks(self):
        for module in (run_matches, background_worker):
            with self.subTest(module=module.__name__), tempfile.TemporaryDirectory() as temporary:
                path = Path(temporary) / "state.json"
                original = Path.replace
                calls = []
                def replace(source, target):
                    calls.append(1)
                    if len(calls) < 3:
                        raise PermissionError("Windows reader lock")
                    return original(source, target)
                with patch.object(Path, "replace", autospec=True, side_effect=replace), patch.object(module.time, "sleep"):
                    module.save(path, {"complete": 2})
                self.assertEqual(json.loads(path.read_text()), {"complete": 2})
                self.assertEqual(len(calls), 3)

    def test_permanent_write_failure_keeps_previous_state(self):
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "state.json"
            path.write_text('{"previous":true}')
            with patch.object(Path, "replace", side_effect=PermissionError("locked")), patch.object(run_matches.time, "sleep"):
                with self.assertRaises(PermissionError):
                    run_matches.save(path, {"new": True})
            self.assertEqual(json.loads(path.read_text()), {"previous": True})

    def test_streams_restore_even_on_supervisor_error(self):
        original = sys.stdout, sys.stderr
        def fail(_):
            sys.stdout, sys.stderr = None, None
            raise RuntimeError("test startup error")
        with patch.object(background_worker, "_supervise", side_effect=fail):
            with self.assertRaises(RuntimeError):
                background_worker.supervise(Path("unused"))
        self.assertEqual((sys.stdout, sys.stderr), original)

    def test_all_subprocesses_use_frozen_paths_and_postprocess(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            (root / "records/summary").mkdir(parents=True)
            (root / "background.json").write_text(json.dumps({"configuration": {
                "python": sys.executable, "count": 1000, "concurrency": 2, "firstSeed": 101, "modelDirectory": "existing-model", "curseRuleVersion": "v2"}}))
            (root / "records/batch.json").write_text(json.dumps({"status": "completed", "statusCounts": {"completed": 1000}}))
            class Power:
                def __init__(self): self.flags = []
                def __call__(self, flag): self.flags.append(flag); return 1
            power = Power()
            with patch.object(background_worker.ctypes.windll.kernel32, "SetThreadExecutionState", power), patch.object(
                background_worker.subprocess, "run", return_value=subprocess.CompletedProcess([], 0)) as launched:
                self.assertEqual(background_worker.supervise(root), 0)
            self.assertEqual(len(launched.call_args_list), 4)
            for call in launched.call_args_list:
                command = call.args[0]
                script = next(argument for argument in command if argument.endswith(".py"))
                self.assertEqual(Path(script).parent, root / "runtime/python")
                self.assertEqual(call.kwargs["stdin"], subprocess.DEVNULL)
            first = launched.call_args_list[0].args[0]
            self.assertIn("--resume", first)
            self.assertIn(str(root / "stop.requested"), first)
            self.assertEqual(first[first.index("--curse-rule-version") + 1], "v2")
            self.assertEqual(power.flags, [0x80000001, 0x80000000])
            self.assertEqual(json.loads((root / "background.json").read_text())["status"], "completed")


if __name__ == "__main__":
    unittest.main()
