using TMPro;
using System;
using System.Collections.Generic;
using System.Linq;
using MaJiangLib;
using UnityEngine;
using UnityEngine.Events;
using MatchType = MaJiangLib.MatchType;

namespace MaJiang.Presentation
{
    /// <summary>独立牌桌预览：合法牌库存、真实操作机和计分，本地对手仅摸切/跳过；不启动网络或改动旧场景。</summary>
    public sealed class MahjongTablePreview : MonoBehaviour
    {
        public MahjongTableView View;
        private MainMatchControl core;
        private MatchJournal journal;
        private AuthoritativeMatchMachine machine;
        private int selected = -1;
        private bool riichiSelection;
        private bool wired;
        private UnityAction[] actionHandlers;
        private readonly int[] riichiDiscards = { -1, -1, 4, -1 };
        private RoundSettlementResult result;

        private void Start()
        {
            Wire();
            InitializePreview();
            View.RoomButton.GetComponentInChildren<TMP_Text>().text = "四座调试";
            foreach (TMP_Text text in View.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "底部模式说明") { text.text = "演示牌局 · F1 或右上角“四座调试”进入调试"; }
            }
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.F1)) { StartDebug(); }
        }

        /// <summary>在当前牌桌直接开始真实四座会话，保留场景里的布局和素材。</summary>
        public void StartDebug()
        {
            if (!enabled) { return; }
            enabled = false;
            MahjongTableDebug debug = GetComponent<MahjongTableDebug>();
            if (debug == null) { debug = gameObject.AddComponent<MahjongTableDebug>(); }
            debug.View = View; debug.enabled = true;
        }

        private void Wire()
        {
            if (wired) { return; }
            wired = true;
            View.TileClicked += OnTile;
            actionHandlers = new UnityAction[View.Actions.Length];
            for (int index = 0; index < View.Actions.Length; index++)
            {
                int action = index;
                actionHandlers[index] = () => OnAction(action);
                View.Actions[index].onClick.AddListener(actionHandlers[index]);
            }
            if (View.ResetButton != null) { View.ResetButton.onClick.AddListener(InitializePreview); }
            View.RoomButton.onClick.AddListener(StartDebug);
            View.ModalClose.onClick.AddListener(CloseModal);
            View.ModalReset.onClick.AddListener(InitializePreview);
        }

        private void CloseModal() => View.Modal.SetActive(false);

        private void OnDisable()
        {
            if (!wired || View == null) { return; }
            wired = false; View.TileClicked -= OnTile;
            for (int index = 0; index < actionHandlers.Length; index++)
            {
                View.Actions[index].onClick.RemoveListener(actionHandlers[index]);
            }
            if (View.ResetButton != null) { View.ResetButton.onClick.RemoveListener(InitializePreview); }
            View.RoomButton.onClick.RemoveListener(StartDebug);
            View.ModalClose.onClick.RemoveListener(CloseModal);
            View.ModalReset.onClick.RemoveListener(InitializePreview);
        }

        public void InitializePreview()
        {
            core = CreatePosition();
            journal = new MatchJournal(core, () => 0);
            machine = new AuthoritativeMatchMachine(journal, () => 0, false);
            selected = 13; riichiSelection = false; result = null;
            riichiDiscards[0] = riichiDiscards[1] = riichiDiscards[3] = -1; riichiDiscards[2] = 4;
            View.Modal.SetActive(false);
            Refresh("选中后再次点击出牌  ·  也可以选择自摸");
        }

        private void OnTile(MahjongTileWidget widget)
        {
            if (machine.Stage == MatchOperationStage.RoundEnded) { return; }
            var actions = machine.GetActions(0);
            PlayerActionData action = actions.FirstOrDefault(choice => widget.MatchesDiscard(choice, riichiSelection));
            if (action == null) { return; }
            if (selected == widget.HandIndex)
            {
                Execute(action);
                return;
            }
            selected = widget.HandIndex;
            View.UpdateDiscardSelection(journal.GetSnapshot(0), selected, actions, riichiSelection);
        }

        private void OnAction(int index)
        {
            var actions = machine.GetActions(0);
            if (index == 0)
            {
                riichiSelection = !riichiSelection;
                selected = -1;
                View.UpdateDiscardSelection(journal.GetSnapshot(0), selected, actions, riichiSelection);
                if (View.Hint != null) { View.Hint.text = riichiSelection ? "请选择立直宣言牌，再次点击确认" : "选中后再次点击出牌"; }
                return;
            }
            if (View.ChoicePanel != null && (index == 1 || index == 2 || index == 3))
            {
                PlayerAction[] types = index == 1 ? new[] { PlayerAction.Chi } : index == 2 ? new[] { PlayerAction.Peng }
                    : new[] { PlayerAction.ClosedKan, PlayerAction.AddedKan, PlayerAction.OpenKan };
                PlayerActionData[] choices = actions.Where(choice => types.Contains(choice.PlayerAction)).ToArray();
                if (choices.Length > 1) { View.ShowChoices(choices, Execute); return; }
            }
            PlayerActionData action = index switch
            {
                6 => actions.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.Draw),
                1 => actions.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.Chi),
                2 => actions.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.Peng),
                3 => actions.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.ClosedKan || choice.PlayerAction == PlayerAction.AddedKan || choice.PlayerAction == PlayerAction.OpenKan),
                4 => actions.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.Tsumo || choice.PlayerAction == PlayerAction.Ron),
                _ => actions.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.Skip)
            };
            if (action != null) { Execute(action); }
        }

        private void Execute(PlayerActionData action)
        {
            View.HideChoices();
            if (action.PlayerAction == PlayerAction.Riichi) { riichiDiscards[0] = core.DiscardTileList[0].Count; }
            Submit(action); riichiSelection = false; selected = -1;
            AdvanceOpponents();
            if (machine.Stage == MatchOperationStage.RoundEnded)
            {
                result = RoundSettlement.Calculate(core, journal.Outcome, journal.WinnerMask, journal.ResponseSourceSeat, journal.ResponseTile, journal.WinByRobbingKan);
                result.RoundId = journal.RoundId; result.RoundGeneration = journal.RoundGeneration;
                core.PlayerPoint = result.PointsAfter.ToList(); core.RiichiStickCount = result.SticksAfter;
                journal.Settlement = result; journal.RecordAppliedAction(null);
                Refresh("本局结束  ·  可以重置预览，再看一次牌桌"); View.ShowResult(result, 0);
            }
            else
            {
                selected = core.PlayerList[0].PlayerHandTile.SingleTile != null ? core.PlayerList[0].PlayerHandTile.HandTileList.Count : -1;
                Refresh(machine.Stage == MatchOperationStage.Discard || machine.Stage == MatchOperationStage.DiscardAfterCall
                    ? "选中后再次点击出牌" : "可以响应这张舍牌，或选择跳过");
            }
        }

        private void AdvanceOpponents()
        {
            for (int guard = 0; guard < 64 && machine.Stage != MatchOperationStage.RoundEnded; guard++)
            {
                bool response = machine.Stage == MatchOperationStage.DiscardResponse || machine.Stage == MatchOperationStage.AddedKanResponse || machine.Stage == MatchOperationStage.ClosedKanResponse;
                if (response)
                {
                    PlayerActionData[] bots = Enumerable.Range(1, 3).SelectMany(machine.GetActions).Where(choice => choice.PlayerAction == PlayerAction.Skip).ToArray();
                    foreach (PlayerActionData bot in bots) { Submit(bot); }
                    if (machine.GetActions(0).Count > 0 || bots.Length == 0) { return; }
                }
                else
                {
                    if (core.CurrentPlayerIndex == 0) { return; }
                    var choices = machine.GetActions(core.CurrentPlayerIndex);
                    PlayerActionData cut = choices.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.TakeCut)
                        ?? choices.FirstOrDefault(choice => choice.PlayerAction == PlayerAction.HandCut);
                    if (cut == null) { return; }
                    Submit(cut);
                }
            }
        }

        private void Submit(PlayerActionData action)
        {
            MatchCommand command = new() { MatchId = journal.MatchId, RoundId = journal.RoundId, RoundGeneration = journal.RoundGeneration,
                PhaseId = journal.PhaseId, CommandId = Guid.NewGuid(), Action = action };
            MatchCommandResult answer = machine.Submit(action.SelfNumber, command);
            if (!answer.Accepted) { Debug.LogError("Preview action failed: " + answer.Reason); }
        }

        private void Refresh(string hint)
        {
            var choices = machine.GetActions(0);
            View.Render(journal.GetSnapshot(0), riichiDiscards, selected, hint, choices, riichiSelection);
            bool Has(params PlayerAction[] types) => choices.Any(choice => types.Contains(choice.PlayerAction));
            bool[] enabled = { Has(PlayerAction.Riichi), Has(PlayerAction.Chi), Has(PlayerAction.Peng),
                Has(PlayerAction.ClosedKan, PlayerAction.OpenKan, PlayerAction.AddedKan), Has(PlayerAction.Tsumo, PlayerAction.Ron),
                Has(PlayerAction.Skip) };
            View.SetActions(new[] { "立 直", "吃", "碰", "杠", Has(PlayerAction.Ron) ? "荣 和" : "自 摸", "跳 过" }, enabled);
        }

        /// <summary>从136张实体牌分配可见牌、其他人暗手、王牌和25张牌山，预览不泄露对手暗手。</summary>
        private static MainMatchControl CreatePosition()
        {
            MainMatchControl state = new();
            state.ServerFourMatchInit(new MatchSettingData(MatchType.FourMahjongSouth), new[] { new UserProfile("你"), new UserProfile("青岚"), new UserProfile("白露"), new UserProfile("千寻") });
            List<Tile> pool = new();
            for (int face = 0; face < 34; face++)
            {
                Tile tile = HandAnalysis.TileOf(face);
                for (int copy = 0; copy < 4; copy++) { pool.Add(new Tile(tile.Color, tile.Number, tile.Color != MaJiangLib.Color.Honor && tile.Number == 5 && copy == 0)); }
            }
            Tile Take(string name)
            {
                Tile wanted = Tile.BytesTo(name); int index = pool.FindIndex(tile => Tile.ExactlyEquals(tile, wanted));
                if (index < 0) { throw new InvalidOperationException("Preview exhausted " + name); }
                Tile found = pool[index]; pool.RemoveAt(index); return found;
            }
            List<Tile> TakeHand(string text) => text.Split(' ').Select(Take).ToList();
            state.PlayerList[0].PlayerHandTile.HandTileList = TakeHand("2w 3w 4w 3p 4p 0p 6p 6p 4s 5s 6s 7s 8s");
            state.PlayerList[0].PlayerHandTile.SingleTile = Take("9s");
            state.PlayerList[2].PlayerHandTile.HandTileList = TakeHand("1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z");
            Tile indicator = Take("4p");
            string[] rivers = {
                "1w 9s 2z 8p 1p 6w 4z 3s 8w 2p 9p",
                "9w 1s 6z 2w 7p 8s 3z 4p 6p 7w 1z",
                "1p 5z 9w 6s 3w 8p 9p 1s 4w 5s 2z",
                "7w 9s 1w 5p 4s 2p 3z 8w 6w 6z 7s"
            };
            for (int seat = 0; seat < 4; seat++) { state.DiscardTileList[seat] = TakeHand(rivers[seat]); }
            GlobalFunction.Shuffle(pool, 20261025);
            foreach (int seat in new[] { 1, 3 }) { state.PlayerList[seat].PlayerHandTile.HandTileList = pool.Take(13).ToList(); pool.RemoveRange(0, 13); }
            state.PrimeTileList = new List<Tile> { indicator }; state.PrimeTileList.AddRange(pool.Take(13)); pool.RemoveRange(0, 13);
            state.MainTileList = pool; state.CurrentTileIndex = 0; state.PrimeTileIndex = 0; state.RemainTileCount = pool.Count;
            state.CurrentPlayerIndex = 0; state.CurrentDealerIndex = 0; state.FirstCycleIppatsu = false;
            state.DoraPointerList = new() { indicator }; state.PlayerPoint = new() { 25000, 27300, 25300, 21400 };
            state.IsRiichi[2] = true; state.RiichiStickCount = 1;
            state.CurrentTile = state.PlayerList[0].PlayerHandTile.SingleTile;
            return state;
        }
    }
}
