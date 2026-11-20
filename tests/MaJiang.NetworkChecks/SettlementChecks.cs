using MaJiangLib;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

internal static partial class Program
{
    private const string ScorePinfu = "2w 3w 4w 3p 4p 5p 4s 5s 6s 7s 8s 6p 6p";
    private static (MachineFixture Fixture, MatchSession Session, MatchCommand Ron) SettledRon(bool endMatch = false, bool riichi = false)
    {
        MachineFixture fixture = new(); fixture.Hand(1, ScorePinfu); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = Tile.BytesTo("9s");
        fixture.Core.FirstCycleIppatsu = false; fixture.Core.DoraPointerList = Tiles("1z"); fixture.Core.IsRiichi[1] = riichi;
        if (endMatch)
        {
            fixture.Core.Wind = WindType.South; fixture.Core.Round = 4; fixture.Core.CurrentDealerIndex = 1;
            fixture.Core.PlayerPoint = new() { 24000, 29000, 23000, 24000 };
        }
        fixture.Start(); MatchSession session = new(fixture.Journal, () => fixture.Now, machine: fixture.Machine);
        fixture.Act(0, PlayerAction.TakeCut); MatchCommand ron = fixture.Pick(1, PlayerAction.Ron);
        fixture.Machine.Submit(1, ron); session.Tick(); return (fixture, session, ron);
    }

    private static Task SettlementOnce()
    {
        var (fixture, session, ron) = SettledRon();
        Assert(fixture.Journal.Settlement != null && fixture.Core.PlayerPoint.SequenceEqual(new[] { 24000, 26000, 25000, 25000 }), "Win was not paid.");
        long sequence = fixture.Journal.LatestSequence;
        for (int repeat = 0; repeat < 4; repeat++) { session.Tick(); fixture.Machine.Submit(1, ron); }
        Assert(fixture.Journal.LatestSequence == sequence && fixture.Core.PlayerPoint[1] == 26000 && fixture.Journal.NextRoundMilliseconds == 10000,
            "Settlement/retry paid twice or reset display deadline.");
        return Task.CompletedTask;
    }

    private static Task SettlementNextRound()
    {
        var (fixture, session, ron) = SettledRon(); Guid previous = fixture.Journal.RoundId; AuthoritativeMatchMachine retired = fixture.Machine;
        fixture.Now = 9999; session.Tick(); Assert(fixture.Journal.RoundId == previous, "Result display ended early.");
        fixture.Now = 10000; session.Tick();
        Assert(fixture.Journal.RoundId != previous && fixture.Journal.RoundGeneration == 2 && fixture.Journal.LatestSequence == 0
            && fixture.Core.CurrentDealerIndex == 1 && fixture.Core.CurrentPlayerIndex == 1 && fixture.Core.RemainTileCount == 69
            && fixture.Core.PlayerPoint[1] == 26000 && fixture.Journal.Outcome == RoundOutcomeKind.None && fixture.Journal.Settlement.RoundId == previous
            && fixture.Journal.SettlementConfirmations == 0,
            "Next round reset points, dealer or result history.");
        Assert(fixture.Core.PlayerList.Select(player => player.PlayerHandTile.HandTileList.Count).All(count => count == 13)
            && fixture.Journal.GetSnapshot(1).ThinkingMilliseconds == 20000 && fixture.Journal.FuritenFlags.All(flags => flags == 0), "New round did not reset private state and banks.");
        Assert(session.Machine.Submit(1, ron).Reason == "StaleRound", "Old round command affected next round.");
        MatchCommand fresh = new() { MatchId = fixture.Journal.MatchId, RoundId = fixture.Journal.RoundId, RoundGeneration = 2,
            PhaseId = fixture.Journal.PhaseId, CommandId = Guid.NewGuid(), Action = session.Machine.GetActions(1).First(action => action.PlayerAction == PlayerAction.TakeCut) };
        Assert(retired.Submit(1, fresh).Reason == "RetiredMachine" && retired.GetActions(1).Count == 0, "Retired machine controlled new round.");
        return Task.CompletedTask;
    }

