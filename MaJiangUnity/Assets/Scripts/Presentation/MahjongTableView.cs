using TMPro;
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
            public TMP_Text Name;
            public TMP_Text Points;
            public TMP_Text Wind;
            public GameObject Riichi;
            public TableShape TurnBorder;
        }

        [Serializable]
        public sealed class DiscardFrameSettings
        {
            public bool Enabled = true;
            public Color FrameColor = new(1f, 0.72f, 0.23f, 0.60f);
            [Min(0)] public float FramePadding = 0.8f;
            public Color ResponseColor = new(1f, 0.86f, 0.42f, 1f);
            [Min(0)] public float ResponsePadding = 2f;
            [Min(0.05f)] public float ResponsePeriod = 1.1f;
            [Min(0)] public float ResponseScaleMin = 0.9f;
            [Min(0)] public float ResponseScaleMax = 1.1f;
            [Range(0, 1)] public float ResponseOpacityMin = 0.25f;
            [Range(0, 1)] public float ResponseOpacityMax = 0.9f;
        }
        public MahjongTileArt Art;
        public MahjongTileWidget TilePrefab;
        public RectTransform LeftClaimGroup;
        public RectTransform TopClaimGroup;
        public RectTransform RightClaimGroup;
        public bool SquareTable;
        public MahjongHandStripLayout[] HandStrips = new MahjongHandStripLayout[4];
        public MahjongChoicePanel ChoicePanel;
        public MahjongTenpaiHint TenpaiHint;
        public TMP_Text Deposits;
        public TMP_Text DebugHandLabel;
        public Graphic[] TurnLights = new Graphic[4];
        public MahjongDoraSweepSettings DoraSweep = new();
        public DiscardFrameSettings DiscardFrame = new();
        [NonSerialized] public int FixedTableSeat = -1;
        public RectTransform[] Hands = new RectTransform[4];
        public RectTransform[] Rivers = new RectTransform[4];
        public RectTransform[] Melds = new RectTransform[4];
        public PlayerPanel[] Players = new PlayerPanel[4];
        public RectTransform Indicators;
        public TMP_Text Round;
        public TMP_Text Counters;
        public TMP_Text Remaining;
        public TMP_Text Timer;
        public TMP_Text Hint;
        public TMP_Text Status;
        public TMP_Text HeaderState;
        public Button[] Actions;
        public TMP_Text[] ActionLabels;
        public Button RoomButton;
        public Button ResetButton;
        public MahjongSettlementView SettlementView;
        public GameObject Modal;
        public TMP_Text ModalEyebrow;
        public TMP_Text ModalTitle;
        public TMP_Text ModalScore;
        public TMP_Text ModalDetail;
        public RectTransform ModalHand;
        public Button ModalClose;
        public Button ModalReset;
        [Min(1)] public float SelfTileWidth = 62;
        [Min(1)] public float OtherTileWidth = 30;
        [Min(0)] public float TileGap = 4;
        [Min(0)] public float DrawGap = 21;
        [Min(1)] public float RiverTileWidth = 40;
        [HideInInspector] public int RiverColumns = 6;
        private readonly List<MahjongTileWidget> handWidgets = new();
        private int displayedDealer;
        private int lightRelative = -1;
        private double lightStarted;
        private readonly Color[] lightColors = new Color[4];
        private bool lightsCaptured;
        private MahjongTileWidget hoveredTile;
        private Image currentDiscardFrame;
        private Image responseFrame;
        private Guid framedRound;
        private long framedPhase;
        private double frameStarted;
        public event Action<MahjongTileWidget> TileClicked;
        private static readonly string[] Winds = { "東", "南", "西", "北" };

        private void Update()
        {
            if (!Application.isPlaying) { return; }
            float pulse = (1 + Mathf.Cos((float)(Time.unscaledTimeAsDouble - lightStarted) * Mathf.PI)) / 2;
            for (int i = 0; i < TurnLights.Length; i++)
            {
                if (TurnLights[i] == null) { continue; }
                Color color = lightColors[i]; color.a *= i == lightRelative ? 0.25f + 0.75f * pulse : 0;
                TurnLights[i].color = color;
            }
            UpdateDiscardFrames();
        }
        public void ClearHover()
        {
            hoveredTile = null;
            foreach (var tile in GetComponentsInChildren<MahjongTileWidget>(true)) { tile.Highlight(false); }
        }
        private void Hover(MahjongTileWidget source, bool entered)
        {
            if (!entered && hoveredTile != source) { return; }
            ClearHover();
            if (!entered) { return; }
            hoveredTile = source;
            foreach (var tile in GetComponentsInChildren<MahjongTileWidget>())
            {
                // 正向限定目标区域：任何手牌、摸牌、宝牌、选项牌、结算牌都不参与。
                bool river = Rivers.Any(rect => rect != null && tile.transform.IsChildOf(rect));
                bool meld = Melds.Any(rect => rect != null && tile.transform.IsChildOf(rect));
                tile.Highlight(MahjongTableEffects.Highlight(source.Tile, tile.Tile, tile.FaceVisible, river, meld));
            }
        }
        private void OnDisable() { ClearHover(); }

        private void OnEnable()
        {
            if (!lightsCaptured)
            {
                for (int i = 0; i < TurnLights.Length; i++) { if (TurnLights[i] != null) { lightColors[i] = TurnLights[i].color; TurnLights[i].raycastTarget = false; } }
                lightsCaptured = true;
            }
            if (SquareTable)
            {
                for (int i = 0; i < HandStrips.Length; i++)
                {
                    if (HandStrips[i] != null) { Hands[i] = HandStrips[i].Concealed; Melds[i] = HandStrips[i].Melds; }
                }
            }
            EnsureLayouts(); ApplyFonts();
        }

        public void EnsureLayouts()
        {
            foreach (RectTransform rect in Hands)
            {
                if (rect == null) { continue; }
                HorizontalLayoutGroup layout = MahjongTableLayouts.Horizontal(rect, TileGap, SquareTable ? TextAnchor.UpperLeft : TextAnchor.MiddleCenter);
                if (SquareTable) { layout.childControlWidth = layout.childControlHeight = false; layout.childForceExpandWidth = layout.childForceExpandHeight = false; }
            }
            foreach (RectTransform rect in Melds)
            {
                if (rect == null) { continue; }
                var layout = MahjongTableLayouts.Horizontal(rect, 0, SquareTable ? TextAnchor.LowerRight : TextAnchor.MiddleRight); layout.spacing = 0;
                if (SquareTable) { layout.childAlignment = TextAnchor.LowerRight; layout.childControlWidth = layout.childControlHeight = false; layout.childForceExpandWidth = layout.childForceExpandHeight = false; layout.reverseArrangement = true; }
            }
            foreach (RectTransform rect in Rivers) { if (rect != null) { MahjongTableLayouts.RiverGrid(rect, RiverTileWidth); } }
            if (Indicators != null)
            {
                var layout = MahjongTableLayouts.Horizontal(Indicators, TileGap, SquareTable ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                layout.childAlignment = SquareTable ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                if (SquareTable) { layout.spacing = TileGap; layout.childForceExpandWidth = layout.childForceExpandHeight = false; }
            }
            if (ModalHand != null) { MahjongTableLayouts.Horizontal(ModalHand, 3, TextAnchor.MiddleCenter); }
            if (Actions != null && Actions.Length > 0 && Actions[0] != null)
            {
                var layout = MahjongTableLayouts.Horizontal((RectTransform)Actions[0].transform.parent, SquareTable ? 10 : 8, TextAnchor.MiddleRight);
                if (SquareTable)
                {
                    layout.childAlignment = TextAnchor.MiddleRight; layout.spacing = 10;
                    layout.childControlWidth = layout.childControlHeight = false;
                    layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                }
                foreach (Button button in Actions)
                {
                    if (button != null && button.GetComponent<LayoutElement>() == null) { MahjongTableLayouts.Size((RectTransform)button.transform, SquareTable ? 80 : 104, SquareTable ? 80 : 46); }
                }
            }
        }
        public void ApplyFonts()
        {
            if (Art == null) { return; }
            TMP_FontAsset font = Art.GetFont();
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (Art.Font != null || text.font == null || text.font == TMP_Settings.defaultFontAsset) { text.font = font; }
            }
        }

        /// <summary>手牌仅从SelfSeat读取，其他座位永远以公开张数显示牌背。</summary>
        public void Render(VisibleMatchState visible, int[] riichiDiscards, int selected, string hint,
            IReadOnlyList<PlayerActionData> actions, bool riichiSelection)
        {
            MainMatchControl state = visible.State;
            MahjongTileWidget previousHover = hoveredTile;
            ClearHover();
            displayedDealer = state.CurrentDealerIndex;
            int tableSeat = FixedTableSeat >= 0 ? FixedTableSeat : visible.SelfSeat;
            int activeLight = MahjongTableEffects.ActiveSeat(visible);
            int nextLight = activeLight < 0 ? -1 : (activeLight - tableSeat + 4) % 4;
            if (nextLight != lightRelative) { lightStarted = Time.unscaledTimeAsDouble; }
            lightRelative = nextLight;
            for (int i = 0; i < TurnLights.Length; i++)
            {
                if (TurnLights[i] != null) { TurnLights[i].gameObject.SetActive(true); }
            }
            if (DebugHandLabel != null)
            {
                int relativeHand = (visible.SelfSeat - tableSeat + 4) % 4;
                DebugHandLabel.text = $"观看{new[] { "自家", "下家", "对家", "上家" }[relativeHand]}手牌\n{new[] { "东", "南", "西", "北" }[MahjongSeatOrder.WindIndex(visible.SelfSeat, displayedDealer)]}家 · {state.PlayerList[visible.SelfSeat].PlayerProfile.Name}";
                DebugHandLabel.raycastTarget = false;
            }
            EnsureLayouts();
            ApplyFonts();
            Round.text = SquareTable ? $"{new[] { "东", "南", "西", "北" }[(int)state.Wind - 1]}{new[] { "一", "二", "三", "四" }[state.Round - 1]}局"
                : $"{Winds[(int)state.Wind - 1]} {new[] { "一", "二", "三", "四" }[state.Round - 1]} 局";
            Counters.text = SquareTable ? $"{state.Honba}本场" : $"{state.Honba} 本场     ·     {state.RiichiStickCount} 供托";
            Remaining.text = SquareTable ? $"余 {state.RemainTileCount}" : state.RemainTileCount.ToString("00");
            if (Deposits != null) { Deposits.text = $"供托+{state.RiichiStickCount * 1000}"; }
            if (Timer != null) { Timer.text = visible.OperationStage == MatchOperationStage.RoundEnded ? "—" : "25"; }
            if (Hint != null) { Hint.text = hint; }
            if (HeaderState != null) { HeaderState.text = $"四人东南战  /  {visible.RoundGeneration:00}"; }
            if (Status != null) { Status.text = visible.OperationStage == MatchOperationStage.RoundEnded ? "本局结束"
                : state.CurrentPlayerIndex == visible.SelfSeat ? "你的回合" : "等待响应";
            }
            currentDiscardFrame = null;
            responseFrame = null;
            bool canRespond = MahjongTableEffects.CanRespondToDiscard(visible, actions);
            if (visible.OperationStage == MatchOperationStage.DiscardResponse && (framedRound != visible.RoundId || framedPhase != visible.PhaseId))
            {
                framedRound = visible.RoundId;
                framedPhase = visible.PhaseId;
                frameStarted = Time.unscaledTimeAsDouble;
            }
            for (int relative = 0; relative < 4; relative++)
            {
                int seat = MahjongSeatOrder.PublicSeat(visible.SelfSeat, relative, FixedTableSeat);
                PlayerPanel player = Players[relative];
                player.Name.text = state.PlayerList[seat].PlayerProfile.Name;
                player.Points.text = state.PlayerPoint[seat].ToString(SquareTable ? "0" : "N0");
                player.Wind.text = Winds[MahjongSeatOrder.WindIndex(seat, state.CurrentDealerIndex)];
                player.Riichi.SetActive(state.IsRiichi[seat]);
                if (player.TurnBorder != null) { player.TurnBorder.gameObject.SetActive(state.CurrentPlayerIndex == seat && visible.OperationStage != MatchOperationStage.RoundEnded); }
                DrawHand(visible, relative == 0 ? visible.SelfSeat : seat, relative, selected);
                DrawRiver(Rivers[relative], state.DiscardTileList[seat], riichiDiscards[seat], state.DoraPointerList,
                    MahjongTableEffects.ActiveDiscardIndex(visible, seat), canRespond);
                IReadOnlyList<Group> melds = relative == 0 && seat != visible.SelfSeat
                    ? Array.Empty<Group>()
                    : state.PlayerOpenSetList[seat];
                DrawMelds(Melds[relative], melds, relative == 0 ? SelfTileWidth : OtherTileWidth, seat, state.DoraPointerList);
                if (SquareTable)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)HandStrips[relative].transform);
                    HandStrips[relative].Drawn.GetComponent<MahjongTileWidget>().CaptureRest();
                }
            }
            UpdateDiscardFrames();
            Clear(Indicators);
            for (int index = 0; index < 5; index++)
            {
                bool opened = index < state.DoraPointerList.Count;
                float width = SquareTable ? Indicators.rect.height * 0.75f : 38;
                MahjongTileWidget tile = CreateTile(Indicators, opened ? state.DoraPointerList[index] : null, width, !opened, false, false);
                MahjongTableLayouts.Size((RectTransform)tile.transform, width, width * 4 / 3);
            }
            UpdateDiscardSelection(visible, selected, actions, riichiSelection);
            if (previousHover != null && previousHover.gameObject.activeInHierarchy && previousHover.HoverSource) { Hover(previousHover, true); }
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
        }

        private void DrawHand(VisibleMatchState visible, int seat, int relative, int selected)
        {
            RectTransform parent = Hands[relative]; Clear(parent);
            if (relative == 0) { handWidgets.Clear(); }
            HandTile hand = visible.State.PlayerList[seat].PlayerHandTile;
            bool self = relative == 0;
            int concealed = self ? hand.HandTileList.Count : visible.HandCounts[seat];
            bool showDraw = self ? hand.SingleTile != null : stateDraw(visible, seat);
            if (SquareTable && !self && showDraw) { concealed--; }
            int[] order = self ? MahjongHandOrder.SortedIndices(hand.HandTileList) : Enumerable.Range(0, concealed).ToArray();
            float width = self ? SelfTileWidth : OtherTileWidth;
            int count = concealed + (!SquareTable && self && hand.SingleTile != null ? 1 : 0);
            for (int display = 0; display < count; display++)
            {
                bool drawn = self && display == concealed;
                int index = drawn ? concealed : order[display];
                Tile value = !self ? null : drawn ? hand.SingleTile : hand.HandTileList[index];
                if (drawn) { MahjongTableLayouts.Slot(parent, "摸牌间隔", DrawGap, 1); }
                bool dora = MahjongTableEffects.IsVisibleDora(value, self, visible.State.DoraPointerList);
                MahjongTileWidget tile = AddTile(parent, value, width, !self, dora);
                if (dora) { tile.PlayDoraSweep(DoraSweep); }
                tile.HandIndex = index; tile.Drawn = drawn; tile.Interactive = self && visible.OperationStage != MatchOperationStage.RoundEnded;
                if (self) { tile.Clicked += item => TileClicked?.Invoke(item); tile.HoverSource = true; tile.Hovered += Hover; handWidgets.Add(tile); }
            }
            if (SquareTable)
            {
                RectTransform draw = HandStrips[relative].Drawn;
                HandStrips[relative].TileWidth = width;
                // 独立摸牌是第14张（有副露时相应减少），不放进自动排序的暗手列表。
                // 摸牌的尺寸统一交给手牌布局；不依赖Prefab保存的0高度或另一组件的重建时序。
                AspectRatioFitter aspect = draw.GetComponent<AspectRatioFitter>();
                if (aspect != null) { aspect.enabled = false; }
                draw.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                draw.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, width * 4 / 3);
                draw.gameObject.SetActive(showDraw);
                if (showDraw)
                {
                    MahjongTileWidget tile = draw.GetComponent<MahjongTileWidget>();
                    tile.Bind(Art, self ? hand.SingleTile : null, !self); tile.Drawn = true; tile.HandIndex = concealed;
                    ApplyDoraSweep(tile, visible.State.DoraPointerList);
                    tile.Interactive = self && visible.OperationStage != MatchOperationStage.RoundEnded;
                    tile.HoverSource = self;
                    if (self) { tile.Clicked -= ForwardDrawClick; tile.Clicked += ForwardDrawClick; tile.Hovered -= Hover; tile.Hovered += Hover; handWidgets.Add(tile); }
                }
                LayoutRebuilder.MarkLayoutForRebuild((RectTransform)HandStrips[relative].transform);
            }
        }

        private static bool stateDraw(VisibleMatchState view, int seat) => view.State.CurrentPlayerIndex == seat && view.OperationStage == MatchOperationStage.Discard && view.HandCounts[seat] % 3 == 2;
        private void ForwardDrawClick(MahjongTileWidget tile) => TileClicked?.Invoke(tile);
        public void HideChoices() { if (ChoicePanel != null) { ChoicePanel.Hide(); } }
        public void ShowChoices(PlayerActionData[] choices, Action<PlayerActionData> selected) { if (ChoicePanel != null) { ChoicePanel.Show(choices, selected); } }
        public void SetAbortiveDraw(bool available)
        {
            if (Actions.Length > 6) { Actions[6].gameObject.SetActive(available); Actions[6].interactable = available; }
        }

        private void DrawRiver(RectTransform parent, IReadOnlyList<Tile> river, int riichiAt,
            IReadOnlyList<Tile> indicators, int activeDiscard, bool canRespond)
        {
            Clear(parent);
            for (int index = 0; index < river.Count; index++)
            {
                MahjongTileWidget tile = CreateTile(parent, river[index], RiverTileWidth, false, false, index == riichiAt);
                ApplyDoraSweep(tile, indicators);
                if (index == riichiAt)
                {
                    tile.MarkRiichi();
                }
                if (index == activeDiscard && Art.DiscardFrame != null)
                {
                    currentDiscardFrame = tile.AddDiscardFrame(Art.DiscardFrame, "当前舍牌金框");
                    if (canRespond) { responseFrame = tile.AddDiscardFrame(Art.DiscardFrame, "可响应舍牌金框"); }
                }
            }
        }

        private void UpdateDiscardFrames()
        {
            if (currentDiscardFrame == null) { return; }
            SetFrame(currentDiscardFrame, DiscardFrame.FrameColor, DiscardFrame.FramePadding, 1f);
            if (responseFrame == null) { return; }
            float period = Mathf.Max(0.05f, DiscardFrame.ResponsePeriod);
            float progress = (float)(((Time.unscaledTimeAsDouble - frameStarted) % period) / period);
            float wave = (1 - Mathf.Cos(progress * 2 * Mathf.PI)) * 0.5f;
            Color tint = DiscardFrame.ResponseColor;
            tint.a *= Mathf.Lerp(DiscardFrame.ResponseOpacityMin, DiscardFrame.ResponseOpacityMax, wave);
            float scale = Mathf.Lerp(DiscardFrame.ResponseScaleMin, DiscardFrame.ResponseScaleMax, wave);
            SetFrame(responseFrame, tint, DiscardFrame.ResponsePadding, scale);
        }

        private void SetFrame(Image frame, Color color, float pad, float scale)
        {
            frame.enabled = DiscardFrame.Enabled;
            frame.color = color;
            RectTransform rect = frame.rectTransform;
            rect.sizeDelta = Vector2.one * (2 * pad);
            rect.localScale = new Vector3(scale, scale, 1);
        }

        private void ApplyDoraSweep(MahjongTileWidget widget, IReadOnlyList<Tile> indicators)
        {
            if (indicators != null && MahjongTableEffects.IsVisibleDora(widget.Tile, widget.FaceVisible, indicators))
            {
                widget.PlayDoraSweep(DoraSweep);
            }
        }

        internal void DrawMelds(RectTransform parent, IReadOnlyList<Group> groups, float width, int owner, IReadOnlyList<Tile> indicators = null)
        {
            Clear(parent);
            foreach (Group group in groups)
            {
                MahjongMeldPresentation data = MahjongMeldPresentation.From(group, owner);
                RectTransform prefab = data.Direction switch { ClaimDirection.Left => LeftClaimGroup, ClaimDirection.Right => RightClaimGroup, _ => TopClaimGroup };
                if (prefab == null) { throw new InvalidOperationException("未绑定鸣牌组Prefab。"); }
                float scale = width / 30f;
                RectTransform slot = MahjongTableLayouts.Slot(parent, "副露面子", prefab.rect.width * scale, prefab.rect.height * scale);
                RectTransform meld = Instantiate(prefab, slot); meld.name = "鸣牌组";
                meld.anchorMin = meld.anchorMax = meld.pivot = Vector2.one / 2;
                meld.anchoredPosition = Vector2.zero; meld.localScale = Vector3.one * scale;
                BindGroupCard(meld, "FirstCard", data.First, data.ConcealOuter, indicators);
                BindGroupCard(meld, "SecondCard", data.Second, data.ConcealOuter, indicators);
                BindGroupCard(meld, "ClaimedCard", data.Claimed, false, indicators);
                Transform fourth = meld.Find("KanCard"); fourth.gameObject.SetActive(data.ShowKan);
                if (data.ShowKan) { BindGroupCard(meld, "KanCard", data.Kan, false, indicators); }
            }
        }

        private void BindGroupCard(Transform meld, string name, Tile tile, bool hidden, IReadOnlyList<Tile> indicators)
        {
            Transform card = meld.Find(name);
            MahjongTileWidget widget = card.GetComponent<MahjongTileWidget>();
            if (widget == null) { widget = card.gameObject.AddComponent<MahjongTileWidget>(); }
            widget.Interactive = false; widget.Bind(Art, tile, hidden);
            ApplyDoraSweep(widget, indicators);
        }

        public void UpdateDiscardSelection(VisibleMatchState visible, int selected, IReadOnlyList<PlayerActionData> actions, bool riichiSelection)
        {
            MahjongTileWidget selectedTile = null;
            foreach (MahjongTileWidget tile in handWidgets)
            {
                tile.SetDiscardBlocked(riichiSelection && !actions.Any(action => tile.MatchesDiscard(action, true)));
                tile.Select(tile.HandIndex == selected);
                if (tile.HandIndex == selected) { selectedTile = tile; }
            }
            if (TenpaiHint != null)
            {
                PlayerActionData discard = selectedTile == null ? null : actions.FirstOrDefault(action => selectedTile.MatchesDiscard(action, riichiSelection));
                TenpaiHint.Show(Art, visible, discard);
            }
        }

        public void SetActions(string[] labels, bool[] available)
        {
            EnsureLayouts();
            for (int index = 0; index < Actions.Length; index++)
            {
                if (index >= labels.Length) { Actions[index].gameObject.SetActive(false); continue; }
                ActionLabels[index].text = labels[index];
                Actions[index].interactable = available[index];
                Actions[index].gameObject.SetActive(available[index]);
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
                    string.Join("      ", Enumerable.Range(0, 4).Select(player => $"{Winds[MahjongSeatOrder.WindIndex(player, displayedDealer)]} {result.Deltas[player]:+0;-0;0}"));
                RevealedHand hand = result.Revealed.FirstOrDefault(item => item.Seat == winner.Seat);
                if (hand != null)
                {
                    List<Tile> tiles = hand.Concealed.Concat(new[] { winner.WinningTile }).Where(tile => tile != null).ToList();
                    foreach (Tile tile in tiles.OrderBy(tile => tile)) { AddTile(ModalHand, tile, 32); }
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
                $"{Winds[MahjongSeatOrder.WindIndex(index, visible.State.CurrentDealerIndex)]}    {player.PlayerProfile.Name}       {visible.State.PlayerPoint[index]:N0} 点")) + "\n\n当前为可交互的本地界面预览";
            ApplyFonts();
        }

        private static string YakuLabel(ScoredYaku yaku)
        {
            string label = MahjongSettlementView.YakuName(yaku.Yaku);
            return yaku.Han > 0 ? $"{label} {yaku.Han}" : label;
        }

        private MahjongTileWidget AddTile(RectTransform parent, Tile value, float width, bool hidden = false, bool dora = false, bool sideways = false)
        {
            float height = width * 4 / 3;
            RectTransform slot = MahjongTableLayouts.Slot(parent, "牌布局单元", sideways ? height : width, sideways ? width : height);
            return CreateTile(slot, value, width, hidden, dora, sideways);
        }

        private MahjongTileWidget CreateTile(RectTransform parent, Tile value, float width, bool hidden, bool dora, bool sideways)
        {
            float height = width * 4 / 3;
            MahjongTileWidget tile = Instantiate(TilePrefab, parent);
            tile.name = hidden ? "暗牌" : value == null ? "麻将牌" : "明牌_" + TileLabel(value);
            RectTransform rect = (RectTransform)tile.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one / 2;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(width, height); rect.localScale = Vector3.one;
            rect.localEulerAngles = sideways ? new Vector3(0, 0, 90) : Vector3.zero;
            tile.Bind(Art, value, hidden, dora);
            return tile;
        }

        private static string TileLabel(Tile tile)
        {
            if (tile.Color == MaJiangLib.Color.Honor) { return new[] { "东风", "南风", "西风", "北风", "白板", "发财", "红中" }[tile.Number - 1]; }
            return (tile.IsRedDora ? "赤" : "") + "一二三四五六七八九"[tile.Number - 1] + new[] { "万", "筒", "索" }[(int)tile.Color];
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
