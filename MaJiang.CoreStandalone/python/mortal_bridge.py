"""Project-local Mortal bridge for Python 3.10+ and an explicitly selected native encoder.

The model source and weights remain in the existing deployment directory.
Each input event produces one ok/reaction envelope; diagnostics go to stderr.
"""
from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import sys
from types import SimpleNamespace


def file_digest_compat(handle, algorithm):
    """Python 3.10 equivalent for the checkpoint loader's hashlib.file_digest call."""
    digest = hashlib.new(algorithm)
    for block in iter(lambda: handle.read(1024 * 1024), b""):
        digest.update(block)
    return digest


def load_inference(model_directory: Path, native_directory: Path | None):
    if native_directory is not None:
        native_path = native_directory / "libriichi.pyd"
        if not native_path.is_file():
            raise FileNotFoundError(f"Missing native encoder: {native_path}")
        spec = importlib.util.spec_from_file_location("libriichi", native_path)
        if spec is None or spec.loader is None:
            raise ImportError(f"Cannot load native encoder: {native_path}")
        module = importlib.util.module_from_spec(spec)
        sys.modules["libriichi"] = module
        spec.loader.exec_module(module)

    source = model_directory / "inference.py"
    spec = importlib.util.spec_from_file_location("majiang_mortal_inference", source)
    if spec is None or spec.loader is None:
        raise ImportError(f"Cannot load model entry: {source}")
    inference = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = inference
    spec.loader.exec_module(inference)
    if not hasattr(hashlib, "file_digest"):
        # Limit the compatibility replacement to this loader; keep the standard module intact.
        inference.hashlib = SimpleNamespace(file_digest=file_digest_compat)
    return inference


def emit(value):
    print(json.dumps(value, ensure_ascii=False, allow_nan=False), flush=True)


def runtime_info(engine, torch):
    return {
        "python": sys.executable,
        "python_version": sys.version.split()[0],
        "torch": torch.__version__,
        "torch_cuda": torch.version.cuda,
        "requested_device": str(engine.device),
        "brain_device": str(next(engine.brain.parameters()).device),
        "dqn_device": str(next(engine.dqn.parameters()).device),
        "gpu": torch.cuda.get_device_name(engine.device) if engine.device.type == "cuda" else None,
        "allocated_gpu_bytes": torch.cuda.memory_allocated(engine.device) if engine.device.type == "cuda" else 0,
        "native_module": sys.modules["libriichi"].__file__,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seat", type=int, choices=range(4), default=0)
    parser.add_argument("--device", choices=("cpu", "cuda"), default="cuda")
    parser.add_argument("--threads", type=int, default=2)
    parser.add_argument("--model-dir", default=os.environ.get("MAJIANG_MORTAL_MODEL_DIR"))
    parser.add_argument("--native-dir", default=os.environ.get("MAJIANG_LIBRIICHI_DIR"))
    parser.add_argument("--smoke", action="store_true")
    args = parser.parse_args()
    if args.threads < 1 or args.model_dir is None:
        parser.error("positive --threads and --model-dir (or MAJIANG_MORTAL_MODEL_DIR) are required")
    sys.stdin.reconfigure(encoding="utf-8")
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")

    import torch

    torch.set_num_threads(args.threads)
    if args.device == "cuda" and not torch.cuda.is_available():
        raise RuntimeError(f"CUDA is unavailable in {sys.executable}; no CPU fallback was selected")
    inference = load_inference(Path(args.model_dir).resolve(), Path(args.native_dir).resolve() if args.native_dir else None)
    engine = inference.load_engine(args.device)
    info = runtime_info(engine, torch)
    print("Mortal runtime: " + json.dumps(info, ensure_ascii=False), file=sys.stderr, flush=True)
    if args.smoke:
        report = inference.smoke(engine)
        if args.device == "cuda":
            torch.cuda.synchronize()
        report["runtime"] = runtime_info(engine, torch)
        emit(report)
        return 0

    session = None
    for line in sys.stdin:
        if not line.strip():
            continue
        try:
            event = json.loads(line)
            if not isinstance(event, dict) or not isinstance(event.get("type"), str):
                raise ValueError("Expected one mjai event object with a string type")
            if event["type"] == "start_game":
                session = inference.Session(engine, args.seat)
            if session is None:
                raise ValueError("Send start_game before other events")
            if event["type"] == "start_kyoku":
                if len(event["tehais"]) != 4 or any(len(hand) != 13 for hand in event["tehais"]):
                    raise ValueError("tehais must contain four arrays of exactly 13 tiles")
                event["tehais"] = [hand if seat == args.seat else ["?"] * 13 for seat, hand in enumerate(event["tehais"])]
            elif event["type"] == "tsumo" and event["actor"] != args.seat:
                event["pai"] = "?"
            emit({"ok": True, "reaction": session.react(event)})
            if event["type"] == "end_game":
                session = None
        except Exception as error:
            emit({"ok": False, "error": str(error), "reset_required": True})
            return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
