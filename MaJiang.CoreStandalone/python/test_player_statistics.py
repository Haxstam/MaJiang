"""Contract tests for score accounting, counters, selection and turn definitions."""
import copy
import json
from pathlib import Path
import tempfile
import unittest

from player_statistics import Accumulator, Definitions, analyze, configuration, round_facts


def action(seat, kind):
    return {"selfNumber": seat, "playerAction": kind}


def winner(seat=1, source=0, base=1000, payments=None):
    return {"seat": seat, "sourceSeat": source, "paoSeat": -1,
            "score": {"basePoints": base, "yaku": []},
            "payments": payments if payments is not None else [-4000, 4000, 0, 0]}


def fixture(actions=(), outcome="ExhaustiveDraw", winners=(), after=None, sticks_after=0, sticks_before=0, tenpai_mask=0,
            initial_points=None, match_id="match-a"):
    initial_points = initial_points or [25000] * 4
    after = after if after is not None else initial_points.copy()
    manifest = {"matchId": match_id, "status": "completed", "matchEnded": True, "eligibleForStatistics": True,
                "purpose": "experiment", "seedNumber": 1, "settledRounds": 1, "scores": after,
                "ranking": sorted(range(4), key=lambda seat: (-after[seat], seat)), "modelSeats": list(range(4)),
                "modelIdentity": {"sha256": "model-sha"}, "curses": [1, 2, 3, 4], "rules": {"basePoint": 25000},
                "coreAssemblySha256": "core-a"}
    state = {"playerPoint": initial_points, "riichiStickCount": 0, "honba": 0, "currentDealerIndex": 0,
             "playerOpenSetList": [[], [], [], []], "playerList": [{"playerProfile": {"name": "AI-" + str(seat)}} for seat in range(4)]}
    checkpoint = {"type": "round_start", "checkpointSequence": 2,
                  "state": {"state": state, "roundId": "round-a", "matchId": match_id, "roundGeneration": 1}}
    settlement = {"outcome": outcome, "winners": list(winners), "pointsAfter": after,
                  "sticksAfter": sticks_after, "sticksBefore": sticks_before, "tenpaiMask": tenpai_mask, "nagashiMask": 0}
    records = []

    def record(kind, value=None):
        records.append({"matchId": match_id, "roundId": "round-a", "sequence": len(records), "kind": kind,
                        "action": value, "settlement": settlement if kind == "RoundSettled" else None})
    record("RoundStarted")
    record("DoraRevealed")
    record("ActionApplied", action(0, "Take"))
    for value in actions:
        record("ActionApplied", value)
    record("RoundSettled")
    return manifest, checkpoint, records, settlement


def facts(data):
    return list(round_facts(*data))


def group(data, seat=0, definitions=Definitions()):
    accumulator = Accumulator({"label": "test"}, definitions)
    accumulator.add_match(data[0], seat)
    accumulator.add(facts(data)[seat], "config")
    return accumulator.result()


def value(result, name):
    return result["metrics"][name]["value"]


def save_fixture(root, name, data):
    folder = root / name
    folder.mkdir()
    manifest, checkpoint, records, settlement = data
    (folder / "match.json").write_text(json.dumps(manifest), encoding="utf-8")
    (folder / "events.jsonl").write_text("\n".join(json.dumps(item) for item in [checkpoint] + [{"type": "applied", "record": record} for record in records]) + "\n", encoding="utf-8")
    (folder / "rounds.jsonl").write_text(json.dumps({"roundId": "round-a", "roundGeneration": 1, "settlement": settlement}) + "\n", encoding="utf-8")


