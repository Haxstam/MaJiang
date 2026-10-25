using System;
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
        private Guid renderedRound;
        private readonly int[] riichiDiscards = { -1, -1, -1, -1 };
        private bool panelOpen = true;
        private Vector2 scroll;
        private MatchCommand[] variants;
        private string message;
        private double clockFraction;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle boxStyle;
        private GameObject inputShield;
        private RectTransform shieldRect;
        private int startedFrame;
        private IMatchDecisionInput InputSeat => match?.GetInput(seat);

        private void Start()
        {
            startedFrame = Time.frameCount;
            if (View == null) { View = GetComponent<MahjongTableView>(); }
            View.TileClicked += OnTile;
            for (int index = 0; index < View.Actions.Length; index++)
            {
                int action = index;
                View.Actions[index].onClick.AddListener(() => OnAction(action));
            }
            View.ResetButton.onClick.AddListener(Restart);
            View.RoomButton.onClick.AddListener(() => panelOpen = !panelOpen);
            View.ResetButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "重开整场";
            View.RoomButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "调试";
            View.ModalClose.onClick.AddListener(() => View.Modal.SetActive(false));
            View.ModalReset.onClick.AddListener(() => { match.NextRound(); View.Modal.SetActive(false); });
            inputShield = new GameObject("Debug Panel Input Shield", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inputShield, gameObject.scene);
            Canvas shieldCanvas = inputShield.GetComponent<Canvas>();
            shieldCanvas.renderMode = RenderMode.ScreenSpaceOverlay; shieldCanvas.sortingOrder = 30000;
            GameObject shield = new("Panel Raycast Blocker", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            shield.transform.SetParent(inputShield.transform, false);
            shield.GetComponent<UnityEngine.UI.Image>().color = UnityEngine.Color.clear;
            shieldRect = shield.GetComponent<RectTransform>();
            shieldRect.anchorMin = shieldRect.anchorMax = new Vector2(0, 1); shieldRect.pivot = new Vector2(0, 1);
            foreach (UnityEngine.UI.Text text in View.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            {
                if (text.name == "Footer Preview Mode") { text.text = "本机四客户端调试 · F1 控制面板"; }
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
            View.Modal.SetActive(false);
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
                View.Status.text = match.Status; View.Hint.text = message;
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
            View.Timer.text = PauseClock ? "停" : Math.Ceiling(Math.Max(0, current.DeadlineMilliseconds - match.NowMilliseconds) / 1000.0).ToString("00");
            View.HeaderState.text = $"四座调试  /  正在代选 {seat + 1} 号  /  F1 面板";
            View.Status.text = pending ? "正在等待服务端确认" : InputSeat.Choices.Count > 0 ? $"请为 {seat + 1} 号选择" : "该座位当前无需操作";
            RoundSettlementResult result = current.Settlement;
            if (current.OperationStage == MatchOperationStage.RoundEnded && result != null && result.RoundId != shownResult)
            {
                shownResult = result.RoundId; View.ShowResult(result, seat);
                View.ModalReset.GetComponentInChildren<UnityEngine.UI.Text>().text = result.MatchEnded ? "整场结束" : "下一局";
                View.ModalReset.interactable = !result.MatchEnded;
            }
            else if (current.OperationStage != MatchOperationStage.RoundEnded && shownResult != Guid.Empty)
            {
                shownResult = Guid.Empty; View.Modal.SetActive(false);
            }
        }

        private void SelectSeat(int next)
        {
            if (seat == next) { return; }
            seat = next; selected = -1; riichiSelection = false; variants = null; renderedSeat = -1;
        }

        private void Render()
        {
            IMatchDecisionInput input = InputSeat;
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
            if (variants != null && variants.Any(command => !input.Choices.Contains(command))) { variants = null; }
            View.Render(rendered, riichiDiscards, selected, riichiSelection ? "请选择立直宣言牌，再次点击确认" : "数字键 1–4 切换座位 · 选中手牌后再次点击出牌 · F1 调试面板");
            bool Has(params PlayerAction[] types) => input.Choices.Any(command => types.Contains(command.Action.PlayerAction));
            View.SetActions(new[] { "立 直", "吃", "碰", "杠", Has(PlayerAction.Ron) ? "荣 和" : "自 摸", Has(PlayerAction.Skip) ? "跳 过" : "摸 切" },
                new[] { Has(PlayerAction.Riichi), Has(PlayerAction.Chi), Has(PlayerAction.Peng), Has(PlayerAction.ClosedKan, PlayerAction.OpenKan, PlayerAction.AddedKan),
                    Has(PlayerAction.Ron, PlayerAction.Tsumo), Has(PlayerAction.Skip, PlayerAction.TakeCut) });
        }

        private void OnTile(MahjongTileWidget widget)
        {
            if (InputSeat == null || InputSeat.Choices.Count == 0) { return; }
            if (selected != widget.HandIndex) { selected = widget.HandIndex; View.SetSelected(selected); return; }
            PlayerAction type = riichiSelection ? PlayerAction.Riichi : widget.Drawn ? PlayerAction.TakeCut : PlayerAction.HandCut;
            MatchCommand command = InputSeat.Choices.FirstOrDefault(choice => choice.Action.PlayerAction == type && Tile.ExactlyEquals(choice.Action.TargetTile, widget.Tile));
            if (command != null) { Submit(command); }
            else { View.Hint.text = "该牌不能用于当前操作，请查看调试面板中的合法选择"; }
        }

        private void OnAction(int index)
        {
            if (InputSeat == null) { return; }
            if (index == 0)
            {
                riichiSelection = !riichiSelection; Render(); return;
            }
            PlayerAction[] types = index switch
            {
                1 => new[] { PlayerAction.Chi }, 2 => new[] { PlayerAction.Peng },
                3 => new[] { PlayerAction.OpenKan, PlayerAction.ClosedKan, PlayerAction.AddedKan },
                4 => new[] { PlayerAction.Ron, PlayerAction.Tsumo },
                _ => new[] { PlayerAction.Skip, PlayerAction.TakeCut }
            };
            MatchCommand[] choices = InputSeat.Choices.Where(command => types.Contains(command.Action.PlayerAction)).ToArray();
            if (choices.Length == 1) { Submit(choices[0]); }
            else if (choices.Length > 1) { variants = choices; panelOpen = true; scroll = Vector2.zero; }
        }

        private void Submit(MatchCommand command)
        {
            if (!InputSeat.Submit(command)) { message = "操作未发送，请等待同步后重新选择"; return; }
            message = $"已发送 {seat + 1} 号：{Describe(command)}";
            selected = -1; riichiSelection = false; variants = null;
            Render();
        }

        private void OnGUI()
        {
            if (View?.Art == null) { return; }
            labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true };
            boxStyle ??= new GUIStyle(GUI.skin.box);
            labelStyle.font = buttonStyle.font = boxStyle.font = View.Art.GetFont();
            Matrix4x4 previous = GUI.matrix;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Mathf.Min(410, Screen.width / scale - 20);
            float panelHeight = Mathf.Min(730, Screen.height / scale - 112);
            if (shieldRect != null)
            {
                shieldRect.anchoredPosition = new Vector2(12 * scale, -92 * scale);
                shieldRect.sizeDelta = panelOpen ? new Vector2(width * scale, panelHeight * scale) : new Vector2(150 * scale, 34 * scale);
            }
            if (!panelOpen)
            {
                if (GUI.Button(new Rect(12, 92, 150, 34), "四座调试面板 (F1)", buttonStyle)) { panelOpen = true; }
                GUI.matrix = previous; return;
            }
            GUI.Window(GetInstanceID(), new Rect(12, 92, width, panelHeight), DrawPanel, "", boxStyle);
            GUI.matrix = previous;
        }

        private void DrawPanel(int id)
        {
            GUILayout.Label("单人四座调试 · 真实 TCP 回环", labelStyle);
            if (match == null) { return; }
            GUILayout.Label(match.Status, labelStyle);
            GUILayout.BeginHorizontal();
            for (int index = 0; index < 4; index++)
            {
                IMatchDecisionInput input = match.GetInput(index);
                string state = input == null ? "连接中" : input.Pending ? "待确认" : input.Choices.Count > 0 ? "待操作" : input.Current?.HasResponded == true ? "已答复" : "等待";
                if (GUILayout.Button($"{index + 1}号{(index == seat ? " ◀" : "")}\n{state}", buttonStyle, GUILayout.Height(48))) { SelectSeat(index); }
            }
            GUILayout.EndHorizontal();
            FollowPendingSeat = GUILayout.Toggle(FollowPendingSeat, "自动切换到待操作座位", labelStyle);
            PauseClock = GUILayout.Toggle(PauseClock, "暂停对局计时（网络继续运行）", labelStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("时间 +1秒", buttonStyle)) { match.AdvanceTime(1000); }
            if (GUILayout.Button("时间 +30秒", buttonStyle)) { match.AdvanceTime(30000); }
            if (GUILayout.Button("下一局", buttonStyle)) { match.NextRound(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("重开整场", buttonStyle)) { Restart(); }
            if (GUILayout.Button("收起 (F1)", buttonStyle)) { panelOpen = false; }
            GUILayout.EndHorizontal();
            IMatchDecisionInput current = InputSeat;
            if (current?.Current != null)
            {
                VisibleMatchState view = current.Current;
                GUILayout.Label($"视角 {seat + 1}号 · {view.OperationStage}\n局代数 {view.RoundGeneration} · 阶段 {view.PhaseId} · 日志 {view.Sequence}", labelStyle);
                if (view.ResponseTile != null) { GUILayout.Label($"响应 {view.ResponseSourceSeat + 1}号的 {view.ResponseTile}", labelStyle); }
                if (current.Pending) { GUILayout.Label("等待服务端确认，当前座位输入已锁定", labelStyle); }
            }
            GUILayout.Label(message ?? "", labelStyle);
            string error = match.SeatError(seat);
            if (!string.IsNullOrEmpty(error)) { GUILayout.Label("客户端提示：" + error, labelStyle); }
            GUILayout.Label(variants != null ? "请选择具体组合：" : "全部合法操作（含九种九牌等）：", labelStyle);
            if (variants != null && GUILayout.Button("返回全部操作", buttonStyle)) { variants = null; }
            // 点击可能清空variants；复制本次菜单，避免OnGUI重入和提交后枚举变化。
            MatchCommand[] choices = variants ?? current?.Choices.ToArray() ?? Array.Empty<MatchCommand>();
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (MatchCommand command in choices)
            {
                if (GUILayout.Button(Describe(command), buttonStyle, GUILayout.MinHeight(32))) { Submit(command); break; }
            }
            GUILayout.EndScrollView();
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
            if (match != null) { stopping = match.StopAsync(); }
            if (inputShield != null) { Destroy(inputShield); }
        }
        private void OnDisable() { if (inputShield != null) { inputShield.SetActive(false); } }
        private void OnEnable() { if (inputShield != null) { inputShield.SetActive(true); } }
    }
}
