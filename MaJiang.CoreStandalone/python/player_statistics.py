"""Compute player statistics from this project's completed match recordings.

Python standard library only. Rates retain numerator/denominator, and undefined
conditional statistics are null. See PLAYER_STATISTICS.zh-CN.md for definitions.
"""
from __future__ import annotations

import argparse
from collections import Counter, defaultdict
import csv
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
import fnmatch
import hashlib
import json
from pathlib import Path


VERSION = "1.1"
OPEN_ACTIONS = {"Chi", "Peng", "OpenKan", "AddedKan"}
KAN_ACTIONS = {"OpenKan", "AddedKan", "ClosedKan"}
WINS = {"Ron", "Tsumo"}
ABORTS = {"NineTerminals", "FourWinds", "FourRiichi", "FourKans"}


@dataclass(frozen=True)
class Definitions:
    include_abortive: bool = True
    draws: str = "exhaustive"
    tsumo_denominator: str = "wins"
    dama: str = "closed-nonriichi-wins"
    points: str = "base"
    ron_turn: str = "current"


def divide(numerator, denominator):
    return numerator / denominator if denominator else None


def read_json(path):
    with path.open(encoding="utf-8-sig") as handle:
        return json.load(handle)


def read_jsonl(path):
    with path.open(encoding="utf-8-sig") as handle:
        for number, line in enumerate(handle, 1):
            try:
                yield json.loads(line)
            except (ValueError, TypeError) as error:
                raise ValueError(f"Invalid JSONL at {path}:{number}: {error}") from error


def require(condition, message):
    if not condition:
        raise ValueError(message)


def round_hundred(value):
    return (value + 99) // 100 * 100


def intrinsic_win(winner, dealer, tsumo):
    base = winner["score"]["basePoints"]
    if not tsumo:
        return round_hundred(base * (6 if winner["seat"] == dealer else 4))
    return 3 * round_hundred(2 * base) if winner["seat"] == dealer else round_hundred(2 * base) + 2 * round_hundred(base)


def configuration(manifest):
    content = {key: manifest.get(key) for key in (
        "rules", "coreAssemblySha256", "adapterAssemblySha256", "bridgeSha256", "nativeSha256", "modelPolicies")}
    content["modelSha256"] = (manifest.get("modelIdentity") or {}).get("sha256")
    content["curseRuleVersion"] = manifest.get("curseRuleVersion") or ("v1" if manifest.get("curses") else None)
    require(content["curseRuleVersion"] in (None, "v1", "v2"), "Unsupported curse rule version")
    digest = hashlib.sha256(json.dumps(content, sort_keys=True, separators=(",", ":")).encode()).hexdigest()
    return digest, content


