using MaJiangLib;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>Unity客户端入口。输入与显示在主线程，网络会话由LanRoomClient管理。</summary>
public class NetworkControl : MonoBehaviour
{
    public string testName;
    public string testIPAddress;
    public string serverIPAddress = "127.0.0.1";
    public int serverPort = 11111;
    public KeyCode clientKeyCode;
    public TextMeshProUGUI textMeshPro;
    public bool IsHost;
    public MatchServer MatchServer;
    private readonly LanRoomClient client = new();
    private readonly CancellationTokenSource lifetime = new();
    private Task connectionTask = Task.CompletedTask;
    private int selectedTileIndex;
    public LanRoomClient Client => client;
    public RoomSnapshot Room => client.Snapshot;
    public MainMatchControl MatchControl => client.Match.Current?.State;
    public bool IsPlaying => client.IsJoined && Room?.Phase == RoomPhase.Playing;
    public int SelfPlayerNumber => Room?.Members.FirstOrDefault(member => member.PlayerId == client.PlayerId)?.Seat ?? -1;

    private void Awake()
    {
        if (string.IsNullOrWhiteSpace(testName)) { testName = "Client"; }
        if (MatchServer == null) { MatchServer = GetComponent<MatchServer>(); }
        if (MatchServer != null) { IsHost = true; }
        client.OperationCompleted += (id, accepted, reason) =>
        {
            if (accepted) { Debug.Log($"[{testName}] {reason}"); }
            else { Debug.LogWarning($"[{testName}] {reason}"); }
        };
        client.CommandCompleted += result =>
        {
            if (result.Accepted) { Debug.Log($"[{testName}] 操作已确认"); }
            else { Debug.LogWarning($"[{testName}] {result.Reason}"); }
        };
    }

