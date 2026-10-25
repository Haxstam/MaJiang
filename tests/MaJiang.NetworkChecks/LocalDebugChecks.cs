using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using MaJiangLib;

internal static partial class Program
{
    private static bool DebugSynced(LocalDebugMatch debug) => debug.Ready && Enumerable.Range(0, 4)
        .All(seat => debug.GetInput(seat).Current.Sequence == debug.Server.Match.LatestSequence && !debug.GetInput(seat).Pending);

    private static async Task DebugSeatsAndCommands()
    {
        LocalDebugMatch debug = new();
        try
        {
            debug.Start();
            await Until(() => DebugSynced(debug), debug.Pump);
            Assert(debug.Server.ConnectionCount == 4 && debug.Server.MatchSnapshotsSent >= 4, "Debug must use four real TCP peers.");
            for (int seat = 0; seat < 4; seat++)
            {
                VisibleMatchState view = debug.GetInput(seat).Current;
                Assert(view.SelfSeat == seat, "Random room seat assignment must be reflected in debug input mapping.");
                Assert(view.State.PlayerList[seat].PlayerHandTile.HandTileList.Count == 13, "Selected seat has no private hand.");
                for (int other = 0; other < 4; other++)
                {
                    if (other != seat) { Assert(view.State.PlayerList[other].PlayerHandTile.HandTileList.Count == 0, "Debug input bypassed the visible projection."); }
                }
            }
            IMatchDecisionInput input = Enumerable.Range(0, 4).Select(debug.GetInput).First(source => source.Choices.Count > 0);
            MatchCommand cut = input.Choices.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut);
            Assert(input.Submit(cut), "Valid private menu command failed to send.");
            Assert(input.Pending && input.Choices.Count == 0 && !input.Submit(cut), "Double clicks must be blocked until reply and projection.");
            await Until(() => DebugSynced(debug), debug.Pump);
            Assert(!input.Submit(cut), "An old menu object must not enter a new phase.");
            Assert(debug.Server.Match.AuthoritativeState.DiscardTileList.Sum(river => river.Count) == 1, "Duplicate input performed multiple discards.");
        }
        finally { await debug.StopAsync(); }
    }

    private static async Task DebugFullHand()
    {
        LocalDebugMatch debug = new();
        try
        {
            debug.Start();
            await Until(() => DebugSynced(debug), debug.Pump);
            long phase = debug.Server.Match.PhaseId;
            // 保持真实TCP活动，同时冻结所有牌局期限。
            DateTime frozenUntil = DateTime.UtcNow.AddMilliseconds(1300);
            while (DateTime.UtcNow < frozenUntil) { debug.Pump(); await Task.Delay(5); }
            Assert(debug.NowMilliseconds == 0 && debug.Server.Match.PhaseId == phase && debug.Ready, "Paused debug time changed a decision or dropped peers.");
            MatchCommand stale = debug.GetInput(debug.Server.Match.AuthoritativeState.CurrentPlayerIndex).Choices.First();
            debug.AdvanceTime(30000);
            await Until(() => DebugSynced(debug) && debug.Server.Match.PhaseId > phase, debug.Pump);
            Assert(!debug.GetInput(stale.Action.SelfNumber).Submit(stale), "Timed-out command remained sendable.");
            int decisions = 0;
            while (debug.Server.Match.OperationStage != MatchOperationStage.RoundEnded && decisions++ < 500)
            {
                await Until(() => DebugSynced(debug), debug.Pump);
                IMatchDecisionInput input = Enumerable.Range(0, 4).Select(debug.GetInput).First(source => source.Choices.Count > 0);
                MatchCommand command = input.Choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                    ?? input.Choices.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut);
                Assert(input.Submit(command), "Manual four-seat decision could not be sent.");
                await Until(() => DebugSynced(debug), debug.Pump);
            }
            await Until(() => DebugSynced(debug) && debug.Server.Match.Settlement != null, debug.Pump);
            Assert(debug.Server.Match.Outcome == RoundOutcomeKind.ExhaustiveDraw && decisions < 500, "Debug table did not complete a full hand.");
            Guid completed = debug.Server.Match.RoundId;
            Assert(debug.NextRound(), "Explicit next-round control failed.");
            await Until(() => DebugSynced(debug) && debug.Server.Match.RoundId != completed, debug.Pump);
            for (int seat = 0; seat < 4; seat++)
            {
                VisibleMatchState current = debug.GetInput(seat).Current;
                Assert(current.Settlement.RoundId == completed && current.RoundGeneration == 2, "New round lost last settlement or seat projection.");
                SyncSameState(current, debug.Server.Match.GetSnapshot(seat));
            }
        }
        finally { await debug.StopAsync(); }
    }

    private static async Task DebugShutdown()
    {
        LocalDebugMatch early = new(); early.Start(); int port = early.Server.Port;
        await early.StopAsync(); await early.StopAsync();
        TcpListener probe = new(IPAddress.Loopback, port);
        try { probe.Start(); } finally { probe.Stop(); }
        LocalDebugMatch fresh = new();
        try { fresh.Start(); await Until(() => DebugSynced(fresh), fresh.Pump); }
        finally { await fresh.StopAsync(); }
        Assert(!early.Ready && !fresh.Ready, "Stopped debug sessions still accept input.");
    }
}
