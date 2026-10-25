using MaJiangLib;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

internal static partial class Program
{
    private static LanRoomClient SeatClient(Fixture fixture, int seat) => fixture.Clients.Single(client => client.Match.Current.SelfSeat == seat);

    /// <summary>仅供网络测试布置指定手牌；真实操作仍由客户端菜单提交到当前权威机。</summary>
    private static async Task RecoveryHands(Fixture fixture, string drawn, params (int Seat, string Hand)[] hands)
    {
        MainMatchControl core = fixture.Server.Match.AuthoritativeState;
        for (int seat = 0; seat < 4; seat++)
        {
            core.PlayerList[seat].PlayerHandTile.HandTileList = Tiles(hands.Any(item => item.Seat == seat) ? hands.Single(item => item.Seat == seat).Hand : Inert);
            core.PlayerList[seat].PlayerHandTile.SingleTile = seat == 0 ? Tile.BytesTo(drawn) : null;
        }
        core.FirstCycleIppatsu = false; core.CurrentTile = Tile.BytesTo(drawn); core.DoraPointerList = Tiles("1z");
        fixture.Server.Match.RecordAppliedAction(null);
        await Until(() => fixture.Clients.All(client => client.Match.Current.Sequence == fixture.Server.Match.LatestSequence), fixture.Pump);
    }