def round_facts(manifest, checkpoint, records, settlement):
    """Yield four seat observations. Read facts once; never replay attempted inputs."""
    state = checkpoint["state"]
    start = state["state"]
    round_id, match_id = state["roundId"], manifest["matchId"]
    require([item["sequence"] for item in records] == list(range(len(records))), f"Record gap in {match_id}/{round_id}")
    require(records and records[0]["kind"] == "RoundStarted" and records[-1]["kind"] == "RoundSettled", f"Incomplete round {round_id}")
    require(records[-1]["settlement"] == settlement, f"Settlement mismatch in {round_id}")
    require(all(item["matchId"] == match_id and item["roundId"] == round_id for item in records), f"Wrong record identity in {round_id}")
    # The full checkpoint contains the first dealer draw. Process its matching
    # fact once, rather than treating the checkpoint as another draw.
    prefix = [record["action"] for record in records if record["sequence"] <= checkpoint["checkpointSequence"] and record["action"]]
    require(len(prefix) == 1 and prefix[0]["playerAction"] == "Take", f"Checkpoint is not a round opening: {round_id}")
    require(len(start["playerPoint"]) == len(settlement["pointsAfter"]) == 4, f"Invalid point vectors in {round_id}")
    require(sum(start["playerPoint"]) + start["riichiStickCount"] * 1000 ==
            sum(settlement["pointsAfter"]) + settlement["sticksAfter"] * 1000, f"Nonconserving round {round_id}")
    turns, discards, draws = [0] * 4, [0] * 4, [0] * 4
    replacement = [False] * 4
    open_hand = [bool(groups) and any(group["groupType"] != "ClosedKan" for group in groups) for groups in start["playerOpenSetList"]]
    riichi, first, riichi_turn, winning_turn = [False] * 4, [False] * 4, [None] * 4, [None] * 4
    seen_riichi = False
    for record in records:
        action = record["action"]
        if action is None:
            continue
        seat, kind = action["selfNumber"], action["playerAction"]
        require(seat in range(4), f"Invalid action seat in {round_id}")
        if kind == "Take":
            draws[seat] += 1
            if not replacement[seat]:
                turns[seat] = discards[seat] + 1
            replacement[seat] = False
        elif kind in {"Chi", "Peng", "OpenKan"}:
            turns[seat] = discards[seat] + 1
        if kind in OPEN_ACTIONS:
            open_hand[seat] = True
        if kind in KAN_ACTIONS:
            replacement[seat] = True
        if kind in {"HandCut", "TakeCut"}:
            discards[seat] += 1
            turns[seat] = discards[seat]
        elif kind == "Riichi":
            require(not riichi[seat] and not open_hand[seat], f"Invalid repeated/open riichi in {round_id}")
            riichi[seat], first[seat] = True, not seen_riichi
            riichi_turn[seat] = max(1, turns[seat])
            seen_riichi = True
        elif kind in WINS:
            winning_turn[seat] = max(1, turns[seat])
        elif kind == "Skip":
            raise ValueError(f"An intent/skip entered the successful log: {round_id}")
    require(settlement["outcome"] in WINS | ABORTS | {"ExhaustiveDraw"}, f"Unsupported outcome {settlement['outcome']}")
    regular_win = settlement["outcome"] in WINS
    winners = {winner["seat"]: winner for winner in settlement["winners"]} if regular_win else {}
    require(len(winners) == len(settlement["winners"]) if regular_win else True, f"Duplicate winner in {round_id}")
    deal_in_base, deal_in_actual = [0] * 4, [0] * 4
    win_base, win_receipt = [0] * 4, [0] * 4
    source_seats = set()
    for index, winner in enumerate(settlement["winners"] if regular_win else []):
        seat = winner["seat"]
        base = intrinsic_win(winner, start["currentDealerIndex"], settlement["outcome"] == "Tsumo")
        honba = start["honba"] * 300 if index == 0 else 0
        deposits = settlement["sticksBefore"] * 1000 if index == 0 else 0
        require(winner["payments"][seat] == base + honba + deposits, f"Winner payment mismatch in {round_id}")
        win_base[seat], win_receipt[seat] = base, winner["payments"][seat]
        if settlement["outcome"] == "Ron":
            source = winner["sourceSeat"]
            source_seats.add(source)
            paid = -winner["payments"][source]
            # Pao may put honba on another liable player. A discarder's loss
            # follows the actual ledger, not the winner's entire receipt.
            source_honba = honba if winner["paoSeat"] in (-1, source) else 0
            require(paid >= source_honba, f"Invalid source payment in {round_id}")
            deal_in_actual[source] += paid
            deal_in_base[source] += paid - source_honba
    for seat in range(4):
        winner = winners.get(seat)
        yield {
            "matchId": match_id, "roundId": round_id, "roundGeneration": state["roundGeneration"],
            "seat": seat, "name": start["playerList"][seat]["playerProfile"]["name"],
            "curse": (manifest.get("curses") or [None] * 4)[seat],
            "curseRuleVersion": manifest.get("curseRuleVersion") or ("v1" if manifest.get("curses") else None),
            "isModel": seat in manifest["modelSeats"],
            "modelSha256": (manifest.get("modelIdentity") or {}).get("sha256") if seat in manifest["modelSeats"] else None,
            "outcome": settlement["outcome"], "abortive": settlement["outcome"] in ABORTS,
            "exhaustive": settlement["outcome"] == "ExhaustiveDraw",
            "tenpaiAtExhaustive": settlement["outcome"] == "ExhaustiveDraw" and bool(settlement["tenpaiMask"] & (1 << seat)),
            "won": seat in winners, "tsumo": seat in winners and settlement["outcome"] == "Tsumo",
            "ron": seat in winners and settlement["outcome"] == "Ron", "dealtIn": seat in source_seats,
            "riichi": riichi[seat], "firstRiichi": first[seat], "riichiTurn": riichi_turn[seat],
            "openHand": open_hand[seat], "closedNonriichiWin": seat in winners and not riichi[seat] and not open_hand[seat],
            "closedNonriichiRon": seat in winners and settlement["outcome"] == "Ron" and not riichi[seat] and not open_hand[seat],
            "winningTurn": winning_turn[seat], "ownDiscards": discards[seat], "ownDraws": draws[seat],
            "winBasePoints": win_base[seat], "winReceiptPoints": win_receipt[seat],
            "dealInBasePoints": deal_in_base[seat], "dealInActualPoints": deal_in_actual[seat],
            "startPoints": start["playerPoint"][seat], "endPoints": settlement["pointsAfter"][seat],
            "netPoints": settlement["pointsAfter"][seat] - start["playerPoint"][seat],
            "yaku": [yaku["yaku"] for yaku in winner["score"]["yaku"]] if winner else [],
            "nagashi": bool(settlement["nagashiMask"] & (1 << seat)),
        }


