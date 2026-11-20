"""Validate a completed batch's records and summarize first-place rates by curse."""
import argparse
from collections import Counter, defaultdict
from datetime import datetime, timezone
import json
from pathlib import Path


def read_lines(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines()]


def analyze(directory):
    batch = json.loads((directory / "batch.json").read_text(encoding="utf-8"))
    completed = [job for job in batch["jobs"] if job["status"] == "completed"]
    stats = {curse: {"games": 0, "rankCounts": [0, 0, 0, 0], "pointsSum": 0,
                     "winningRounds": 0, "dealInRounds": 0, "liveWallSwaps": 0, "deadWallSwaps": 0,
                     "unavoidable": 0} for curse in range(1, 5)}
    outcomes, assignments, identities = Counter(), [Counter() for _ in range(4)], set()
    counts = Counter()
    checked_ids = set()
    rule_versions = set()
    for job in completed:
        attempt = job["attempts"][-1]
        summary = attempt["summary"]
        folder = Path(summary["recordDirectory"]).resolve()
        assert folder.parent == (directory / "matches").resolve(), "Recording path escaped this batch"
        match = json.loads((folder / "match.json").read_text(encoding="utf-8"))
        match_id = match["matchId"]
        assert match_id not in checked_ids, "Duplicate match identity"
        checked_ids.add(match_id)
        assert match["status"] == "completed" and match["matchEnded"]
        assert match["purpose"] == batch["configuration"]["purpose"]
        assert match["eligibleForStatistics"] == (match["purpose"] == "experiment")
        assert match["seedNumber"] == job["seedNumber"] and match["curses"] == job["cursesBySeat"]
        version = match.get("curseRuleVersion") or "v1"
        assert version == batch["configuration"].get("curseRuleVersion", "v1"), "Curse version disagrees with scheduled batch"
        rule_versions.add(version)
        assert match["modelPolicies"]["kokushiRobClosedKan"] == "skip"
        assert sorted(match["ranking"]) == [0, 1, 2, 3]
        assert len(match["modelRuntimes"]) == 4
        assert all(runtime["brain_device"] == "cuda:0" and runtime["dqn_device"] == "cuda:0" for runtime in match["modelRuntimes"].values())
        assert match["ranking"] == summary["ranking"] and match["scores"] == summary["scores"]
        assert summary["localDecisions"] == 0
        expected_points = 4 * match["rules"]["basePoint"]
        assert sum(match["scores"]) + match["kyotaku"] * 1000 == expected_points
        identities.add((match["coreAssemblySha256"], match["adapterAssemblySha256"], match["bridgeSha256"],
                        match["nativeSha256"], match["modelIdentity"]["sha256"]))
        records, checkpoints = defaultdict(list), set()
        for event in read_lines(folder / "events.jsonl"):
            if event["type"] == "round_start":
                state = event["state"]
                if version == "v2":
                    hand = state["state"]["playerList"][match["curses"].index(3)]["playerHandTile"]["handTileList"]
                    assert len(hand) == 13 and len({(tile["color"], tile["number"]) for tile in hand}) == 13, "V2 curse-three initial hand has duplicates"
                assert state["matchId"] == match_id and state["roundId"] not in checkpoints
                checkpoints.add(state["roundId"])
            else:
                assert event["type"] == "applied"
                record = event["record"]
                assert record["matchId"] == match_id
                assert not record["action"] or record["action"]["playerAction"] != "Skip"
                records[record["roundId"]].append(record)
                counts["successfulFacts"] += 1
        rounds = read_lines(folder / "rounds.jsonl")
        assert len(rounds) == match["settledRounds"] == summary["rounds"] == len(checkpoints) == len(records)
        assert checkpoints == set(records) == {item["roundId"] for item in rounds}
        for item in rounds:
            facts = records[item["roundId"]]
            assert [record["sequence"] for record in facts] == list(range(len(facts)))
            assert facts[0]["kind"] == "RoundStarted" and facts[-1]["kind"] == "RoundSettled"
            settlement = item["settlement"]
            assert facts[-1]["settlement"] == settlement
            assert sum(settlement["pointsAfter"]) + settlement["sticksAfter"] * 1000 == expected_points
            assert settlement["deltas"] == [after - before for before, after in zip(settlement["pointsBefore"], settlement["pointsAfter"])]
            outcomes[settlement["outcome"]] += 1
            for winner in settlement["winners"]:
                stats[match["curses"][winner["seat"]]]["winningRounds"] += 1
            if settlement["outcome"] == "Ron":
                for seat in {winner["sourceSeat"] for winner in settlement["winners"]}:
                    stats[match["curses"][seat]]["dealInRounds"] += 1
        audit = read_lines(folder / "curse-audit.jsonl")
        assert [item["index"] for item in audit] == list(range(len(audit)))
        counts["auditEntries"] += len(audit)
        counts["rounds"] += len(rounds)
        counts["modelDecisions"] += summary["modelDecisions"]
        for seat, curse in enumerate(match["curses"]):
            assignments[seat][curse] += 1
            stats[curse]["games"] += 1
            stats[curse]["rankCounts"][match["ranking"].index(seat)] += 1
            stats[curse]["pointsSum"] += match["scores"][seat]
        for curse_stats in match["curseStatistics"]:
            for field in ("liveWallSwaps", "deadWallSwaps", "unavoidable"):
                stats[match["curses"][curse_stats["seat"]]][field] += curse_stats[field]
    for values in stats.values():
        games = values["games"]
        values["firstPlaceRate"] = values["rankCounts"][0] / games if games else None
        values["averageRank"] = sum((rank + 1) * count for rank, count in enumerate(values["rankCounts"])) / games if games else None
        values["averagePoints"] = values["pointsSum"] / games if games else None
    resources = read_lines(directory / "resources.jsonl")
    starts = [datetime.fromisoformat(job["attempts"][-1]["startedAtUtc"]) for job in completed]
    ends = [datetime.fromisoformat(job["attempts"][-1]["finishedAtUtc"]) for job in completed]
    elapsed = (max(ends) - min(starts)).total_seconds() if completed else 0
    result = {"analysisStatus": "complete" if len(completed) == len(batch["jobs"]) and batch["status"] == "completed" else "partial",
              "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
              "batchDirectory": str(directory), "plannedMatches": len(batch["jobs"]), "statusCounts": batch["statusCounts"],
              "curseRuleVersions": sorted(rule_versions),
              "validatedCompletedMatches": len(completed), "artifactConfigurations": len(identities),
              "elapsedSeconds": elapsed, "matchesPerHour": len(completed) * 3600 / elapsed if elapsed else 0,
              "counts": dict(counts), "roundOutcomes": dict(outcomes), "seatCurseCounts": assignments,
              "curseResults": stats, "peakGlobalGpuMiB": max(item["usedMiB"] for item in resources),
              "minimumAvailableRamMiB": min(item["availableRamMiB"] for item in resources),
              "recordingBytes": sum(path.stat().st_size for path in (directory / "matches").rglob("*") if path.is_file()),
              "interpretation": "Pilot sample; first-place rates describe this model under the recorded curse and skip policies."}
    output = directory / "summary"
    output.mkdir(exist_ok=True)
    (output / "analysis.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    print(json.dumps(analyze(parser.parse_args().directory.resolve()), ensure_ascii=False, indent=2))
