"""Count actual scored yaku per winning hand, with bonus han kept separate."""
from collections import Counter, defaultdict
from datetime import datetime, timezone
import argparse
import csv
import json
from pathlib import Path


NAMES = {
    "Riichi": "立直", "DoubleRiichi": "两立直", "Ippatsu": "一发", "Tsumo": "门前清自摸和",
    "Rinshankaiho": "岭上开花", "HaiteiRaoyui": "海底捞月", "HoteiRaoyui": "河底捞鱼", "Chankan": "抢杠",
    "Jikaze": "自风牌", "Bakaze": "场风牌", "Haku": "白", "Hatsu": "发", "Chun": "中",
    "Pinfu": "平和", "Tanyao": "断幺九", "Iipeikou": "一杯口", "Ryanpeiko": "两杯口",
    "Ittsu": "一气通贯", "SanshokuDoujun": "三色同顺", "Chanta": "混全带幺九", "Junchan": "纯全带幺九",
    "SanshokuDoukou": "三色同刻", "Chiitoitsu": "七对子", "Toitoi": "对对和", "Honroutou": "混老头",
    "Sananko": "三暗刻", "Sankantsu": "三杠子", "ShoSanGen": "小三元", "Honitsu": "混一色", "Chinitsu": "清一色",
    "Suuankou": "四暗刻", "SuuankouTanki": "四暗刻单骑", "Tsuuiiso": "字一色", "DaiSanGen": "大三元",
    "ShoSuuShii": "小四喜", "DaiSuuShii": "大四喜", "Ryuiishoku": "绿一色", "Chinroto": "清老头", "Suukantsu": "四杠子",
    "Tenho": "天和", "Chiiho": "地和", "ChurenPoto": "九莲宝灯", "ChuurenPoutou": "纯正九莲宝灯",
    "KokushiMusou": "国士无双", "KokushiMusouThirteenOrphans": "国士无双十三面",
    "Dora": "宝牌", "AkaDora": "赤宝牌", "UraDora": "里宝牌", "KitaDora": "拔北宝牌",
}
BONUS = {"Dora", "AkaDora", "UraDora", "KitaDora"}


def csv_write(path, rows):
    with path.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)