class PlayerStatisticsChecks(unittest.TestCase):
    def test_riichi_cost_uses_opening_score_not_settlement_delta(self):
        data = fixture([action(0, "TakeCut"), action(0, "Riichi")], after=[24000, 26000, 25000, 25000], sticks_before=1)
        result = group(data)
        self.assertEqual(value(result, "roundNetPoints"), -1000)
        self.assertEqual(value(result, "riichiNetPoints"), -1000)
        self.assertEqual(value(result, "riichiRate"), 1)
        self.assertEqual(value(result, "firstRiichiRate"), 1)
        self.assertEqual(value(result, "averageRiichiTurn"), 1)

    def test_double_ron_is_one_deal_in_with_combined_loss(self):
        data = fixture([action(0, "TakeCut"), action(0, "Riichi"), action(1, "Ron"), action(2, "Ron")],
                       outcome="Ron", winners=[winner(payments=[-4000, 5000, 0, 0]), winner(2, 0, 2000, [-8000, 0, 8000, 0])],
                       after=[12000, 30000, 33000, 25000], sticks_before=1)
        result = group(data)
        self.assertEqual(result["counts"]["dealtIn"], 1)
        self.assertEqual(value(result, "averageDealInPoints"), 12000)
        self.assertEqual(value(result, "dealInRate"), 1)
        self.assertEqual(value(result, "netPointEfficiency"), -12000)
        self.assertEqual(value(result, "roundNetPoints"), -13000)
        self.assertEqual(value(result, "riichiDealInRate"), 1)
        self.assertEqual(value(group(data, 1), "averageWinPoints"), 4000)
        self.assertEqual(value(group(data, 1, Definitions(points="actual")), "averageWinPoints"), 5000)

    def test_declaration_tile_ron_does_not_invent_established_riichi(self):
        data = fixture([action(0, "HandCut"), action(1, "Ron")], outcome="Ron", winners=[winner()], after=[21000, 29000, 25000, 25000])
        self.assertEqual(value(group(data), "riichiRate"), 0)
        self.assertIsNone(value(group(data), "riichiDealInRate"))
        self.assertEqual(value(group(data, 1), "damaRate"), 1)
        self.assertEqual(value(group(data, 1), "averageWinningTurn"), 1)

    def test_kan_replacements_do_not_add_turns_and_closed_kan_is_closed(self):
        data = fixture([action(0, "HandCut"), action(1, "Take"), action(1, "ClosedKan"), action(1, "Take"), action(1, "Tsumo")],
                       outcome="Tsumo", winners=[winner(1, 1, 1000, [-2000, 4000, -1000, -1000])], after=[23000, 29000, 24000, 24000])
        result = group(data, 1)
        self.assertEqual(value(result, "averageWinningTurn"), 1)
        self.assertEqual(value(result, "openRate"), 0)
        self.assertEqual(value(result, "tsumoRate"), 1)
        self.assertEqual(value(result, "damaRate"), 1)
        self.assertIsNone(value(group(data, 1, Definitions(dama="closed-nonriichi-ron")), "damaRate"))

    def test_call_turns_and_multiple_rinshan_stay_one_turn(self):
        data = fixture([action(0, "HandCut"), action(2, "Chi"), action(2, "HandCut"), action(0, "Take"), action(0, "HandCut"),
                        action(2, "OpenKan"), action(2, "Take"), action(2, "ClosedKan"), action(2, "Take"), action(2, "Tsumo")],
                       outcome="Tsumo", winners=[winner(2, 2, 1000, [-2000, -1000, 4000, -1000])], after=[23000, 24000, 29000, 24000])
        result = group(data, 2)
        self.assertEqual(value(result, "averageWinningTurn"), 2)
        self.assertEqual(value(result, "openRate"), 1)
        self.assertEqual(value(result, "damaRate"), 0)

    def test_ron_turn_is_explicitly_configurable(self):
        data = fixture([action(0, "HandCut"), action(1, "Take"), action(1, "TakeCut"), action(2, "Take"), action(2, "HandCut"), action(1, "Ron")],
                       outcome="Ron", winners=[winner(1, 2, 1000, [0, 4000, -4000, 0])], after=[25000, 29000, 21000, 25000])
        self.assertEqual(value(group(data, 1), "averageWinningTurn"), 1)
        self.assertEqual(value(group(data, 1, Definitions(ron_turn="next")), "averageWinningTurn"), 2)

    def test_abortive_and_exhaustive_denominators_are_distinct(self):
        data = fixture([action(0, "Draw")], outcome="NineTerminals")
        self.assertEqual(value(group(data), "drawRate"), 0)
        self.assertEqual(value(group(data, definitions=Definitions(draws="all")), "drawRate"), 1)
        excluded = group(data, definitions=Definitions(include_abortive=False))
        self.assertIsNone(value(excluded, "winRate"))
        self.assertEqual(excluded["sample"]["excludedAbortiveRounds"], 1)
        self.assertIsNone(value(group(data), "drawTenpaiRate"))
        tenpai = fixture(tenpai_mask=1)
        self.assertEqual(value(group(tenpai), "drawTenpaiRate"), 1)
        self.assertEqual(value(group(tenpai, 1), "drawTenpaiRate"), 0)

    def test_empty_conditionals_are_null_but_efficiencies_are_zero(self):
        result = group(fixture())
        for key in ("averageWinPoints", "averageDealInPoints", "averageWinningTurn", "riichiNetPoints", "firstRiichiRate", "tsumoRate"):
            self.assertIsNone(value(result, key))
        for key in ("pointEfficiency", "dealInLoss", "netPointEfficiency", "roundNetPoints"):
            self.assertEqual(value(result, key), 0)

    def test_pao_deal_in_uses_actual_discarder_payment(self):
        w = winner(1, 0, 8000, [-16000, 32300, -16300, 0])
        w["paoSeat"] = 2
        data = fixture([action(0, "HandCut"), action(1, "Ron")], outcome="Ron", winners=[w], after=[9000, 57300, 8700, 25000])
        data[1]["state"]["state"]["honba"] = 1
        self.assertEqual(value(group(data), "averageDealInPoints"), 16000)
        self.assertEqual(value(group(data, 2), "dealInRate"), 0)
        self.assertEqual(value(group(data, 2), "roundNetPoints"), -16300)
        self.assertEqual(value(group(data, 1), "averageWinPoints"), 32000)

    def test_zero_points_is_not_bankrupt_and_rank_is_per_player_match(self):
        accumulator = Accumulator({"label": "model"}, Definitions())
        manifest = fixture()[0]
        manifest["scores"] = [-1, 0, 25000, 75001]
        manifest["ranking"] = [3, 2, 1, 0]
        for seat in range(4):
            accumulator.add_match(manifest, seat)
            accumulator.add_match(manifest, seat)
        result = accumulator.result()
        self.assertEqual(value(result, "bankruptRate"), 0.25)
        self.assertEqual(value(result, "averageRank"), 2.5)
        self.assertEqual(result["sample"]["uniqueMatches"], 1)
        self.assertEqual(result["sample"]["playerMatchObservations"], 4)

    def test_only_first_established_riichi_is_first(self):
        data = fixture([action(0, "TakeCut"), action(0, "Riichi"), action(1, "Take"), action(1, "TakeCut"), action(1, "Riichi"), action(2, "Ron")],
                       outcome="Ron", winners=[winner(2, 0, 1000, [-4000, 0, 6000, 0])], after=[20000, 24000, 31000, 25000], sticks_before=2)
        self.assertEqual(value(group(data, 0), "firstRiichiRate"), 1)
        self.assertEqual(value(group(data, 1), "firstRiichiRate"), 0)

    def test_input_selection_config_separation_and_exclusion(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            one = fixture()
            two = fixture(match_id="match-b")
            two[0]["coreAssemblySha256"] = "core-b"
            save_fixture(root, "a", one)
            save_fixture(root, "b", two)
            failed = root / "failed"
            failed.mkdir()
            (failed / "match.json").write_text(json.dumps({"matchId": "bad", "status": "failed"}), encoding="utf-8")
            report, observations = analyze([root], names=["AI-0"], curses=[1])
            self.assertEqual(len(report["groups"]), 2)
            self.assertEqual(len(observations), 2)
            self.assertEqual(report["sourceSummary"]["excludedReasons"], {"not_completed": 1})
            merged, _ = analyze([root], names=["AI-0"], merge_configurations=True)
            self.assertEqual(len(merged["groups"]), 1)
            self.assertEqual(merged["groups"][0]["sample"]["playerMatchObservations"], 2)
            save_fixture(root, "duplicate", copy.deepcopy(one))
            with self.assertRaisesRegex(ValueError, "Duplicate matchId"):
                analyze([root])

    def test_missing_facts_or_midround_checkpoints_fail_loudly(self):
        data = fixture()
        data[2][2]["sequence"] = 99
        with self.assertRaisesRegex(ValueError, "Record gap"):
            facts(data)
        data = fixture([action(0, "HandCut")])
        data[1]["checkpointSequence"] = 3
        with self.assertRaisesRegex(ValueError, "not a round opening"):
            facts(data)

    def test_curse_versions_are_backward_compatible_and_never_implicitly_pooled(self):
        one = fixture()
        explicit = copy.deepcopy(one[0])
        explicit["curseRuleVersion"] = "v1"
        self.assertEqual(configuration(one[0])[0], configuration(explicit)[0])
        two = fixture(match_id="match-v2")
        two[0]["curseRuleVersion"] = "v2"
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            save_fixture(root, "v1", one)
            save_fixture(root, "v2", two)
            report, _ = analyze([root], names=["AI-0"])
            self.assertEqual(len(report["groups"]), 2)
            self.assertEqual({group["identity"]["curseRuleVersion"] for group in report["groups"]}, {"v1", "v2"})
            merged, _ = analyze([root], names=["AI-0"], merge_configurations=True)
            self.assertEqual(merged["groups"][0]["identity"]["curseRuleVersion"], "mixed")
            from yaku_statistics import analyze as analyze_yaku
            with self.assertRaisesRegex(ValueError, "Mixed curse rule versions"):
                analyze_yaku(root, root / "output")


if __name__ == "__main__":
    unittest.main()