    private static async Task RecoveryReconnect(Fixture fixture, LanRoomClient client)
    {
        Guid identity = client.PlayerId;
        await client.CloseAsync(); await Until(() => fixture.Server.ConnectionCount == 3, fixture.Pump);
        await client.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "恢复原身份");
        await Until(() => client.IsMatchSynchronized && client.Match.Current.Sequence == fixture.Server.Match.LatestSequence, fixture.Pump);
        Assert(client.PlayerId == identity, "Reconnect changed authenticated identity.");
    }

    private static async Task RecoveryMenuGate()
    {
        Fixture fixture = new();
        try
        {
            await SyncStart(fixture); LanRoomClient actor = SeatClient(fixture, 0);
            MatchCommand saved = actor.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.TakeCut);
            long sequence = fixture.Server.Match.LatestSequence;
            actor.RequestMatchSync(true);
            Assert(!actor.IsMatchSynchronized && actor.AvailableCommands.Count == 0 && actor.SendCommand(saved) == Guid.Empty,
                "Recovery-pending client exposed/sent a stale menu.");
            await Until(() => actor.IsMatchSynchronized, fixture.Pump);
            Assert(fixture.Server.Match.LatestSequence == sequence && actor.AvailableCommands.Count > 0, "Recovery consumed a turn or failed to reopen menu.");
            bool sawJoin = false;
            actor.OperationCompleted += (id, accepted, _) =>
            {
                if (id == 0 && accepted)
                {
                    sawJoin = true;
                    Assert(!actor.IsMatchSynchronized && actor.AvailableCommands.Count == 0, "Joined callback exposed old state before checkpoint.");
                }
            };
            await RecoveryReconnect(fixture, actor);
            Assert(sawJoin && actor.AvailableCommands.Count > 0, "Restored checkpoint did not reopen the current menu.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task RecoveryOwnDeadline()
    {
        long now = 0; Fixture fixture = new(new LanRoomServer(() => now));
        try
        {
            await SyncStart(fixture); LanRoomClient actor = SeatClient(fixture, 0); Guid identity = actor.PlayerId;
            MatchCommand late = actor.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.TakeCut);
            await actor.CloseAsync(); await Until(() => fixture.Server.ConnectionCount == 3, fixture.Pump);
            now = 25000; fixture.Pump();
            Assert(fixture.Server.Match.AuthoritativeState.DiscardTileList[0].Count == 1 && fixture.Server.Match.GetSnapshot(0).ThinkingMilliseconds == 0,
                "Disconnected player failed to default exactly at deadline.");
            await actor.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "deadline return");
            await Until(() => actor.IsMatchSynchronized, fixture.Pump);
            Assert(actor.PlayerId == identity && actor.Match.Current.ThinkingMilliseconds == 0 && actor.RemainingActionMilliseconds == 0, "Reconnect refunded the spent turn/bank.");
            long sequence = fixture.Server.Match.LatestSequence;
            actor.SendCommand(late);
            await Until(() => actor.LastCommandResult?.CommandId == late.CommandId, fixture.Pump);
            Assert(actor.LastCommandResult.Reason == "StalePhase" && fixture.Server.Match.LatestSequence == sequence, "Late pre-disconnect selection consumed a new opportunity.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task RecoveryAnsweredClaim()
    {
        long now = 0; Fixture fixture = new(new LanRoomServer(() => now));
        try
        {
            await SyncStart(fixture);
            await RecoveryHands(fixture, "3w", (1, "1w 2w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z"), (2, "3w 3w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z"));
            LanRoomClient source = SeatClient(fixture, 0), chi = SeatClient(fixture, 1), pon = SeatClient(fixture, 2);
            source.SendCommand(source.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.TakeCut));
            await Until(() => chi.AvailableCommands.Any(command => command.Action.PlayerAction == PlayerAction.Chi), fixture.Pump);
            now = 6000;
            MatchCommand chosen = chi.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.Chi); chi.SendCommand(chosen);
            await Until(() => chi.Match.Current.HasResponded && chi.LastCommandResult?.CommandId == chosen.CommandId, fixture.Pump);
            now = 7000; await RecoveryReconnect(fixture, chi);
            Assert(chi.Match.Current.OperationStage == MatchOperationStage.DiscardResponse && chi.Match.Current.HasResponded
                && chi.AvailableCommands.Count == 0 && chi.Match.Current.ThinkingMilliseconds == 19000, "Reconnect forgot a privately submitted choice or debited it again.");
            long sequence = fixture.Server.Match.LatestSequence; int replies = 0; chi.CommandCompleted += _ => replies++;
            chi.SendCommand(chosen); await Until(() => replies > 0, fixture.Pump);
            Assert(chi.LastCommandResult.Accepted && fixture.Server.Match.LatestSequence == sequence, "Cross-connection retry applied twice.");
            pon.SendCommand(pon.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.Skip));
            await Until(() => chi.Match.Current.OperationStage == MatchOperationStage.DiscardAfterCall, fixture.Pump);
            Assert(chi.Match.Current.State.CurrentPlayerIndex == 1 && chi.Match.Current.ThinkingMilliseconds == 19000
                && fixture.Server.Match.AuthoritativeState.PlayerOpenSetList[1].Count == 1, "Saved chi was lost or bank reset when higher claim passed.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task RecoveryRobKan()
    {
        long now = 0; Fixture fixture = new(new LanRoomServer(() => now));
        try
        {
            await SyncStart(fixture);
            await RecoveryHands(fixture, "5p", (0, "1w 2w 3w 4w 5w 6w 7s 8s 9s 5z"), (2, "1w 2w 3w 6p 7p 1s 2s 3s 7z 7z 7z 2z 2z"));
            MainMatchControl core = fixture.Server.Match.AuthoritativeState;
            core.PlayerOpenSetList[0].Add(new(GroupType.OpenTriple, Color.Tungs, Tiles("5p 5p"), 1, Tile.BytesTo("5p")));
            fixture.Server.Match.RecordAppliedAction(null);
            await Until(() => fixture.Clients.All(client => client.Match.Current.Sequence == fixture.Server.Match.LatestSequence), fixture.Pump);
            LanRoomClient source = SeatClient(fixture, 0), robber = SeatClient(fixture, 2);
            source.SendCommand(source.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.AddedKan));
            await Until(() => robber.Match.Current.OperationStage == MatchOperationStage.AddedKanResponse, fixture.Pump);
            long phase = robber.Match.Current.PhaseId; now = 6000; await RecoveryReconnect(fixture, robber);
            Assert(robber.Match.Current.PhaseId == phase && robber.Match.Current.ResponseSourceSeat == 0 && robber.Match.Current.ResponseTile == Tile.BytesTo("5p")
                && robber.Match.Current.ThinkingMilliseconds == 19000 && core.KangCount == 0, "Reconnect lost rob-kan window or committed it early.");
            robber.SendCommand(robber.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.Ron));
            await Until(() => robber.Match.Current.Settlement?.RoundId == robber.Match.Current.RoundId, fixture.Pump);
            Assert(robber.Match.Current.Settlement.Winners.Single().Score.Yaku.Any(yaku => yaku.Yaku == YakuType.Chankan)
                && core.KangCount == 0 && core.PrimeTileIndex == 0 && core.PlayerOpenSetList[0].Single().GroupType == GroupType.OpenTriple,
                "Restored rob-kan paid incorrectly or completed kan/rinshan.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task RecoveryAcrossRound()
    {
        long now = 0; Fixture fixture = new(new LanRoomServer(() => now));
        try
        {
            await SyncStart(fixture); await RecoveryHands(fixture, "9s", (1, ScorePinfu));
            LanRoomClient source = SeatClient(fixture, 0), winner = SeatClient(fixture, 1), offline = SeatClient(fixture, 2);
            source.SendCommand(source.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.TakeCut));
            await Until(() => winner.AvailableCommands.Any(command => command.Action.PlayerAction == PlayerAction.Ron), fixture.Pump);
            winner.SendCommand(winner.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.Ron));
            await Until(() => offline.Match.Current.Settlement != null, fixture.Pump);
            Guid oldRound = offline.Match.Current.RoundId;
            await offline.CloseAsync(); await Until(() => fixture.Server.ConnectionCount == 3, fixture.Pump);
            now = 5000; fixture.Pump();
            await Until(() => fixture.Server.Match.RoundGeneration == 2, fixture.Pump);
            await offline.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "new round return");
            await Until(() => offline.IsMatchSynchronized && offline.Match.Current.RoundGeneration == 2, fixture.Pump);
            VisibleMatchState view = offline.Match.Current;
            Assert(view.RoundId != oldRound && view.Settlement.RoundId == oldRound && view.State.PlayerPoint[1] == 26000
                && view.ThinkingMilliseconds == 20000 && view.State.PlayerList[2].PlayerHandTile.HandTileList.Count == 13,
                "Offline round transition lost score/history or private reset.");
            SyncSameState(view, fixture.Server.Match.GetSnapshot(2));
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task RecoveryHeartbeatTimeout()
    {
        Fixture fixture = new();
        try
        {
            Throws<ArgumentOutOfRangeException>(() => new LanRoomClient(TimeSpan.Zero));
            LanRoomClient client = new(TimeSpan.FromSeconds(2.5)); fixture.Clients.Add(client);
            await client.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "heartbeat");
            await Until(() => client.IsJoined, fixture.Pump);
            Stopwatch healthy = Stopwatch.StartNew();
            await Until(() => healthy.ElapsedMilliseconds >= 3500, fixture.Pump, 5000);
            Assert(client.IsConnected, "Regular server heartbeats caused a false timeout.");
            Guid identity = client.PlayerId, token = client.ResumeToken;
            await Until(() => !client.IsConnected, client.Pump, 4000);
            Assert(!client.IsJoined && client.LastError.Contains("heartbeat") && client.PlayerId == identity && client.ResumeToken == token,
                "Silent server stayed connected or timeout lost recovery identity.");
        }
        finally { await fixture.Cleanup(); }
    }
}
