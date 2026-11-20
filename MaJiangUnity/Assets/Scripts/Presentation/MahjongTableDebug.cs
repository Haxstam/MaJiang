using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaJiangLib;
using UnityEngine;

namespace MaJiang.Presentation
{
    /// <summary>一张牌桌轮流控制四个真实客户端。调试面板是运行时工具，不进入正式对局输入。</summary>
    public sealed class MahjongTableDebug : MonoBehaviour
    {
        public MahjongTableView View;
        public bool FollowPendingSeat = true;
        public bool PauseClock = true;
        private LocalDebugMatch match;
        private Task stopping = Task.CompletedTask;
        private int seat;
        private int selected = -1;
        private bool riichiSelection;
        private VisibleMatchState rendered;
        private int renderedSeat = -1;
        private bool renderedPending;
        private Guid shownResult;
        private readonly int[] settlementPages = new int[4];
        private int sentConfirmations;
        private Guid renderedRound;
        private readonly int[] riichiDiscards = { -1, -1, -1, -1 };
        private bool panelOpen = true;
        private MatchCommand[] variants;
        private string message;
        private double clockFraction;
        private MahjongDebugPanel debugPanel;
        private int startedFrame;
        private IMatchDecisionInput InputSeat => match?.GetInput(seat);

        private void Start()
        {
            startedFrame = Time.frameCount;
            if (View == null) { View = GetComponent<MahjongTableView>(); }
            View.FixedTableSeat = 0;
            View.TileClicked += OnTile;
            if (View.ChoicePanel != null) { View.ChoicePanel.Cancelled = () => variants = null; }
            for (int index = 0; index < View.Actions.Length; index++)
            {
                int action = index;
                View.Actions[index].onClick.AddListener(() => OnAction(action));
            }
            if (View.ResetButton != null) { View.ResetButton.onClick.AddListener(Restart); }
            View.RoomButton.onClick.AddListener(() => panelOpen = !panelOpen);
            if (View.ResetButton != null) { View.ResetButton.GetComponentInChildren<TMP_Text>().text = "重开整场"; }
            View.RoomButton.GetComponentInChildren<TMP_Text>().text = "调试";
            if (View.SettlementView != null) { View.SettlementView.ConfirmButton.onClick.AddListener(ConfirmSettlement); }
            else if (View.Modal != null)
            {
                if (View.ModalClose != null) { View.ModalClose.onClick.AddListener(() => View.Modal.SetActive(false)); }
                if (View.ModalReset != null) { View.ModalReset.onClick.AddListener(() => { match.NextRound(); View.Modal.SetActive(false); }); }
            }
            debugPanel = gameObject.AddComponent<MahjongDebugPanel>();
            debugPanel.HideCollapsedButton = View.SquareTable; if (View.SquareTable) { panelOpen = false; }
            debugPanel.Initialize(View.Art, SelectSeat, () => FollowPendingSeat = !FollowPendingSeat,
                () => PauseClock = !PauseClock, amount => match?.AdvanceTime(amount), () => match?.NextRound(), Restart,
                () => panelOpen = !panelOpen);
            foreach (TMP_Text text in View.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "底部模式说明") { text.text = "本机四客户端调试 · F1 控制面板"; }
            }
            Restart();
        }

        private async void Restart()
        {
            if (!stopping.IsCompleted) { return; }
            if (match != null)
            {
                stopping = match.StopAsync();
                await stopping;
                if (this == null) { return; }
            }
            match = new LocalDebugMatch(); rendered = null; renderedSeat = -1; shownResult = renderedRound = Guid.Empty;
            selected = -1; riichiSelection = false; variants = null; clockFraction = 0;
            Array.Fill(riichiDiscards, -1);
            HideResult(); View.HideChoices();
            if (View.TenpaiHint != null) { View.TenpaiHint.Hide(); }
            View.SetActions(new string[6], new bool[6]);
            try { match.Start(); message = "正在建立本机房间…"; }
            catch (Exception exception) { message = exception.Message; Debug.LogException(exception); }
        }

