using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MaJiangLib;

namespace MaJiang.Mortal
{
    /// <summary>协议事件有独立序号：包含已公开宣告，不等同于核心的最终成功动作日志。</summary>
    internal sealed class MjaiObservation
    {
        public long Index;
        public Guid RoundId;
        public long PhaseId;
        public int Targets;
        public int PreviewedSeat = -1;
        public JsonObject Payload;

        public JsonObject ForSeat(int seat, bool canAct)
        {
            JsonObject result = (JsonObject)Payload.DeepClone();
            if (result["type"]?.GetValue<string>() == "start_kyoku")
            {
                JsonArray hands = (JsonArray)result["tehais"];
                for (int other = 0; other < 4; other++)
                {
                    if (other != seat) { hands[other] = new JsonArray(Enumerable.Range(0, 13).Select(_ => (JsonNode)JsonValue.Create("?")).ToArray()); }
                }
            }
            if (result["type"]?.GetValue<string>() == "tsumo" && result["actor"].GetValue<int>() != seat) { result["pai"] = "?"; }
            result["can_act"] = canAct;
            return result;
        }
    }

    /// <summary>把已执行结果与真实公开宣告组合成 mjai 事件；未裁定的吃碰意向永不发送。</summary>
    internal sealed class MjaiEventTranslator
    {
        private PlayerActionData announcedKan;

        public static JsonObject Event(string type) => new() { ["type"] = type };
        private static JsonArray Tiles(IEnumerable<Tile> tiles) => new(tiles.Select(tile => (JsonNode)JsonValue.Create(MjaiTiles.Format(tile))).ToArray());
        private static JsonArray Numbers(IEnumerable<int> points) => new(points.Select(point => (JsonNode)JsonValue.Create(point)).ToArray());

        public static JsonObject StartGame(MatchState state) => new()
        {
            ["type"] = "start_game",
            ["names"] = new JsonArray(state.State.PlayerList.Select(player => (JsonNode)JsonValue.Create(player.PlayerProfile.Name ?? "")).ToArray())
        };

        public List<JsonObject> StartRound(MatchState state, IReadOnlyList<MatchRecord> records)
        {
            MainMatchControl core = state.State;
            if (core.PlayerList.Any(player => player.PlayerHandTile.HandTileList.Count != 13 || player.PlayerHandTile.OpenTileList.Count != 0) ||
                core.DoraPointerList.Count != 1 || records.Any(record => record.Kind == MatchRecordKind.ActionApplied && record.Action.PlayerAction != PlayerAction.Take) ||
                records.Count(record => record.Action?.PlayerAction == PlayerAction.Take) != 1)
            {
                throw new InvalidOperationException("Attach the adapter at round start, before any player operation. Mid-round recovery requires this adapter's event history.");
            }
            announcedKan = null;
            List<JsonObject> events = new()
            {
                new JsonObject
                {
                    ["type"] = "start_kyoku", ["bakaze"] = new[] { "E", "S", "W", "N" }[(int)core.Wind - 1],
                    ["kyoku"] = core.Round, ["honba"] = core.Honba, ["kyotaku"] = core.RiichiStickCount,
                    ["oya"] = core.CurrentDealerIndex, ["scores"] = Numbers(core.PlayerPoint),
                    ["dora_marker"] = MjaiTiles.Format(core.DoraPointerList[0]),
                    ["tehais"] = new JsonArray(core.PlayerList.Select(player => (JsonNode)Tiles(player.PlayerHandTile.HandTileList)).ToArray())
                }
            };
            foreach (MatchRecord record in records.Where(record => record.Action?.PlayerAction == PlayerAction.Take))
            {
                events.Add(ActionEvent(record.Action, core));
            }
            return events;
        }