def analyze(root, output):
    groups, hands, excluded, identities = {}, [], Counter(), set()
    match_count, rounds = 0, 0
    rule_versions = set()
    for path in sorted(root.rglob("match.json")):
        manifest = json.loads(path.read_text(encoding="utf-8-sig"))
        if manifest["matchId"] in identities:
            raise ValueError("Duplicate matchId: " + manifest["matchId"])
        identities.add(manifest["matchId"])
        if manifest["status"] != "completed" or not manifest.get("eligibleForStatistics"):
            excluded[manifest["status"]] += 1
            continue
        match_count += 1
        rule_versions.add(manifest.get("curseRuleVersion") or ("v1" if manifest.get("curses") else "none"))
        if len(rule_versions) > 1:
            raise ValueError("Mixed curse rule versions; analyze each version separately.")
        with (path.parent / "events.jsonl").open(encoding="utf-8") as stream:
            checkpoint = json.loads(stream.readline())
        names = [player["playerProfile"]["name"] for player in checkpoint["state"]["state"]["playerList"]]
        for line in (path.parent / "rounds.jsonl").read_text(encoding="utf-8").splitlines():
            item = json.loads(line)
            rounds += 1
            settlement = item["settlement"]
            if settlement["outcome"] not in ("Ron", "Tsumo"):
                continue
            for winner in settlement["winners"]:
                seat = winner["seat"]
                curse = (manifest.get("curses") or [None] * 4)[seat]
                yaku = winner["score"]["yaku"]
                if len({entry["yaku"] for entry in yaku}) != len(yaku):
                    raise ValueError("Repeated scored yaku in " + item["roundId"])
                hands.append({"matchId": manifest["matchId"], "roundId": item["roundId"], "seat": seat, "name": names[seat],
                              "curse": curse, "outcome": settlement["outcome"], "yaku": ";".join(entry["yaku"] for entry in yaku)})
                for grouping, label in (("curse", str(curse)), ("name", names[seat])):
                    key = grouping, label
                    if key not in groups:
                        groups[key] = {"wins": 0, "tsumo": 0, "matches": set(), "counts": Counter(), "han": Counter(), "yakuman": Counter()}
                    group = groups[key]
                    group["wins"] += 1
                    group["tsumo"] += settlement["outcome"] == "Tsumo"
                    group["matches"].add(manifest["matchId"])
                    for entry in yaku:
                        kind = entry["yaku"]
                        group["counts"][kind] += 1
                        group["han"][kind] += entry["han"]
                        group["yakuman"][kind] += entry["yakuman"]
    if not hands:
        raise ValueError("No completed eligible winning hands in input")
    records, totals = [], []
    for (grouping, label), group in sorted(groups.items()):
        totals.append({"groupBy": grouping, "group": label, "wins": group["wins"], "ron": group["wins"] - group["tsumo"], "tsumo": group["tsumo"],
                       "matchesWithWins": len(group["matches"])})
        kinds = list(NAMES) + sorted(set(group["counts"]) - set(NAMES))
        for kind in kinds:
            records.append({"groupBy": grouping, "group": label, "yaku": kind, "nameZh": NAMES.get(kind, kind), "bonus": kind in BONUS,
                            "count": group["counts"][kind], "wins": group["wins"], "shareOfWins": group["counts"][kind] / group["wins"],
                            "hanTotal": group["han"][kind], "yakumanUnits": group["yakuman"][kind]})
    output.mkdir(parents=True, exist_ok=True)
    csv_write(output / "yaku-statistics.csv", records)
    csv_write(output / "winning-hand-index.csv", hands)
    csv_write(output / "group-totals.csv", totals)
    report = {"generatedAtUtc": datetime.now(timezone.utc).isoformat(), "input": str(root), "completedMatches": match_count,
              "excluded": dict(excluded), "rounds": rounds, "winningHands": len(hands), "groups": totals,
              "curseRuleVersions": sorted(rule_versions),
              "definitions": {"count": "Winning hands containing this scored yaku; each type once per winner, multiple yaku may overlap.",
                              "denominator": "All actual Ron/Tsumo wins in this group; each winner in multi-Ron counts separately.",
                              "bonus": "Bonus entries listed separately: count is hands with bonus; hanTotal is actual bonus han sum.",
                              "yakuman": "One winning hand counted once even for double yakuman; yakumanUnits holds scored multiplicity.",
                              "exclusions": "Incomplete/ineligible matches excluded in full, including their settled prefix; abortive/exhaustive rounds are not ordinary wins.",
                              "scoredYaku": "Actual final score yaku only; suppressed ordinary yaku in yakuman are not imputed."}, "yaku": records}
    (output / "yaku-statistics.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    roles = {label: group for (by, label), group in groups.items() if by == "curse"}
    labels = sorted(roles)
    text = [f"# {match_count}场和牌役种统计", "", f"共{rounds}局、{len(hands)}次和牌。排除场次：{dict(excluded)}。", "",
            "按诅咒角色跨座位聚合；计数单位是一手和牌中出现该役，括号内为占该角色和牌次数的比例。同一和牌可有多个役，各行不可相加。", "",
            "| 项目 | " + " | ".join("诅咒" + label for label in labels) + " |", "| --- |" + " ---: |" * len(labels),
            "| 和牌数 | " + " | ".join(str(roles[label]["wins"]) for label in labels) + " |",
            "| 实际自摸数 | " + " | ".join(str(roles[label]["tsumo"]) for label in labels) + " |"]
    regular_kinds = [kind for kind in NAMES if kind not in BONUS and any(group["counts"][kind] for group in roles.values())]
    regular_kinds += sorted(set().union(*(set(group["counts"]) for group in roles.values())) - set(NAMES))
    for kind in regular_kinds:
        text.append("| " + NAMES.get(kind, kind) + " | " + " | ".join(f"{roles[label]['counts'][kind]}（{roles[label]['counts'][kind] / roles[label]['wins']:.2%}）" for label in labels) + " |")
    text += ["", "## 宝牌类", "", "每格为“出现手数 / 累计番数”，宝牌不是独立役。", "",
             "| 类型 | " + " | ".join("诅咒" + label for label in labels) + " |", "| --- |" + " ---: |" * len(labels)]
    for kind in ("Dora", "AkaDora", "UraDora", "KitaDora"):
        text.append("| " + NAMES[kind] + " | " + " | ".join(f"{roles[label]['counts'][kind]} / {roles[label]['han'][kind]}" for label in labels) + " |")
    text += ["", "门前清自摸和是役种，与实际自摸次数不同：副露自摸和役满自摸不会强行补入该役。役满时被计分器排除的普通役也不补算。", "",
             "国士普通形和十三面分开列；十三面按一手出现计数，双倍役满另记单位。CSV/JSON含所有支持役种（含0次）及固定名字玩家分组，方便复核。"]
    (output / "yaku-statistics.zh-CN.md").write_text("\n".join(text), encoding="utf-8")
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    result = analyze(args.input.resolve(), args.output.resolve())
    print(json.dumps({key: value for key, value in result.items() if key not in ("yaku", "definitions")}, ensure_ascii=False, indent=2))
