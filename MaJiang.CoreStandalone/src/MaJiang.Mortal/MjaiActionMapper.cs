using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using MaJiangLib;

namespace MaJiang.Mortal
{
    /// <summary>只匹配本地当前合法菜单，不把未经校验的模型动作交给核心的非法输入默认路径。</summary>
    public static class MjaiActionMapper
    {
        public static MatchCommand Match(JsonObject reaction, VisibleMatchState view, IReadOnlyList<MatchCommand> choices, bool riichiDiscard = false)
        {
            if (reaction == null) { return null; }
            string type = reaction["type"]?.GetValue<string>();
            if (reaction["actor"] != null && reaction["actor"].GetValue<int>() != view.SelfSeat) { return null; }
            if (type != "none" && type != "ryukyoku" && reaction["actor"] == null) { return null; }
            Tile pai = reaction["pai"] == null ? null : MjaiTiles.Parse(reaction["pai"].GetValue<string>());
            List<Tile> consumed = reaction["consumed"] is JsonArray array
                ? array.Select(node => MjaiTiles.Parse(node.GetValue<string>())).ToList() : null;
            int? target = reaction["target"]?.GetValue<int>();
            bool? tsumogiri = reaction["tsumogiri"]?.GetValue<bool>();
            return choices.FirstOrDefault(command =>
            {
                PlayerActionData action = command.Action;
                switch (type)
                {
                    case "none": return !riichiDiscard && action.PlayerAction == PlayerAction.Skip;
                    case "dahai":
                        if (pai == null || !tsumogiri.HasValue || !Tile.ExactlyEquals(action.TargetTile, pai)) { return false; }
                        if (riichiDiscard)
                        {
                            bool fromDraw = Tile.ExactlyEquals(view.State.PlayerList[view.SelfSeat].PlayerHandTile.SingleTile, pai);
                            return action.PlayerAction == PlayerAction.Riichi && tsumogiri == fromDraw;
                        }
                        return action.PlayerAction == (tsumogiri.Value ? PlayerAction.TakeCut : PlayerAction.HandCut);
                    case "chi": return Call(PlayerAction.Chi);
                    case "pon": return Call(PlayerAction.Peng);
                    case "daiminkan": return Call(PlayerAction.OpenKan);
                    case "ankan": return !riichiDiscard && action.PlayerAction == PlayerAction.ClosedKan && SameTiles(action.Tiles, consumed);
                    case "kakan":
                        if (riichiDiscard || action.PlayerAction != PlayerAction.AddedKan || !Tile.ExactlyEquals(action.TargetTile, pai)) { return false; }
                        Group pon = view.State.PlayerOpenSetList[view.SelfSeat].FirstOrDefault(group => group.GroupType == GroupType.OpenTriple && group.Tiles[0] == pai);
                        return pon != null && SameTiles(pon.Tiles, consumed);
                    case "hora": return !riichiDiscard && target.HasValue && action.TargetPlayerNumber == target.Value &&
                        action.PlayerAction == (target == view.SelfSeat ? PlayerAction.Tsumo : PlayerAction.Ron);
                    case "ryukyoku": return !riichiDiscard && action.PlayerAction == PlayerAction.Draw;
                    default: return false;
                }

                bool Call(PlayerAction expected) => !riichiDiscard && action.PlayerAction == expected && target == action.TargetPlayerNumber &&
                    Tile.ExactlyEquals(action.TargetTile, pai) && SameTiles(action.Tiles, consumed);
            });
        }

        internal static bool SameTiles(IEnumerable<Tile> left, IEnumerable<Tile> right)
        {
            if (left == null || right == null) { return false; }
            Tile[] a = left.OrderBy(tile => tile).ToArray(), b = right.OrderBy(tile => tile).ToArray();
            return a.Length == b.Length && a.Zip(b, Tile.ExactlyEquals).All(equal => equal);
        }

        internal static MatchCommand Copy(MatchCommand source) => new()
        {
            MatchId = source.MatchId, RoundId = source.RoundId, RoundGeneration = source.RoundGeneration,
            PhaseId = source.PhaseId, CommandId = source.CommandId,
            Action = new PlayerActionData(source.Action.SelfNumber, source.Action.TargetPlayerNumber,
                source.Action.Tiles?.ToList() ?? new(), source.Action.TargetTile, source.Action.PlayerAction)
        };
    }
}
