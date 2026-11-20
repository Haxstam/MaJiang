"""Describe achieved kokushi/flush wins and win values in an existing match batch.

Uses scored wins only (Ron/Tsumo), excludes nagashi, and counts each winner once.
No unobserved model intent or no-curse counterfactual is inferred.
"""
import argparse
from collections import Counter, defaultdict
import csv
from datetime import datetime, timezone
import json
from pathlib import Path
from statistics import mean, median


def lines(path):
    return [json.loads(line) for line in path.read_text(encoding="utf-8-sig").splitlines()]


def round_hundred(value):
    return (value + 99) // 100 * 100


def write_csv(path, rows):
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def analyze(directory):
    batch = json.loads((directory / "batch.json").read_text(encoding="utf-8"))
    assert batch["status"] == "completed"
    wins, rows_by_curse, match_ids = [], defaultdict(list), defaultdict(set)
    exposures, final_points = Counter(), defaultdict(list)
    rule_versions = set()
    kokushi = {"KokushiMusou", "KokushiMusouThirteenOrphans"}
    for job in batch["jobs"]:
        assert job["status"] == "completed"
        folder = Path(job["attempts"][-1]["summary"]["recordDirectory"])
        manifest = json.loads((folder / "match.json").read_text(encoding="utf-8"))
        assert manifest["status"] == "completed" and manifest["eligibleForStatistics"]
        rule_versions.add(manifest.get("curseRuleVersion") or "v1")
        if len(rule_versions) > 1:
            raise ValueError("Mixed curse rule versions; analyze V1 and V2 separately.")
        contexts = {}
        for event in lines(folder / "events.jsonl"):
            if event["type"] == "round_start":
                state = event["state"]
                contexts[state["roundId"]] = state["state"]
        settlements = lines(folder / "rounds.jsonl")
        for seat, curse in enumerate(manifest["curses"]):
            exposures[curse] += len(settlements)
            final_points[curse].append(manifest["scores"][seat])
            match_ids[curse].add(manifest["matchId"])
        for item in settlements:
            settlement = item["settlement"]
            if settlement["outcome"] not in ("Ron", "Tsumo"):
                continue
            context = contexts[item["roundId"]]
            for index, winner in enumerate(settlement["winners"]):
                score = winner["score"]
                seat = winner["seat"]
                curse = manifest["curses"][seat]
                yaku = {entry["yaku"] for entry in score["yaku"]}
                dealer = seat == context["currentDealerIndex"]
                tsumo = settlement["outcome"] == "Tsumo"
                base = score["basePoints"]
                intrinsic = (3 * round_hundred(2 * base) if dealer else round_hundred(2 * base) + 2 * round_hundred(base)) if tsumo else round_hundred(base * (6 if dealer else 4))
                honba = context["honba"] * 300 if index == 0 else 0
                sticks = settlement["sticksBefore"] * 1000 if index == 0 else 0
                receipt = winner["payments"][seat]
                assert receipt == intrinsic + honba + sticks, "Win receipt does not match intrinsic score plus honba/deposits"
                revealed = next(hand for hand in settlement["revealed"] if hand["seat"] == seat)
                # Tiles already in the concealed hand and each complete open group;
                # winning tile is stored separately for Ron and as Drawn for Tsumo.
                tiles = revealed["concealed"] + ([revealed["drawn"]] if revealed["drawn"] else [])
                if not revealed["drawn"]:
                    tiles.append(winner["winningTile"])
                for group in revealed["groups"]:
                    tiles += group["tiles"]
                suits = {tile["color"] for tile in tiles if tile["color"] != "Honor"}
                has_honors = any(tile["color"] == "Honor" for tile in tiles)
                shape_honitsu = len(suits) == 1 and has_honors
                shape_chinitsu = len(suits) == 1 and not has_honors
                # Yakuman could suppress ordinary yaku. Keep both shape and scored-yaku columns.
                if not score["yakuman"]:
                    assert shape_honitsu == ("Honitsu" in yaku) and shape_chinitsu == ("Chinitsu" in yaku)
                row = {"matchId": manifest["matchId"], "seedNumber": manifest["seedNumber"], "roundId": item["roundId"],
                       "roundGeneration": item["roundGeneration"], "seat": seat, "curse": curse,
                       "outcome": settlement["outcome"], "dealer": dealer, "kokushi": bool(yaku & kokushi),
                       "kokushiThirteenSided": "KokushiMusouThirteenOrphans" in yaku,
                       "honitsu": "Honitsu" in yaku, "chinitsu": "Chinitsu" in yaku,
                       "shapeHonitsu": shape_honitsu, "shapeChinitsu": shape_chinitsu,
                       "han": score["han"], "fu": score["fu"], "yakumanUnits": score["yakuman"],
                       "bonusHan": sum(entry["han"] for entry in score["yaku"] if entry["yaku"] in ("Dora", "UraDora", "AkaDora")),
                       "intrinsicWinPoints": intrinsic, "actualReceiptPoints": receipt, "honbaPoints": honba,
                       "depositPoints": sticks, "childRonEquivalentPoints": round_hundred(4 * base),
                       "yaku": ";".join(sorted(yaku))}
                wins.append(row)
                rows_by_curse[curse].append(row)
    summaries = []
    for curse in range(1, 5):
        rows = rows_by_curse[curse]
        non_yakuman = [row for row in rows if not row["yakumanUnits"]]
        n = len(rows)
        k = sum(row["kokushi"] for row in rows)
        h = sum(row["honitsu"] for row in rows)
        c = sum(row["chinitsu"] for row in rows)
        summaries.append({"curse": curse, "matches": len(match_ids[curse]), "playerRoundExposures": exposures[curse],
                          "wins": n, "ronWins": sum(row["outcome"] == "Ron" for row in rows),
                          "tsumoWins": sum(row["outcome"] == "Tsumo" for row in rows),
                          "kokushiWins": k, "ordinaryKokushiWins": k - sum(row["kokushiThirteenSided"] for row in rows),
                          "thirteenSidedKokushiWins": sum(row["kokushiThirteenSided"] for row in rows),
                          "matchesWithKokushi": len({row["matchId"] for row in rows if row["kokushi"]}),
                          "kokushiShareOfWins": k / n, "kokushiPerPlayerRound": k / exposures[curse],
                          "honitsuWins": h, "chinitsuWins": c, "flushWins": h + c, "flushShareOfWins": (h + c) / n,
                          "flushPerPlayerRound": (h + c) / exposures[curse],
                          "shapeFlushWins": sum(row["shapeHonitsu"] or row["shapeChinitsu"] for row in rows),
                          "yakumanFlushShapeWins": sum(bool(row["yakumanUnits"]) and (row["shapeHonitsu"] or row["shapeChinitsu"]) for row in rows),
                          "averageIntrinsicWinPoints": mean(row["intrinsicWinPoints"] for row in rows),
                          "medianIntrinsicWinPoints": median(row["intrinsicWinPoints"] for row in rows),
                          "averageActualReceiptPoints": mean(row["actualReceiptPoints"] for row in rows),
                          "averageChildRonEquivalentPoints": mean(row["childRonEquivalentPoints"] for row in rows),
                          "averageNonYakumanWinPoints": mean(row["intrinsicWinPoints"] for row in non_yakuman),
                          "averageNonYakumanHan": mean(row["han"] for row in non_yakuman),
                          "averageBonusHan": mean(row["bonusHan"] for row in rows),
                          "averageFinalPoints": mean(final_points[curse])})
    assert len({(row["matchId"], row["roundId"], row["seat"]) for row in wins}) == len(wins)
    result = {"generatedAtUtc": datetime.now(timezone.utc).isoformat(), "batchDirectory": str(directory),
              "completedMatches": len(batch["jobs"]), "totalWinningHands": len(wins),
              "curseRuleVersions": sorted(rule_versions),
              "definitions": {
                  "sample": "One row per actual Ron/Tsumo winner; multiple Ron counts each winner; NagashiMangan excluded.",
                  "kokushi": "KokushiMusou or KokushiMusouThirteenOrphans; each winning hand counted once regardless of yakuman units.",
                  "flush": "Honitsu or Chinitsu in scored yaku, independently checked against all winning-hand tiles.",
                  "yakumanFlush": "Yakuman may suppress ordinary flush yaku. shapeFlushWins includes such shapes; yakumanFlushShapeWins reports them explicitly.",
                  "intrinsicWinPoints": "Rounded total score for actual dealer/non-dealer and Ron/Tsumo, excluding honba and deposits.",
                  "actualReceiptPoints": "Winner's payment receipt, including received honba and deposits; not entire-match final points.",
                  "childRonEquivalentPoints": "All hands scored as a non-dealer Ron, excluding honba/deposits, to remove dealer and win-mode mixture.",
                  "playerRoundExposures": "All rounds including abortive draws; each curse has one player per round.",
                  "intent": "Only completed winning hands are observed; attempts to pursue a hand are not inferred.",
                  "comparison": "No un-cursed control group; this is a descriptive within-batch comparison, not a measured increase over ordinary mahjong."},
              "curseStatistics": summaries}
    output = directory / "summary"
    write_csv(output / "winning-hands.csv", wins)
    write_csv(output / "curse-hand-statistics.csv", summaries)
    (output / "hypotheses.json").write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    print(json.dumps(analyze(parser.parse_args().directory.resolve()), ensure_ascii=False, indent=2))