    private void Update()
    {
        client.Pump();
        if (Input.GetKey(clientKeyCode))
        {
            if (Input.GetKeyDown(KeyCode.Z)) { ConnectToServer(); }
            if (Input.GetKeyDown(KeyCode.X)) { SendReadySignal(); }
            if (Input.GetKeyDown(KeyCode.C)) { SendUnReadySignal(); }
            if (Input.GetKeyDown(KeyCode.V)) { StartRoomMatch(); }
            if (Input.GetKeyDown(KeyCode.U)) { LeaveRoom(); }
            if (client.IsJoined && MatchControl != null)
            {
                HandTile hand = MatchControl.PlayerList[SelfPlayerNumber].PlayerHandTile;
                int count = hand.HandTileList.Count + (hand.SingleTile != null ? 1 : 0);
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { selectedTileIndex--; }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { selectedTileIndex++; }
                selectedTileIndex = count == 0 ? 0 : (selectedTileIndex % count + count) % count;
                Tile selected = selectedTileIndex < hand.HandTileList.Count ? hand.HandTileList[selectedTileIndex] : hand.SingleTile;
                if (Input.GetKeyDown(KeyCode.Return)) { SubmitDebugChoice(action => (action.PlayerAction == PlayerAction.HandCut || action.PlayerAction == PlayerAction.TakeCut) && Tile.ExactlyEquals(action.TargetTile, selected)); }
                if (Input.GetKeyDown(KeyCode.R)) { SubmitDebugChoice(action => action.PlayerAction == PlayerAction.Riichi && Tile.ExactlyEquals(action.TargetTile, selected)); }
                if (Input.GetKeyDown(KeyCode.H)) { SubmitDebugChoice(action => action.PlayerAction == PlayerAction.Ron || action.PlayerAction == PlayerAction.Tsumo); }
                if (Input.GetKeyDown(KeyCode.K)) { SubmitDebugChoice(action => action.PlayerAction == PlayerAction.OpenKan || action.PlayerAction == PlayerAction.ClosedKan || action.PlayerAction == PlayerAction.AddedKan); }
                if (Input.GetKeyDown(KeyCode.P)) { SubmitDebugChoice(action => action.PlayerAction == PlayerAction.Peng); }
                if (Input.GetKeyDown(KeyCode.J)) { SubmitDebugChoice(action => action.PlayerAction == PlayerAction.Chi); }
                if (Input.GetKeyDown(KeyCode.D)) { SubmitDebugChoice(action => action.PlayerAction == PlayerAction.Draw); }
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    MatchCommand command = client.AvailableCommands.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.TakeCut || choice.Action.PlayerAction == PlayerAction.Skip)
                        ?? client.AvailableCommands.Where(choice => choice.Action.PlayerAction == PlayerAction.HandCut).OrderBy(choice => choice.Action.TargetTile).LastOrDefault();
                    client.SendCommand(command);
                }
            }
        }
        if (textMeshPro != null)
        {
            string members = Room == null ? string.Empty : string.Join("\n", Room.Members.Select(member =>
                $"{member.Seat + 1}. {member.Name} {(member.IsHost ? "房主" : "")} {(member.Connected ? (member.Ready ? "已准备" : "未准备") : "已断线")}"));
            textMeshPro.text = $"{testName} {(client.IsJoined ? "已入房" : client.IsConnecting ? "连接中" : "未入房")}\n{Room?.Phase}\n{members}";
            if (client.IsJoined && MatchControl != null)
            {
                HandTile hand = MatchControl.PlayerList[SelfPlayerNumber].PlayerHandTile;
                string draw = hand.SingleTile != null && selectedTileIndex == hand.HandTileList.Count ? $"[{hand.SingleTile}]" : hand.SingleTile?.ToString();
                textMeshPro.text += $"\n手牌 {string.Join(" ", hand.HandTileList.Select((tile, index) => index == selectedTileIndex ? $"[{tile}]" : tile.ToString()))}\n摸牌 {draw}\n剩余牌 {MatchControl.RemainTileCount}\n剩余思考 {client.RemainingActionMilliseconds / 1000.0:F1}秒\n{client.Match.Current.OperationStage}";
                if (client.Match.Current.Outcome != RoundOutcomeKind.None) { textMeshPro.text += $"\n本局结束：{client.Match.Current.Outcome}"; }
                textMeshPro.text += $"\n{MatchControl.Wind} {MatchControl.Round}局 {MatchControl.Honba}本场 供托{MatchControl.RiichiStickCount}\n点数 {string.Join(" / ", MatchControl.PlayerPoint)}";
                RoundSettlementResult result = client.Match.Current.Settlement;
                if (result != null)
                {
                    textMeshPro.text += $"\n{(result.RoundGeneration == client.Match.Current.RoundGeneration ? "本局" : "上局")}结算：{string.Join(" / ", result.Deltas.Select(points => points.ToString("+0;-0;0")))}";
                    foreach (WinnerSettlement winner in result.Winners)
                    {
                        string score = winner.Score.Yaku.Any(yaku => yaku.Yaku == YakuType.NagashiMangan) ? "流局满贯"
                            : winner.Score.Yakuman > 0 ? $"{winner.Score.Yakuman}倍役满" : $"{winner.Score.Han}番{winner.Score.Fu}符";
                        textMeshPro.text += $"\n{winner.Seat + 1}号 {score} 和牌{winner.WinningTile} {string.Join("、", winner.Score.Yaku.Select(yaku => yaku.Yaku))}";
                    }
                    foreach (RevealedHand revealed in result.Revealed)
                    {
                        textMeshPro.text += $"\n公开 {revealed.Seat + 1}号：{string.Join(" ", revealed.Concealed)} {revealed.Drawn}";
                        if (revealed.Groups.Count > 0) { textMeshPro.text += $" 副露 {string.Join(" / ", revealed.Groups.Select(group => $"{group.GroupType}({string.Join(" ", group.Tiles)})"))}"; }
                    }
                    if (result.UraIndicators.Count > 0) { textMeshPro.text += $"\n里宝牌指示 {string.Join(" ", result.UraIndicators)}"; }
                    if (result.MatchEnded)
                    {
                        textMeshPro.text += $"\n整场结束：{string.Join(" > ", result.Ranking.Select(seat => $"{seat + 1}号({result.PointsAfter[seat]})"))}";
                    }
                    else if (client.Match.Current.OperationStage == MatchOperationStage.RoundEnded)
                    {
                        textMeshPro.text += $"\n{client.RemainingResultMilliseconds / 1000.0:F1}秒后进入下一局";
                    }
                }
            }
        }
    }

    /// <summary>手动指定服务端地址；本机房主通过与普通玩家相同的客户端协议入房。</summary>
    public void ConnectToServer(string serverIPAddress = null, int port = 0)
    {
        if (!connectionTask.IsCompleted || client.IsConnected) { return; }
        Guid hostProof = Guid.Empty;
        if (IsHost)
        {
            if (MatchServer == null) { MatchServer = FindFirstObjectByType<MatchServer>(); }
            if (MatchServer == null || !MatchServer.IsListening)
            {
                Debug.LogWarning("房主服务端尚未启动。");
                return;
            }
            hostProof = MatchServer.Server.HostJoinToken;
            if (client.RoomId != Guid.Empty && client.RoomId != MatchServer.Server.Room.RoomId) { client.ForgetSession(); }
        }
        string address = serverIPAddress ?? this.serverIPAddress;
        int targetPort = port != 0 ? port : IsHost && MatchServer != null ? MatchServer.Server.Port : serverPort;
        connectionTask = ConnectAndReportAsync(address, targetPort, hostProof);
    }

    private async Task ConnectAndReportAsync(string address, int port, Guid hostProof)
    {
        try { await client.ConnectAsync(IPAddress.Parse(address), port, testName, hostProof, lifetime.Token); }
        catch (Exception exception)
        {
            if (!lifetime.IsCancellationRequested) { Debug.LogError($"[{testName}] 连接失败：{exception.Message}"); }
        }
    }

    public void SendReadySignal() => client.SetReady(true);
    public void SendUnReadySignal() => client.SetReady(false);
    public void StartRoomMatch() => client.StartMatch();
    public void LeaveRoom() => client.LeaveRoom();
    public Task DisconnectAsync(bool forgetSession = false) => client.CloseAsync(forgetSession);

    private void SubmitDebugChoice(Func<PlayerActionData, bool> predicate)
    {
        client.SendCommand(client.AvailableCommands.FirstOrDefault(command => predicate(command.Action)));
    }

    private void OnDestroy()
    {
        lifetime.Cancel();
        _ = client.CloseAsync();
    }
}