    private static Task SettlementVisibility()
    {
        MachineFixture fixture = new(); fixture.Hand(0, YakuhaiWait); fixture.Core.PlayerList[0].PlayerHandTile.SingleTile = null;
        fixture.Hand(2, YakuhaiWait); fixture.Start();
        for (int seat = 0; seat < 4; seat++) { fixture.Core.DiscardTileList[seat].Add(Tile.BytesTo("2p")); }
        fixture.Journal.OperationStage = MatchOperationStage.RoundEnded; fixture.Journal.Outcome = RoundOutcomeKind.ExhaustiveDraw; fixture.Journal.ResponseSourceSeat = 3;
        MatchSession session = new(fixture.Journal, () => fixture.Now, machine: fixture.Machine); session.Tick();
        Assert(fixture.Journal.Settlement.Revealed.Select(hand => hand.Seat).SequenceEqual(new[] { 0, 2 }) && fixture.Journal.Settlement.UraIndicators.Count == 0,
            "Draw revealed noten hands or ura.");
        for (int seat = 0; seat < 4; seat++)
        {
            VisibleMatchState snapshot = fixture.Journal.GetSnapshot(seat);
            Assert(snapshot.State.PlayerList.Where(player => player.PlayerNumber != seat).All(player => player.PlayerHandTile.HandTileList.Count == 0), "Reveal overwrote private projection.");
            Assert(snapshot.Settlement.Revealed.Count == 2 && snapshot.Settlement.UraIndicators.Count == 0, "Public result leaked unrelated hidden state.");
        }
        return Task.CompletedTask;
    }

    private static Task SettlementCodecChecks()
    {
        var (fixture, _, _) = SettledRon(); RoundSettlementResult result = fixture.Journal.Settlement;
        byte[] bytes = RoundSettlementCodec.Encode(result);
        Assert(bytes.SequenceEqual(RoundSettlementCodec.Encode(RoundSettlementCodec.Decode(bytes))), "Settlement did not round trip.");
        Throws<InvalidDataException>(() => RoundSettlementCodec.Decode(bytes.Concat(new byte[] { 1 }).ToArray()));
        RoundSettlementResult copy = RoundSettlementCodec.Copy(result); copy.PointsAfter[0]++;
        Throws<InvalidDataException>(() => RoundSettlementCodec.Encode(copy));
        copy = RoundSettlementCodec.Copy(result); copy.Revealed.Add(copy.Revealed[0]);
        Throws<InvalidDataException>(() => RoundSettlementCodec.Encode(copy));
        VisibleMatchState view = fixture.Journal.GetSnapshot(2); view.Settlement.Revealed[0].Concealed.Clear();
        Assert(fixture.Journal.GetSnapshot(2).Settlement.Revealed[0].Concealed.Count == 13, "Caller alias rewrote public settlement.");
        return Task.CompletedTask;
    }

    private static Task SettlementUra()
    {
        var (fixture, _, _) = SettledRon(riichi: true);
        Assert(fixture.Journal.Settlement.UraIndicators.Count == fixture.Core.DoraPointerList.Count, "Ura count differs from actually opened dora.");
        Assert(fixture.Journal.GetSnapshot(0).State.PrimeTileList == null && fixture.Journal.GetSnapshot(0).Settlement.UraIndicators.Count == 1,
            "Ura was not scoped to completed result.");
        var (normal, _, _) = SettledRon();
        Assert(normal.Journal.Settlement.UraIndicators.Count == 0, "Non-riichi win revealed ura.");
        return Task.CompletedTask;
    }

    private static Task SettlementFinal()
    {
        var (fixture, session, _) = SettledRon(endMatch: true); Guid round = fixture.Journal.RoundId;
        Assert(fixture.Journal.Settlement.MatchEnded && fixture.Journal.Settlement.Ranking[0] == 1 && fixture.Core.PlayerPoint[1] >= 30000,
            "Final dealer win did not end match.");
        fixture.Now = 1000000; session.Tick();
        Assert(fixture.Journal.RoundId == round && session.Machine.Stage == MatchOperationStage.RoundEnded && fixture.Journal.NextRoundMilliseconds == 0
            && fixture.Journal.SettlementConfirmations == MatchSession.AllSeatsConfirmed,
            "Finished match created another round.");
        return Task.CompletedTask;
    }

    private static Task SettlementWholeMatch()
    {
        MainMatchControl core = SyncCore(); long now = 0; MatchJournal journal = new(core, () => now); MatchSession session = new(journal, () => now, 0);
        int steps = 0, settled = 0; Guid counted = Guid.Empty;
        while (steps++ < 10000)
        {
            now += 25000; session.Tick();
            if (journal.Settlement != null && journal.Settlement.RoundId != counted) { settled++; counted = journal.Settlement.RoundId; }
            if (journal.Settlement?.MatchEnded == true) { break; }
        }
        Assert(journal.Settlement?.MatchEnded == true && settled >= 8 && core.Wind <= WindType.West && core.PlayerPoint.Sum() + core.RiichiStickCount * 1000 == 100000,
            "Default play did not finish a conserved complete match.");
        return Task.CompletedTask;
    }