def load_match(folder):
    manifest = read_json(folder / "match.json")
    checkpoints, records = {}, defaultdict(list)
    for event in read_jsonl(folder / "events.jsonl"):
        if event["type"] == "round_start":
            round_id = event["state"]["roundId"]
            require(round_id not in checkpoints and event["state"]["matchId"] == manifest["matchId"], f"Duplicate/wrong checkpoint at {folder}")
            checkpoints[round_id] = event
        elif event["type"] == "applied":
            records[event["record"]["roundId"]].append(event["record"])
        else:
            raise ValueError(f"Unsupported event type at {folder}")
    settlements = list(read_jsonl(folder / "rounds.jsonl"))
    require(len(settlements) == manifest["settledRounds"] == len(checkpoints), f"Round count mismatch at {folder}")
    require(set(records) == set(checkpoints) == {item["roundId"] for item in settlements}, f"Round identities mismatch at {folder}")
    require([item["roundGeneration"] for item in settlements] == list(range(1, len(settlements) + 1)), f"Missing round at {folder}")
    facts, previous = [], None
    for item in settlements:
        checkpoint = checkpoints[item["roundId"]]
        start = checkpoint["state"]["state"]["playerPoint"]
        require(previous is None or start == previous, f"Score continuity mismatch at {folder}")
        require(checkpoint["state"]["roundGeneration"] == item["roundGeneration"], f"Round generation mismatch at {folder}")
        facts.extend(round_facts(manifest, checkpoint, records[item["roundId"]], item["settlement"]))
        previous = item["settlement"]["pointsAfter"]
    require(previous == manifest["scores"] and sorted(manifest["ranking"]) == [0, 1, 2, 3], f"Final results mismatch at {folder}")
    return manifest, facts


