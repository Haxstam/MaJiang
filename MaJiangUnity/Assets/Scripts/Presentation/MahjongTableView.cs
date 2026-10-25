using System;
using System.Collections.Generic;
using System.Linq;
using MaJiangLib;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace MaJiang.Presentation
{
    /// <summary>只读取接收者可见状态的牌桌视图；背景、牌河容器和面板均为独立可编辑的场景对象。</summary>
    [ExecuteAlways]
    public sealed class MahjongTableView : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerPanel
        {
            public Text Name;
            public Text Points;
            public Text Wind;
            public GameObject Riichi;
            public TableShape TurnBorder;
        }
        public MahjongTileArt Art;
        public MahjongTileWidget TilePrefab;
        public RectTransform[] Hands = new RectTransform[4];
        public RectTransform[] Rivers = new RectTransform[4];
        public RectTransform[] Melds = new RectTransform[4];
        public PlayerPanel[] Players = new PlayerPanel[4];
        public RectTransform Indicators;
        public Text Round;
        public Text Counters;
        public Text Remaining;
        public Text Timer;
        public Text Hint;
        public Text Status;
        public Text HeaderState;
        public Button[] Actions;
        public Text[] ActionLabels;
        public Button RoomButton;
        public Button ResetButton;
        public GameObject Modal;
        public Text ModalEyebrow;
        public Text ModalTitle;
        public Text ModalScore;
        public Text ModalDetail;
        public RectTransform ModalHand;
        public Button ModalClose;
        public Button ModalReset;
        private readonly List<MahjongTileWidget> handWidgets = new();
        public event Action<MahjongTileWidget> TileClicked;
        private static readonly string[] Winds = { "東", "南", "西", "北" };

        private void OnEnable() => ApplyFonts();
        public void ApplyFonts()
        {
            if (Art == null) { return; }
            foreach (Text text in GetComponentsInChildren<Text>(true)) { text.font = Art.GetFont(); }
        }

        /// <summary>手牌仅从SelfSeat读取，其他座位永远以公开张数显示牌背。</summary>
        public void Render(VisibleMatchState visible, int[] riichiDiscards, int selected, string hint)
        {
            MainMatchControl state = visible.State;
            ApplyFonts();
            Round.text = $"{Winds[(int)state.Wind - 1]} {new[] { "一", "二", "三", "四" }[state.Round - 1]} 局";
            Counters.text = $"{state.Honba} 本场     ·     {state.RiichiStickCount} 供托";
            Remaining.text = state.RemainTileCount.ToString("00");
            Timer.text = visible.OperationStage == MatchOperationStage.RoundEnded ? "—" : "25";
            Hint.text = hint;
            HeaderState.text = $"四人东南战  /  {visible.RoundGeneration:00}";
            Status.text = visible.OperationStage == MatchOperationStage.RoundEnded ? "本局结束"
                : state.CurrentPlayerIndex == visible.SelfSeat ? "你的回合" : "等待响应";
            for (int relative = 0; relative < 4; relative++)
            {
                int seat = (visible.SelfSeat + relative) % 4;
                PlayerPanel player = Players[relative];
                player.Name.text = state.PlayerList[seat].PlayerProfile.Name;
                player.Points.text = state.PlayerPoint[seat].ToString("N0");
                player.Wind.text = Winds[(seat - state.CurrentDealerIndex + 4) % 4];
                player.Riichi.SetActive(state.IsRiichi[seat]);
                player.TurnBorder.gameObject.SetActive(state.CurrentPlayerIndex == seat && visible.OperationStage != MatchOperationStage.RoundEnded);
                DrawHand(visible, seat, relative, selected);
                DrawRiver(Rivers[relative], state.DiscardTileList[seat], riichiDiscards[seat]);
                DrawMelds(Melds[relative], state.PlayerOpenSetList[seat]);
            }
            Clear(Indicators);
            for (int index = 0; index < 5; index++)
            {
                bool opened = index < state.DoraPointerList.Count;
                MahjongTileWidget tile = AddTile(Indicators, opened ? state.DoraPointerList[index] : null, 21 + index * 45, 31, 38, 50.7f, !opened);
                if (!opened) { tile.Body.color = new Color(0.31f, 0.36f, 0.32f, 0.35f); }
            }
            SetSelected(selected);
        }

        private void DrawHand(VisibleMatchState visible, int seat, int relative, int selected)
        {
            RectTransform parent = Hands[relative]; Clear(parent);
            if (relative == 0) { handWidgets.Clear(); }
            HandTile hand = visible.State.PlayerList[seat].PlayerHandTile;
            bool self = seat == visible.SelfSeat;
            int concealed = self ? hand.HandTileList.Count : visible.HandCounts[seat];
            int count = concealed + (self && hand.SingleTile != null ? 1 : 0);
            float width = self ? 62 : 30;
            float height = width * 4 / 3;
            float gap = self ? 4 : 3;
            float drawGap = self && hand.SingleTile != null ? 21 : 0;
            float total = count * (width + gap) - gap + drawGap;
            for (int index = 0; index < count; index++)
            {
                bool drawn = self && index == concealed;
                Tile value = !self ? null : drawn ? hand.SingleTile : hand.HandTileList[index];
                float x = (parent.rect.width - total) / 2 + width / 2 + index * (width + gap) + (drawn ? drawGap : 0);
                bool dora = value != null && (value.IsRedDora || visible.State.DoraPointerList.Any(pointer => RiichiScoring.NextDora(HandAnalysis.Face(pointer)) == HandAnalysis.Face(value)));
                MahjongTileWidget tile = AddTile(parent, value, x, parent.rect.height / 2, width, height, !self, dora);
                tile.HandIndex = index; tile.Drawn = drawn; tile.Interactive = self && visible.OperationStage != MatchOperationStage.RoundEnded;
                if (self) { tile.Clicked += item => TileClicked?.Invoke(item); handWidgets.Add(tile); }
            }
        }

        private void DrawRiver(RectTransform parent, IReadOnlyList<Tile> river, int riichiAt)
        {
            Clear(parent);
            int rows = Mathf.Max(1, Mathf.CeilToInt(river.Count / 6f));
            float scale = Mathf.Min(1, 3f / rows), width = 40 * scale, height = width * 4 / 3;
            float pitchX = 44 * scale, pitchY = 57.3f * scale;
            float startY = (parent.rect.height - rows * pitchY) / 2 + height / 2;
            for (int index = 0; index < river.Count; index++)
            {
                int row = index / 6, col = index % 6;
                bool hasReach = riichiAt >= 0 && riichiAt / 6 == row;
                float rowWidth = 6 * pitchX + (hasReach ? 14 * scale : 0);
                float x = (parent.rect.width - rowWidth) / 2 + width / 2 + col * pitchX;
                if (hasReach && index > riichiAt) { x += 14 * scale; }
                MahjongTileWidget tile = AddTile(parent, river[index], x, startY + row * pitchY, width, height);
                if (index == riichiAt)
                {
                    tile.transform.localEulerAngles = new Vector3(0, 0, 90);
                    tile.Selection.color = new Color(0.79f, 0.38f, 0.30f); tile.Selection.gameObject.SetActive(true);
                }
            }
        }

        private void DrawMelds(RectTransform parent, IReadOnlyList<Group> groups)
        {
            Clear(parent); int index = 0;
            foreach (Group group in groups)
            {
                foreach (Tile tile in group.Tiles) { AddTile(parent, tile, 16 + index++ * 34, 22, 31, 41.3f); }
                index++;
            }
        }

        public void SetSelected(int selected)
        {
            foreach (MahjongTileWidget tile in handWidgets) { tile.Select(tile.HandIndex == selected); }
        }

        public void SetActions(string[] labels, bool[] available)
        {
            int count = available.Count(enabled => enabled), position = 0;
            for (int index = 0; index < Actions.Length; index++)
            {
                ActionLabels[index].text = labels[index];
                Actions[index].interactable = available[index];
                Actions[index].gameObject.SetActive(available[index]);
                if (available[index])
                {
                    RectTransform rect = (RectTransform)Actions[index].transform;
                    float width = ((RectTransform)rect.parent).rect.width;
                    rect.anchoredPosition = new Vector2(width - count * 112 + position++ * 112 + 52, -25);
                }
            }
        }

        public void ShowResult(RoundSettlementResult result, int seat)
        {
            Modal.SetActive(true); Clear(ModalHand);
            ModalEyebrow.text = "ROUND RESULT  /  本局结算";
            WinnerSettlement winner = result.Winners.FirstOrDefault(item => item.Seat == seat) ?? result.Winners.FirstOrDefault();
            if (winner != null)
            {
                ModalTitle.text = winner.Seat == seat ? result.Outcome == RoundOutcomeKind.Tsumo ? "自 摸" : "荣 和" : "本局结束";
                ModalScore.text = $"{result.Deltas[winner.Seat]:+0;-0;0} 点";
                string score = winner.Score.Yakuman > 0 ? $"{winner.Score.Yakuman} 倍役满" : $"{winner.Score.Han} 番  ·  {winner.Score.Fu} 符";
                ModalDetail.text = score + "\n\n" + string.Join("  ·  ", winner.Score.Yaku.Select(YakuLabel)) + "\n\n" +
                    string.Join("      ", Enumerable.Range(0, 4).Select(player => $"{new[] { "東", "南", "西", "北" }[player]} {result.Deltas[player]:+0;-0;0}"));
                RevealedHand hand = result.Revealed.FirstOrDefault(item => item.Seat == winner.Seat);
                if (hand != null)
                {
                    List<Tile> tiles = hand.Concealed.Concat(new[] { winner.WinningTile }).Where(tile => tile != null).ToList();
                    float step = 35;
                    for (int index = 0; index < tiles.Count; index++) { AddTile(ModalHand, tiles[index], (ModalHand.rect.width - tiles.Count * step) / 2 + index * step + 16, 27, 32, 42.7f); }
                }
            }
            else
            {
                ModalTitle.text = "流 局"; ModalScore.text = "本局结束";
                ModalDetail.text = "听牌与点数已按当前规则结算\n\n" + string.Join("      ", result.Deltas.Select(value => value.ToString("+0;-0;0")));
            }
            ApplyFonts();
        }

        public void ShowRoom(VisibleMatchState visible)
        {
            Modal.SetActive(true); Clear(ModalHand);
            ModalEyebrow.text = "FRIENDS TABLE  /  友人桌"; ModalTitle.text = "片刻闲暇，一起立直"; ModalScore.text = "四人东南战";
            ModalDetail.text = "25,000 点起始  ·  食断  ·  赤宝牌\n\n" + string.Join("\n", visible.State.PlayerList.Select((player, index) =>
                $"{Winds[index]}    {player.PlayerProfile.Name}       {visible.State.PlayerPoint[index]:N0} 点")) + "\n\n当前为可交互的本地界面预览";
            ApplyFonts();
        }

        private static string YakuLabel(ScoredYaku yaku)
        {
            string label = yaku.Yaku switch
            {
                YakuType.Tsumo => "门前清自摸和", YakuType.Pinfu => "平和", YakuType.Riichi => "立直", YakuType.DoubleRiichi => "两立直",
                YakuType.Ippatsu => "一发", YakuType.Dora => "宝牌", YakuType.AkaDora => "赤宝牌", YakuType.UraDora => "里宝牌",
                YakuType.Tanyao => "断幺九", YakuType.Chiitoitsu => "七对子", YakuType.Iipeikou => "一杯口", YakuType.HoteiRaoyui => "河底捞鱼",
                _ => yaku.Yaku.ToString()
            };
            return yaku.Han > 0 ? $"{label} {yaku.Han}" : label;
        }

        private MahjongTileWidget AddTile(RectTransform parent, Tile value, float x, float y, float width, float height, bool hidden = false, bool dora = false)
        {
            MahjongTileWidget tile = Instantiate(TilePrefab, parent);
            tile.name = hidden ? "Hidden tile" : value?.ToString() ?? "Tile";
            RectTransform rect = (RectTransform)tile.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = Vector2.one / 2;
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one;
            tile.Bind(Art, value, hidden, dora);
            return tile;
        }

        private static void Clear(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                if (Application.isPlaying) { child.SetActive(false); Destroy(child); }
                else { DestroyImmediate(child); }
            }
        }
    }
}