        private void Update()
        {
            if (match == null || !stopping.IsCompleted) { return; }
            if (!PauseClock)
            {
                clockFraction += Time.unscaledDeltaTime * 1000;
                long elapsed = (long)clockFraction; clockFraction -= elapsed;
                match.AdvanceTime(Math.Min(elapsed, 60000));
            }
            match.Pump();
            // 从预览按F1进入时，不让创建本组件的同一次按键立即把面板收起。
            if (Time.frameCount > startedFrame && UnityEngine.Input.GetKeyDown(KeyCode.F1)) { panelOpen = !panelOpen; }
            for (int index = 0; index < 4; index++)
            {
                if (UnityEngine.Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + index))) { SelectSeat(index); }
            }
            if (!match.Ready)
            {
                if (View.Status != null) { View.Status.text = match.Status; } if (View.Hint != null) { View.Hint.text = message; }
                View.SetActions(new string[6], new bool[6]); return;
            }
            if (FollowPendingSeat && InputSeat.Choices.Count == 0 && !InputSeat.Pending)
            {
                for (int offset = 1; offset < 4; offset++)
                {
                    int next = (seat + offset) % 4;
                    if (match.GetInput(next).Choices.Count > 0) { SelectSeat(next); break; }
                }
            }
            VisibleMatchState current = InputSeat.Current;
            bool pending = InputSeat.Pending;
            if (!ReferenceEquals(current, rendered) || renderedSeat != seat || pending != renderedPending) { Render(); }
            if (View.Timer != null) { View.Timer.text = PauseClock ? "停" : Math.Ceiling(Math.Max(0, current.DeadlineMilliseconds - match.NowMilliseconds) / 1000.0).ToString("00"); }
            if (View.HeaderState != null) { View.HeaderState.text = $"四座调试  /  正在代选 {seat + 1} 号  /  F1 面板"; }
            if (View.Status != null) { View.Status.text = pending ? "正在等待服务端确认" : InputSeat.Choices.Count > 0 ? $"请为 {seat + 1} 号选择" : "该座位当前无需操作"; }
            RenderSettlement(current);
        }

        private void RenderSettlement(VisibleMatchState current)
        {
            RoundSettlementResult result = current.Settlement;
            if (current.OperationStage != MatchOperationStage.RoundEnded || result?.RoundId != current.RoundId)
            {
                if (shownResult != Guid.Empty) { HideResult(); }
                return;
            }
            bool newResult = result.RoundId != shownResult;
            if (newResult)
            {
                shownResult = result.RoundId;
                Array.Clear(settlementPages, 0, settlementPages.Length);
                sentConfirmations = 0;
            }
            if (View.SettlementView != null)
            {
                View.SettlementView.Show(View, current, settlementPages[seat], (sentConfirmations & (1 << seat)) != 0);
            }
            else if (newResult && View.Modal != null)
            {
                View.ShowResult(result, seat);
                if (View.ModalReset != null)
                {
                    View.ModalReset.GetComponentInChildren<TMP_Text>().text = result.MatchEnded ? "整场结束" : "下一局";
                    View.ModalReset.interactable = !result.MatchEnded;
                }
            }
        }

        private void ConfirmSettlement()
        {
            VisibleMatchState current = InputSeat?.Current;
            if (current?.OperationStage != MatchOperationStage.RoundEnded || current.Settlement?.RoundId != shownResult) { return; }
            if (settlementPages[seat] < current.Settlement.Winners.Count - 1) { settlementPages[seat]++; }
            else if (match.ConfirmSettlement(seat, shownResult)) { sentConfirmations |= 1 << seat; }
            RenderSettlement(current);
        }

        private void HideResult()
        {
            shownResult = Guid.Empty;
            if (View.SettlementView != null) { View.SettlementView.Hide(); }
            if (View.Modal != null) { View.Modal.SetActive(false); }
        }

        private void SelectSeat(int next)
        {
            if (seat == next) { return; }
            View.HideChoices(); View.ClearHover(); seat = next; selected = -1; riichiSelection = false; variants = null; renderedSeat = -1;
        }

        private void Render()
        {
            IMatchDecisionInput input = InputSeat;
            if (rendered != null && rendered.PhaseId != input.Current.PhaseId)
            {
                selected = -1; riichiSelection = false;
            }
            rendered = input.Current; renderedSeat = seat; renderedPending = input.Pending;
            if (renderedRound != rendered.RoundId)
            {
                renderedRound = rendered.RoundId; Array.Fill(riichiDiscards, -1);
                selected = -1; riichiSelection = false; variants = null;
            }
            for (int index = 0; index < 4; index++)
            {
                if (rendered.State.IsRiichi[index] && riichiDiscards[index] < 0) { riichiDiscards[index] = rendered.State.DiscardTileList[index].Count - 1; }
            }
            if (variants != null && variants.Any(command => !input.Choices.Contains(command))) { variants = null; View.HideChoices(); }
            PlayerActionData[] actions = input.Choices.Select(command => command.Action).ToArray();
            bool Has(params PlayerAction[] types) => actions.Any(action => types.Contains(action.PlayerAction));
            if (riichiSelection && !Has(PlayerAction.Riichi)) { selected = -1; riichiSelection = false; }
            View.Render(rendered, riichiDiscards, selected,
                riichiSelection ? "请选择立直宣言牌，再次点击确认" : "数字键 1–4 切换座位 · 选中手牌后再次点击出牌 · F1 调试面板",
                actions, riichiSelection);
            View.SetActions(new[] { "立 直", "吃", "碰", "杠", Has(PlayerAction.Ron) ? "荣 和" : "自 摸", "跳 过" },
                new[] { Has(PlayerAction.Riichi), Has(PlayerAction.Chi), Has(PlayerAction.Peng), Has(PlayerAction.ClosedKan, PlayerAction.OpenKan, PlayerAction.AddedKan),
                    Has(PlayerAction.Ron, PlayerAction.Tsumo), Has(PlayerAction.Skip) });
            View.SetAbortiveDraw(Has(PlayerAction.Draw));
        }

        private void OnTile(MahjongTileWidget widget)
        {
            IMatchDecisionInput input = InputSeat;
            if (input == null) { return; }
            IReadOnlyList<MatchCommand> choices = input.Choices;
            MatchCommand command = choices.FirstOrDefault(choice => widget.MatchesDiscard(choice.Action, riichiSelection));
            if (command == null) { return; }
            if (selected == widget.HandIndex) { Submit(command); return; }
            selected = widget.HandIndex;
            View.UpdateDiscardSelection(input.Current, selected, choices.Select(choice => choice.Action).ToArray(), riichiSelection);
        }

        private void OnAction(int index)
        {
            if (InputSeat == null) { return; }
            View.HideChoices();
            if (index == 0)
            {
                riichiSelection = !riichiSelection; selected = -1; Render(); return;
            }
            PlayerAction[] types = index switch
            {
                6 => new[] { PlayerAction.Draw },
                1 => new[] { PlayerAction.Chi }, 2 => new[] { PlayerAction.Peng },
                3 => new[] { PlayerAction.OpenKan, PlayerAction.ClosedKan, PlayerAction.AddedKan },
                4 => new[] { PlayerAction.Ron, PlayerAction.Tsumo },
                _ => new[] { PlayerAction.Skip }
            };
            MatchCommand[] choices = InputSeat.Choices.Where(command => types.Contains(command.Action.PlayerAction)).ToArray();
            if (choices.Length == 1) { Submit(choices[0]); }
            else if (choices.Length > 1)
            {
                variants = choices;
                if (View.ChoicePanel != null) { View.ShowChoices(choices.Select(command => command.Action).ToArray(), selectedAction => Submit(choices.First(command => ReferenceEquals(command.Action, selectedAction)))); }
                else { panelOpen = true; }
            }
        }

        private void Submit(MatchCommand command)
        {
            View.ClearHover();
            View.HideChoices();
            if (!InputSeat.Submit(command)) { message = "操作未发送，请等待同步后重新选择"; return; }
            message = $"已发送 {seat + 1} 号：{Describe(command)}";
            selected = -1; riichiSelection = false; variants = null;
            Render();
        }

        private void LateUpdate()
        {
            if (debugPanel == null || match == null) { return; }
            string[] labels = Enumerable.Range(0,4).Select(index =>
            {
                IMatchDecisionInput input = match.GetInput(index);
                string state = input == null ? "连接中" : input.Pending ? "待确认" : input.Choices.Count > 0 ? "待操作" : input.Current?.HasResponded == true ? "已答复" : "等待";
                return $"{index + 1}号{(index == seat ? " ◀" : "")}\n{state}";
            }).ToArray();
            VisibleMatchState view = InputSeat?.Current;
            string information = view == null ? "正在同步牌局" : $"视角 {seat+1}号（{new[] { "东", "南", "西", "北" }[MahjongSeatOrder.WindIndex(seat, view.State.CurrentDealerIndex)]}家） · {view.OperationStage}\n局代数 {view.RoundGeneration} · 阶段 {view.PhaseId} · 日志 {view.Sequence}";
            if (view != null)
            {
                HandTile hand = view.State.PlayerList[view.SelfSeat].PlayerHandTile;
                information += $"\n暗手 {hand.HandTileList.Count}张 · 摸牌 {hand.SingleTile?.ToString() ?? "无"}";
            }
            information += "\n" + message;
            string error = match.SeatError(seat);
            if (!string.IsNullOrEmpty(error)) { information += "\n客户端提示：" + error; }
            MatchCommand[] choices = (variants ?? InputSeat?.Choices.ToArray() ?? Array.Empty<MatchCommand>())
                .Where(command => command.Action.PlayerAction != PlayerAction.TakeCut).ToArray();
            debugPanel.Refresh(panelOpen, match.Status, labels, information, FollowPendingSeat, PauseClock, choices, Describe, Submit);
        }

        private static string Describe(MatchCommand command)
        {
            PlayerActionData action = command.Action;
            string name = action.PlayerAction switch
            {
                PlayerAction.HandCut => "手切", PlayerAction.TakeCut => "摸切", PlayerAction.Riichi => "立直",
                PlayerAction.Chi => "吃", PlayerAction.Peng => "碰", PlayerAction.OpenKan => "大明杠", PlayerAction.ClosedKan => "暗杠",
                PlayerAction.AddedKan => "加杠", PlayerAction.Ron => "荣和", PlayerAction.Tsumo => "自摸", PlayerAction.Skip => "跳过",
                PlayerAction.Draw => "九种九牌", _ => action.PlayerAction.ToString()
            };
            string used = action.Tiles?.Count > 0 ? " · 使用 " + string.Join(" ", action.Tiles) : "";
            return $"{name}  {action.TargetTile}{used}";
        }

        private void OnDestroy()
        {
            if (View != null) { View.TileClicked -= OnTile; }
            if (View != null && View.SettlementView != null) { View.SettlementView.ConfirmButton.onClick.RemoveListener(ConfirmSettlement); }
            if (match != null) { stopping = match.StopAsync(); }
            if (debugPanel != null) { Destroy(debugPanel); }
        }
        private void OnDisable() { if (debugPanel != null) { debugPanel.enabled = false; } }
        private void OnEnable() { if (debugPanel != null) { debugPanel.enabled = true; } }
    }
}