        public List<JsonObject> Translate(MatchState before, MatchState after, OperationFeedback feedback, PlayerActionData acceptedInput)
        {
            List<JsonObject> events = new();
            int next = 0;
            bool declaresKan = acceptedInput?.PlayerAction == PlayerAction.AddedKan || acceptedInput?.PlayerAction == PlayerAction.ClosedKan;
            if (declaresKan)
            {
                // A previous kan's delayed indicator is revealed before the next kan is announced.
                while (next < feedback.Records.Count && feedback.Records[next].Kind == MatchRecordKind.DoraRevealed)
                {
                    events.Add(Dora(feedback.Records[next++].Indicator));
                }
                events.Add(ActionEvent(acceptedInput, before.State));
                announcedKan = new PlayerActionData(acceptedInput.SelfNumber, acceptedInput.TargetPlayerNumber,
                    acceptedInput.Tiles.ToList(), acceptedInput.TargetTile, acceptedInput.PlayerAction);
            }
            if (acceptedInput?.PlayerAction == PlayerAction.Riichi)
            {
                events.Add(new JsonObject { ["type"] = "reach", ["actor"] = acceptedInput.SelfNumber });
            }
            foreach (MatchRecord record in feedback.Records.Skip(next))
            {
                if (record.Kind == MatchRecordKind.DoraRevealed) { events.Add(Dora(record.Indicator)); continue; }
                if (record.Kind == MatchRecordKind.RoundSettled)
                {
                    events.Add(Event("end_kyoku")); announcedKan = null;
                    if (record.Settlement.MatchEnded) { events.Add(Event("end_game")); }
                    continue;
                }
                if (record.Kind != MatchRecordKind.ActionApplied) { continue; }
                PlayerActionData action = record.Action;
                if (action.PlayerAction == PlayerAction.AddedKan || action.PlayerAction == PlayerAction.ClosedKan)
                {
                    if (announcedKan == null || announcedKan.PlayerAction != action.PlayerAction || announcedKan.SelfNumber != action.SelfNumber ||
                        !MjaiEventTranslator.SameKan(announcedKan, action))
                    {
                        throw new InvalidOperationException("Successful kan has no matching prior public declaration.");
                    }
                    announcedKan = null; continue; // The protocol already consumed the declaration exactly once.
                }
                JsonObject evt = ActionEvent(action, before.State);
                if (action.PlayerAction == PlayerAction.Ron || action.PlayerAction == PlayerAction.Tsumo)
                {
                    WinnerSettlement winner = after.Settlement.Winners.Single(item => item.Seat == action.SelfNumber);
                    evt["deltas"] = Numbers(winner.Payments); evt["ura_markers"] = Tiles(after.Settlement.UraIndicators);
                }
                if (action.PlayerAction == PlayerAction.Draw) { evt["deltas"] = Numbers(after.Settlement.Deltas); }
                events.Add(evt);
            }
            return events;
        }

        private static bool SameKan(PlayerActionData a, PlayerActionData b) => Tile.ExactlyEquals(a.TargetTile, b.TargetTile) && MjaiActionMapper.SameTiles(a.Tiles, b.Tiles);
        private static JsonObject Dora(Tile marker) => new() { ["type"] = "dora", ["dora_marker"] = MjaiTiles.Format(marker) };

        private static JsonObject ActionEvent(PlayerActionData action, MainMatchControl before)
        {
            JsonObject evt = new() { ["actor"] = action.SelfNumber };
            switch (action.PlayerAction)
            {
                case PlayerAction.Take: evt["type"] = "tsumo"; evt["pai"] = MjaiTiles.Format(action.TargetTile); break;
                case PlayerAction.HandCut:
                case PlayerAction.TakeCut:
                    evt["type"] = "dahai"; evt["pai"] = MjaiTiles.Format(action.TargetTile); evt["tsumogiri"] = action.PlayerAction == PlayerAction.TakeCut; break;
                case PlayerAction.Chi:
                case PlayerAction.Peng:
                case PlayerAction.OpenKan:
                    evt["type"] = action.PlayerAction == PlayerAction.Chi ? "chi" : action.PlayerAction == PlayerAction.Peng ? "pon" : "daiminkan";
                    evt["target"] = action.TargetPlayerNumber; evt["pai"] = MjaiTiles.Format(action.TargetTile); evt["consumed"] = Tiles(action.Tiles); break;
                case PlayerAction.AddedKan:
                    evt["type"] = "kakan"; evt["pai"] = MjaiTiles.Format(action.TargetTile);
                    evt["consumed"] = Tiles(before.PlayerOpenSetList[action.SelfNumber].Single(group => group.GroupType == GroupType.OpenTriple && group.Tiles[0] == action.TargetTile).Tiles); break;
                case PlayerAction.ClosedKan: evt["type"] = "ankan"; evt["consumed"] = Tiles(action.Tiles); break;
                case PlayerAction.Riichi: evt["type"] = "reach_accepted"; break;
                case PlayerAction.Ron:
                case PlayerAction.Tsumo: evt["type"] = "hora"; evt["target"] = action.TargetPlayerNumber; break;
                case PlayerAction.Draw: return Event("ryukyoku");
                default: throw new InvalidOperationException("Unsupported executed action: " + action.PlayerAction);
            }
            return evt;
        }

        public static int DecisionTargets(JsonObject evt, MatchState current)
        {
            string type = evt["type"]?.GetValue<string>();
            int actor = evt["actor"]?.GetValue<int>() ?? -1;
            int mask = 0;
            foreach (int seat in current.PendingSeats)
            {
                bool relevant = type switch
                {
                    "tsumo" => current.OperationStage == MatchOperationStage.Discard && seat == actor && current.State.CurrentPlayerIndex == actor,
                    "chi" or "pon" => current.OperationStage == MatchOperationStage.DiscardAfterCall && seat == actor,
                    "dahai" => current.OperationStage == MatchOperationStage.DiscardResponse && current.ResponseSourceSeat == actor && seat != actor,
                    "kakan" => current.OperationStage == MatchOperationStage.AddedKanResponse && current.ResponseSourceSeat == actor && seat != actor,
                    "ankan" => current.OperationStage == MatchOperationStage.ClosedKanResponse && current.ResponseSourceSeat == actor && seat != actor,
                    _ => false
                };
                if (relevant) { mask |= 1 << seat; }
            }
            return mask;
        }
    }
}