LABELS = {
    "winRate": ("和牌率", "fraction"), "dealInRate": ("放铳率", "fraction"),
    "tsumoRate": ("自摸率", "fraction"), "riichiRate": ("立直率", "fraction"),
    "damaRate": ("默胡率", "fraction"), "drawRate": ("流局率", "fraction"),
    "drawTenpaiRate": ("流听率", "fraction"), "openRate": ("副露率", "fraction"),
    "averageWinningTurn": ("和了巡数", "turns"), "averageWinPoints": ("平均打点", "points"),
    "averageDealInPoints": ("平均铳点", "points"), "bankruptRate": ("被飞率", "fraction"),
    "averageRank": ("平均顺位", "rank"), "riichiDealInRate": ("立直放铳率", "fraction"),
    "riichiWinRate": ("立直和牌率", "fraction"), "firstRiichiRate": ("先制立直率", "fraction"),
    "averageRiichiTurn": ("立直巡目", "turns"), "riichiNetPoints": ("立直收支", "points/riichi round"),
    "pointEfficiency": ("打点效率", "points/round"), "dealInLoss": ("铳点损失", "points/round"),
    "netPointEfficiency": ("净打点效率", "points/round"), "roundNetPoints": ("局收支", "points/round"),
}


class Accumulator:
    def __init__(self, identity, definitions):
        self.identity = identity
        self.definitions = definitions
        self.counts = Counter()
        self.yaku = Counter()
        self.outcomes = Counter()
        self.matches = set()
        self.player_matches = set()
        self.configurations = set()
        self.names = set()
        self.ranks = Counter()

    def add_match(self, manifest, seat):
        observation = (manifest["matchId"], seat)
        if observation in self.player_matches:
            return
        self.player_matches.add(observation)
        self.matches.add(manifest["matchId"])
        rank = manifest["ranking"].index(seat) + 1
        self.ranks[rank] += 1
        self.counts["rankSum"] += rank
        self.counts["bankrupt"] += manifest["scores"][seat] < 0

    def add(self, fact, config_id):
        self.configurations.add(config_id)
        self.names.add(fact["name"])
        c = self.counts
        c["observedRounds"] += 1
        c["abortiveRounds"] += fact["abortive"]
        if fact["abortive"] and not self.definitions.include_abortive:
            c["excludedAbortiveRounds"] += 1
            return False
        c["rounds"] += 1
        self.outcomes[fact["outcome"]] += 1
        for field in ("won", "dealtIn", "tsumo", "ron", "riichi", "firstRiichi", "exhaustive", "tenpaiAtExhaustive",
                      "openHand", "closedNonriichiWin", "closedNonriichiRon", "nagashi"):
            c[field] += fact[field]
        c["netPoints"] += fact["netPoints"]
        if fact["won"]:
            turn = fact["ownDiscards"] + 1 if self.definitions.ron_turn == "next" and fact["ron"] else fact["winningTurn"]
            require(turn is not None, f"Winning action missing in {fact['roundId']}")
            c["winningTurnSum"] += turn
            c["winPoints"] += fact["winBasePoints"] if self.definitions.points == "base" else fact["winReceiptPoints"]
            self.yaku.update(fact["yaku"])
        c["dealInPoints"] += fact["dealInBasePoints"] if self.definitions.points == "base" else fact["dealInActualPoints"]
        if fact["riichi"]:
            c["riichiWon"] += fact["won"]
            c["riichiDealtIn"] += fact["dealtIn"]
            c["riichiTurnSum"] += fact["riichiTurn"]
            c["riichiNetPoints"] += fact["netPoints"]
        return True

    def result(self):
        c = self.counts
        n, wins, reaches = c["rounds"], c["won"], c["riichi"]
        metrics = {}

        def metric(key, numerator, denominator, formula):
            label, unit = LABELS[key]
            metrics[key] = {"label": label, "value": divide(numerator, denominator), "numerator": numerator,
                            "denominator": denominator, "unit": unit, "formula": formula}

        metric("winRate", wins, n, "和牌局数 / 计入局数")
        metric("dealInRate", c["dealtIn"], n, "放铳局数 / 计入局数；同一舍牌多家荣和仍为一次")
        metric("tsumoRate", c["tsumo"], wins if self.definitions.tsumo_denominator == "wins" else n,
               "自摸和牌次数 / " + ("和牌次数" if self.definitions.tsumo_denominator == "wins" else "计入局数"))
        metric("riichiRate", reaches, n, "成功立直局数 / 计入局数")
        dama_denominator = c["ron"] if self.definitions.dama == "closed-nonriichi-ron" else wins
        dama_count = c["closedNonriichiRon"] if self.definitions.dama == "closed-nonriichi-ron" else c["closedNonriichiWin"]
        metric("damaRate", dama_count, dama_denominator, "门前未立直" + ("荣和 / 荣和次数" if self.definitions.dama == "closed-nonriichi-ron" else "和牌（含自摸）/ 和牌次数"))
        metric("drawRate", c["exhaustive"] + (c["abortiveRounds"] - c["excludedAbortiveRounds"] if self.definitions.draws == "all" else 0), n,
               "荒牌流局" + ("及途中流局" if self.definitions.draws == "all" else "") + "局数 / 计入局数")
        metric("drawTenpaiRate", c["tenpaiAtExhaustive"], c["exhaustive"], "荒牌流局听牌次数 / 荒牌流局次数")
        metric("openRate", c["openHand"], n, "至少一次成功吃/碰/明杠的局数 / 计入局数；暗杠不算")
        metric("averageWinningTurn", c["winningTurnSum"], wins, "和牌巡目总和 / 和牌次数")
        metric("averageWinPoints", c["winPoints"], wins, "所选口径的和牌得点合计 / 和牌次数")
        metric("averageDealInPoints", c["dealInPoints"], c["dealtIn"], "所选口径的放铳损失合计 / 放铳局数")
        metric("bankruptRate", c["bankrupt"], len(self.player_matches), "最终点数 < 0 的玩家-整场数 / 玩家-整场数")
        metric("averageRank", c["rankSum"], len(self.player_matches), "最终名次总和 / 玩家-整场数")
        metric("riichiDealInRate", c["riichiDealtIn"], reaches, "成功立直后放铳局数 / 成功立直局数")
        metric("riichiWinRate", c["riichiWon"], reaches, "成功立直后和牌局数 / 成功立直局数")
        metric("firstRiichiRate", c["firstRiichi"], reaches, "本局首家成功立直次数 / 成功立直次数")
        metric("averageRiichiTurn", c["riichiTurnSum"], reaches, "成功立直宣言牌巡目总和 / 成功立直次数")
        metric("riichiNetPoints", c["riichiNetPoints"], reaches, "成功立直局的结算后点数减开局点数合计 / 成功立直局数")
        metric("pointEfficiency", c["winPoints"], n, "和牌率 × 平均打点；零和牌且有对局时为 0")
        metric("dealInLoss", c["dealInPoints"], n, "放铳率 × 平均铳点；零放铳且有对局时为 0")
        metric("netPointEfficiency", c["winPoints"] - c["dealInPoints"], n, "打点效率 − 铳点损失")
        metric("roundNetPoints", c["netPoints"], n, "Σ(结算后点数 − 开局点数) / 计入局数；含立直棒、本场、供托和末局剩余供托分配")
        return {"identity": self.identity, "configurationIds": sorted(self.configurations), "names": sorted(self.names),
                "sample": {"uniqueMatches": len(self.matches), "playerMatchObservations": len(self.player_matches),
                           "observedRounds": c["observedRounds"], "rounds": n, "abortiveRounds": c["abortiveRounds"],
                           "excludedAbortiveRounds": c["excludedAbortiveRounds"]},
                "metrics": metrics, "counts": dict(c), "rankCounts": dict(sorted(self.ranks.items())),
                "roundOutcomes": dict(self.outcomes), "yakuCounts": dict(self.yaku)}


