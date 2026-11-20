"""Export every recorded match (including failures) and its seat results to CSV.

Rows are retained with explicit eligibility; only completed experiment rows qualify.
This tool does not combine win rates across different models/rules/seat assignments.
"""
import argparse
from collections import Counter
import csv
import json
from pathlib import Path


def write_csv(path, rows, fields):
    with path.open("w", newline="", encoding="utf-8-sig") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()
    root, output = Path(args.input).resolve(), Path(args.output).resolve()
    paths = sorted(root.rglob("match.json"))
    if not paths:
        raise ValueError(f"No match.json recordings found in {root}")
    output.mkdir(parents=True, exist_ok=True)
    matches, seats, seen = [], [], set()
    for path in paths:
        match = json.loads(path.read_text(encoding="utf-8-sig"))
        match_id = match["matchId"]
        if match_id in seen:
            raise ValueError(f"Duplicate matchId {match_id}; avoid counting copied folders twice.")
        seen.add(match_id)
        eligible = bool(match.get("eligibleForStatistics") and match.get("matchEnded") and
                        match["status"] == "completed" and match["purpose"] == "experiment")
        ranking, scores = match.get("ranking"), match.get("scores")
        if eligible and (sorted(ranking or []) != [0, 1, 2, 3] or len(scores or []) != 4):
            raise ValueError(f"Eligible match lacks valid final results: {path}")
        curses = match.get("curses") or [None] * 4
        timing = match.get("timing") or {}
        common = {"matchId": match_id, "purpose": match["purpose"], "status": match["status"],
                  "eligibleForStatistics": eligible, "seedNumber": match["seedNumber"],
                  "curseRuleVersion": match.get("curseRuleVersion") or ("v1" if match.get("curses") else None)}
        matches.append({**common, "rounds": match.get("settledRounds"),
                        "curses": json.dumps(curses), "scores": json.dumps(scores), "ranking": json.dumps(ranking),
                        "startupSeconds": timing.get("startupSeconds"), "playSeconds": timing.get("playSeconds"),
                        "modelSha256": (match.get("modelIdentity") or {}).get("sha256"),
                        "kokushiRobClosedKanPolicy": (match.get("modelPolicies") or {}).get("kokushiRobClosedKan"),
                        "coreAssemblySha256": match.get("coreAssemblySha256"),
                        "adapterAssemblySha256": match.get("adapterAssemblySha256"),
                        "error": match.get("error"), "directory": str(path.parent)})
        statistics = {item["seat"]: item for item in match.get("curseStatistics", [])}
        for seat in range(4):
            stats = statistics.get(seat, {})
            seats.append({**common, "seat": seat, "curse": curses[seat],
                          "points": scores[seat] if scores else None,
                          "rank": ranking.index(seat) + 1 if ranking else None,
                          **{key: stats.get(key) for key in ("attempts", "preparedHands", "alreadyValid", "liveWallSwaps", "deadWallSwaps", "unavoidable")}})
    common_fields = ["matchId", "purpose", "status", "eligibleForStatistics", "seedNumber", "curseRuleVersion"]
    write_csv(output / "matches.csv", matches, common_fields + ["rounds", "curses", "scores", "ranking", "startupSeconds", "playSeconds",
                                                               "modelSha256", "kokushiRobClosedKanPolicy", "coreAssemblySha256", "adapterAssemblySha256", "error", "directory"])
    write_csv(output / "seat-results.csv", seats, common_fields + ["seat", "curse", "points", "rank", "attempts", "preparedHands", "alreadyValid", "liveWallSwaps", "deadWallSwaps", "unavoidable"])
    summary = {"recordedMatches": len(matches), "statusCounts": dict(Counter(row["status"] for row in matches)),
               "purposeCounts": dict(Counter(row["purpose"] for row in matches)),
               "eligibleExperimentalMatches": sum(row["eligibleForStatistics"] for row in matches)}
    (output / "summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary))


if __name__ == "__main__":
    main()