    private static async Task SettlementTcpNextRound()
    {
        Fixture fixture = new(new LanRoomServer(() => 0));
        try
        {
            await SyncStart(fixture); MainMatchControl core = fixture.Server.Match.AuthoritativeState;
            for (int seat = 0; seat < 4; seat++)
            {
                core.PlayerList[seat].PlayerHandTile.HandTileList = Tiles(seat == 1 ? ScorePinfu : Inert);
                core.PlayerList[seat].PlayerHandTile.SingleTile = seat == 0 ? Tile.BytesTo("9s") : null;
            }
            core.FirstCycleIppatsu = false; core.DoraPointerList = Tiles("1z"); fixture.Server.Match.RecordAppliedAction(null);
            await Until(() => fixture.Clients.All(client => client.Match.Current.Sequence == fixture.Server.Match.LatestSequence), fixture.Pump);
            LanRoomClient source = fixture.Clients.Single(client => client.Match.Current.SelfSeat == 0);
            LanRoomClient winner = fixture.Clients.Single(client => client.Match.Current.SelfSeat == 1);
            source.SendCommand(source.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.TakeCut));
            await Until(() => winner.AvailableCommands.Any(command => command.Action.PlayerAction == PlayerAction.Ron), fixture.Pump);
            winner.SendCommand(winner.AvailableCommands.First(command => command.Action.PlayerAction == PlayerAction.Ron));
            await Until(() => fixture.Clients.All(client => client.Match.Current.Settlement?.RoundId == fixture.Server.Match.RoundId), fixture.Pump);
            Guid oldRound = fixture.Server.Match.RoundId;
            LanRoomClient guest = fixture.Clients.Single(client => client.Match.Current.SelfSeat == 2);
            Assert(guest.ConfirmSettlement(Guid.NewGuid()) == 0 && guest.ConfirmSettlement(oldRound) != 0, "Current settlement confirmation was not sent.");
            Assert(guest.ConfirmSettlement(oldRound) != 0, "Retry did not reach the server.");
            await Until(() => fixture.Clients.All(client => client.Match.Current.SettlementConfirmations == 4), fixture.Pump);
            Assert(fixture.Server.Match.RoundId == oldRound && fixture.Server.Match.NextRoundMilliseconds == 10000,
                "A single confirmation or a retry advanced the round or changed the deadline.");
            await guest.CloseAsync(); await Until(() => fixture.Server.ConnectionCount == 3, fixture.Pump);
            await guest.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "restored");
            await Until(() => guest.IsJoined && guest.Match.Current.Settlement?.RoundId == oldRound, fixture.Pump);
            Assert(guest.Match.Current.State.PlayerPoint[1] == 26000 && guest.Match.Current.Settlement.Revealed.Single().Seat == 1
                && guest.Match.Current.SettlementConfirmations == 4 && guest.Match.Current.NextRoundMilliseconds == 10000,
                "Reconnect lost payment, winning reveal, confirmation, or deadline.");
            foreach (LanRoomClient client in fixture.Clients.Where(client => client != guest))
            {
                Assert(client.ConfirmSettlement(oldRound) != 0, "A remaining seat could not confirm.");
            }
            await Until(() => fixture.Clients.All(client => client.Match.Current.RoundGeneration == 2), fixture.Pump);
            Assert(fixture.Server.Match.RoundId != oldRound && fixture.Clients.All(client => client.Match.Current.Settlement.RoundId == oldRound
                && client.Match.Current.State.PlayerPoint[1] == 26000 && client.Match.Current.State.CurrentDealerIndex == 1
                && client.Match.Current.SettlementConfirmations == 0), "Four confirmations did not advance or next TCP round lost settlement.");
            long sequence = fixture.Server.Match.LatestSequence;
            Assert(!fixture.Server.Session.ConfirmSettlement(2, fixture.Server.Match.MatchId, oldRound)
                && fixture.Server.Match.LatestSequence == sequence, "A stale confirmation affected the new round.");
            foreach (LanRoomClient client in fixture.Clients) { SyncSameState(client.Match.Current, fixture.Server.Match.GetSnapshot(client.Match.Current.SelfSeat)); }
        }
        finally { await fixture.Cleanup(); }
    }
}