def find_manifests(inputs):
    paths = set()
    for value in inputs:
        path = Path(value).resolve()
        require(path.exists(), f"Input does not exist: {path}")
        if path.is_file():
            require(path.name == "match.json", f"Input file must be match.json: {path}")
            paths.add(path)
        else:
            paths.update(path.rglob("match.json"))
    require(paths, "No match.json found in input")
    return sorted(paths)


def select(fact, names=(), seats=(), curses=(), model_sha=None, include_local=False):
    return ((fact["isModel"] or include_local) and (not names or any(fnmatch.fnmatchcase(fact["name"], name) for name in names))
            and (not seats or fact["seat"] in seats) and (not curses or fact["curse"] in curses)
            and (not model_sha or (fact["modelSha256"] or "").lower().startswith(model_sha.lower())))


def analyze(inputs, *, group_by="name", definitions=Definitions(), names=(), seats=(), curses=(), model_sha=None,
            include_local=False, merge_configurations=False, purposes=("experiment",)):
    manifests = find_manifests(inputs)
    groups, configs, observations, dispositions = {}, {}, [], []
    seen_ids = set()
    for path in manifests:
        manifest = read_json(path)
        require(manifest.get("schemaVersion", 1) == 1, f"Unsupported recording schema at {path}")
        identity = manifest["matchId"]
        require(identity not in seen_ids, f"Duplicate matchId {identity}; copied recordings must not be counted twice")
        seen_ids.add(identity)
        reason = None
        if manifest["status"] != "completed" or not manifest.get("matchEnded"):
            reason = "not_completed"
        elif manifest["purpose"] not in purposes:
            reason = "purpose_not_selected"
        elif manifest["purpose"] == "experiment" and not manifest.get("eligibleForStatistics"):
            reason = "not_eligible"
        if reason:
            dispositions.append({"matchId": identity, "path": str(path), "included": False, "reason": reason, "status": manifest["status"]})
            continue
        manifest, facts = load_match(path.parent)
        config_id, config = configuration(manifest)
        configs[config_id] = config
        selected = 0
        for fact in facts:
            if not select(fact, names, seats, curses, model_sha, include_local):
                continue
            value = fact[{"name": "name", "seat": "seat", "curse": "curse", "model": "modelSha256"}[group_by]]
            label = f"{group_by}:{value if value is not None else 'none'}"
            key = (label, None if merge_configurations else config_id)
            if key not in groups:
                groups[key] = Accumulator({"groupBy": group_by, "value": value, "label": label,
                                           "configurationId": key[1], "curseRuleVersion": config["curseRuleVersion"]}, definitions)
            group = groups[key]
            if group.identity["curseRuleVersion"] != config["curseRuleVersion"]:
                group.identity["curseRuleVersion"] = "mixed"
            group.add_match(manifest, fact["seat"])
            included = group.add(fact, config_id)
            observations.append({**fact, "configurationId": config_id, "group": label, "includedInRates": included})
            selected += 1
        dispositions.append({"matchId": identity, "path": str(path), "included": bool(selected),
                             "reason": None if selected else "no_selected_player", "status": manifest["status"]})
    require(groups, "No completed matching AI/player observations; inspect filters and selected purposes")
    results = [group.result() for key, group in sorted(groups.items(), key=lambda pair: (pair[0][0], pair[0][1] or ""))]
    return {"schemaVersion": 1, "statisticsVersion": VERSION, "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
            "definitions": asdict(definitions), "filters": {"groupBy": group_by, "names": names, "seats": seats, "curses": curses,
                "modelShaPrefix": model_sha, "includeLocal": include_local, "mergeConfigurations": merge_configurations, "purposes": purposes},
            "sourceSummary": {"discoveredMatches": len(manifests), "includedMatches": sum(row["included"] for row in dispositions),
                              "excludedReasons": dict(Counter(row["reason"] for row in dispositions if not row["included"]))},
            "configurations": configs, "groups": results, "sources": dispositions}, observations


def csv_file(path, rows, fields):
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            writer.writerow({key: json.dumps(value, ensure_ascii=False) if isinstance(value, (list, dict)) else value for key, value in row.items()})


def export(report, observations, output):
    output = Path(output).resolve()
    output.mkdir(parents=True, exist_ok=True)
    (output / "statistics.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    long_rows, wide_rows = [], []
    for group in report["groups"]:
        head = {"group": group["identity"]["label"], "configurationId": group["identity"]["configurationId"],
                "curseRuleVersion": group["identity"]["curseRuleVersion"], **group["sample"]}
        wide_rows.append({**head, **{metric["label"]: metric["value"] for metric in group["metrics"].values()}})
        for key, metric in group["metrics"].items():
            long_rows.append({**head, "metric": key, **metric})
    csv_file(output / "statistics.csv", wide_rows, list(wide_rows[0]))
    csv_file(output / "metric-details.csv", long_rows, list(long_rows[0]))
    csv_file(output / "round-observations.csv", observations, list(observations[0]))
    csv_file(output / "sources.csv", report["sources"], list(report["sources"][0]))
    text = ["# 玩家统计", "", "比率CSV/JSON使用0至1的小数；空值表示分母为0。下表显示百分比。", "",
            "计算口径：`" + json.dumps(report["definitions"], ensure_ascii=False) + "`。完整定义见脚本文档。", ""]
    for group in report["groups"]:
        text.extend(["## " + group["identity"]["label"], "", "诅咒规则：`" + str(group["identity"]["curseRuleVersion"]) + "`。配置：`" + str(group["identity"]["configurationId"] or "merged") + "`。",
                     "", f"{group['sample']['playerMatchObservations']}个玩家-整场样本，{group['sample']['rounds']}个玩家-本局样本。", "",
                     "| 指标 | 值 | 分子 | 分母 |", "| --- | ---: | ---: | ---: |"])
        for metric in group["metrics"].values():
            value = metric["value"]
            display = "—" if value is None else f"{value:.2%}" if metric["unit"] == "fraction" else f"{value:.2f}"
            text.append(f"| {metric['label']} | {display} | {metric['numerator']} | {metric['denominator']} |")
        text.append("")
    (output / "statistics.zh-CN.md").write_text("\n".join(text), encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", nargs="+", required=True, help="Batch roots, recording directories, or match.json files")
    parser.add_argument("--output", required=True)
    parser.add_argument("--group-by", choices=("name", "curse", "seat", "model"), default="name")
    parser.add_argument("--name", action="append", default=[], help="Player-name wildcard; repeat for OR")
    parser.add_argument("--seat", type=int, choices=range(4), action="append", default=[])
    parser.add_argument("--curse", type=int, choices=range(1, 5), action="append", default=[])
    parser.add_argument("--model-sha", help="Model SHA256 prefix")
    parser.add_argument("--include-local", action="store_true")
    parser.add_argument("--merge-configurations", action="store_true", help="Explicitly pool different code/rule/model-policy versions")
    parser.add_argument("--purpose", choices=("experiment", "validation", "benchmark"), action="append")
    parser.add_argument("--exclude-abortive", action="store_true")
    parser.add_argument("--draws", choices=("exhaustive", "all"), default="exhaustive")
    parser.add_argument("--tsumo-denominator", choices=("wins", "rounds"), default="wins")
    parser.add_argument("--dama", choices=("closed-nonriichi-wins", "closed-nonriichi-ron"), default="closed-nonriichi-wins")
    parser.add_argument("--points", choices=("base", "actual"), default="base")
    parser.add_argument("--ron-turn", choices=("current", "next"), default="current")
    args = parser.parse_args()
    definitions = Definitions(not args.exclude_abortive, args.draws, args.tsumo_denominator, args.dama, args.points, args.ron_turn)
    report, observations = analyze(args.input, group_by=args.group_by, definitions=definitions, names=args.name, seats=args.seat,
                                   curses=args.curse, model_sha=args.model_sha, include_local=args.include_local,
                                   merge_configurations=args.merge_configurations, purposes=args.purpose or ("experiment",))
    export(report, observations, args.output)
    print(json.dumps({"output": str(Path(args.output).resolve()), **report["sourceSummary"], "groups": len(report["groups"]),
                      "playerRounds": len(observations), "metricsPerGroup": len(LABELS)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
