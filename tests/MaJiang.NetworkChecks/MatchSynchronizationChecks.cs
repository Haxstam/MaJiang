using MaJiangLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MatchType = MaJiangLib.MatchType;

internal static partial class Program
{
    private static MainMatchControl SyncCore()
    {
        MainMatchControl core = new();
        core.ServerFourMatchInit(new(MatchType.FourMahjongSouth), new[] { new UserProfile("A"), new UserProfile("B"), new UserProfile("C"), new UserProfile("D") },
            new Xoshiro256StarStar(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
        core.DoraPointerList.Add(core.PrimeTileList[0]);
        return core;
    }

    private static PlayerActionData SyncDraw(MatchJournal journal, int seat)
    {
        MainMatchControl core = journal.AuthoritativeState;
        core.CurrentPlayerIndex = seat; core.CurrentStageType = StageType.DiscardStage;
        PlayerActionData draw = new(PlayerAction.Take) { SelfNumber = seat, TargetPlayerNumber = seat, TargetTile = core.GetNextTile(false) };
        core.PlayerList[seat].PlayerHandTile.SingleTile = draw.TargetTile;
        core.CurrentTile = draw.TargetTile;
        journal.PhaseId++; journal.RecordAppliedAction(draw); return draw;
    }

    private static void SyncDiscard(MatchJournal journal, int seat, bool handCut = false)
    {
        MainMatchControl core = journal.AuthoritativeState;
        HandTile hand = core.PlayerList[seat].PlayerHandTile;
        PlayerActionData discard = new(handCut ? PlayerAction.HandCut : PlayerAction.TakeCut)
        {
            SelfNumber = seat, TargetPlayerNumber = seat, TargetTile = handCut ? hand.HandTileList[0] : hand.SingleTile
        };
        if (handCut)
        {
            int index = hand.HandTileList.FindIndex(tile => Tile.ExactlyEquals(tile, discard.TargetTile));
            hand.HandTileList.RemoveAt(index);
            if (hand.SingleTile != null) { hand.HandTileList.Add(hand.SingleTile); }
        }
        hand.SingleTile = null;
        core.DiscardTileList[seat].Add(discard.TargetTile); core.CurrentTile = discard.TargetTile;
        core.CurrentStageType = StageType.ClaimStage;
        journal.PhaseId++; journal.RecordAppliedAction(discard);
    }

    private static void SyncSameState(VisibleMatchState actual, VisibleMatchState expected)
    {
        VisibleMatchState left = actual.Copy(); VisibleMatchState right = expected.Copy();
        left.ServerMilliseconds = right.ServerMilliseconds = 0;
        Assert(MatchSyncCodec.EncodeSnapshot(left).SequenceEqual(MatchSyncCodec.EncodeSnapshot(right)), "Replayed state differs from checkpoint.");
    }

    private static Task SyncVisibility()
    {
        MatchJournal journal = new(SyncCore(), () => 100);
        SyncDraw(journal, 2);
        for (int seat = 0; seat < 4; seat++)
        {
            VisibleMatchState view = MatchSyncCodec.DecodeSnapshot(MatchSyncCodec.EncodeSnapshot(journal.GetSnapshot(seat)));
            Assert(view.State.MainTileList == null && view.State.PrimeTileList == null && view.State.Random == null && view.Settlement == null, "Hidden wall/ura/seed leaked.");
            for (int other = 0; other < 4; other++)
            {
                HandTile hand = view.State.PlayerList[other].PlayerHandTile;
                Assert(other == seat ? hand.HandTileList.Count == 13 : hand.HandTileList.Count == 0 && hand.SingleTile == null, "Other player's hand leaked.");
            }
            Assert((view.State.CurrentTile != null) == (seat == 2), "CurrentTile leaked a hidden draw.");
            PlayerActionData action = MatchSyncCodec.DecodeBatch(MatchSyncCodec.EncodeBatch(journal.GetDelta(seat, -1))).Records[0].Action;
            Assert(seat == 2 ? action.TargetTile != null : action.TargetTile == null && action.Tiles.Count == 0, "Draw log was not receiver-masked.");
        }
        return Task.CompletedTask;
    }

    private static Task SyncReplay()
    {
        MatchJournal journal = new(SyncCore(), () => 0);
        for (int turn = 0; turn < 20; turn++) { int seat = turn % 4; SyncDraw(journal, seat); SyncDiscard(journal, seat, turn % 2 == 0); }
        for (int seat = 0; seat < 4; seat++)
        {
            ClientMatchProjection projection = new(); projection.Install(journal.GetBaseline(seat));
            while (projection.Current.Sequence < journal.LatestSequence)
            {
                MatchLogBatch batch = MatchSyncCodec.DecodeBatch(MatchSyncCodec.EncodeBatch(journal.GetDelta(seat, projection.Current.Sequence)));
                Assert(projection.Apply(batch), "Contiguous batch rejected.");
            }
            SyncSameState(projection.Current, journal.GetSnapshot(seat));
        }
        return Task.CompletedTask;
    }

    private static Task SyncSequenceRules()
    {
        MatchJournal journal = new(SyncCore(), () => 0); SyncDraw(journal, 0); SyncDiscard(journal, 0);
        ClientMatchProjection projection = new(); projection.Install(journal.GetBaseline(0));
        MatchLogBatch full = journal.GetDelta(0, -1);
        MatchLogBatch gap = new() { MatchId = full.MatchId, RoundId = full.RoundId, RoundGeneration = full.RoundGeneration, LatestSequence = full.LatestSequence, Records = full.Records.Skip(1).ToArray() };
        Assert(!projection.Apply(gap) && projection.Current.Sequence == -1 && projection.Current.State.RemainTileCount == 70, "A gap partially advanced state.");
        Assert(projection.Apply(full), "Repair batch failed.");
        byte[] before = MatchSyncCodec.EncodeSnapshot(projection.Current);
        Assert(projection.Apply(full) && before.SequenceEqual(MatchSyncCodec.EncodeSnapshot(projection.Current)), "Duplicate batch changed state twice.");
        VisibleMatchState publicView = journal.GetSnapshot(1);
        Tile discarded = journal.AuthoritativeState.DiscardTileList[0].Last();
        Assert(Tile.ExactlyEquals(publicView.State.CurrentTile, discarded), "Public context used the previous private draw.");
        return Task.CompletedTask;
    }

    private static Task SyncPrivateState()
    {
        MatchJournal journal = new(SyncCore(), () => 400);
        journal.PhaseId = 9; journal.DeadlineMilliseconds = 9000;
        journal.FuritenFlags[2] = 2; journal.HasResponded[2] = true; journal.ThinkingMilliseconds[2] = 7000;
        journal.RecordAppliedAction(null);
        for (int seat = 0; seat < 4; seat++)
        {
            ClientMatchProjection projection = new(); projection.Install(journal.GetBaseline(seat));
            Assert(projection.Apply(journal.GetDelta(seat, -1)), "Private ruling replay failed.");
            VisibleMatchState view = projection.Current;
            Assert(seat == 2 ? view.FuritenFlags == 2 && view.HasResponded && view.ThinkingMilliseconds == 7000
                : view.FuritenFlags == 0 && !view.HasResponded && view.ThinkingMilliseconds == 20000, "Private controls leaked or were lost.");
            Assert(view.RemainingMilliseconds(1000) == 7600 && view.PhaseId == 9, "Phase/deadline was inferred incorrectly.");
            SyncSameState(view, journal.GetSnapshot(seat));
        }
        return Task.CompletedTask;
    }

    private static Task SyncRecovery()
    {
        MatchJournal journal = new(SyncCore(), () => 0); SyncDraw(journal, 0);
        ClientMatchProjection projection = new(); projection.Install(journal.GetBaseline(1));
        MatchLogBatch batch = journal.GetDelta(1, -1);
        VisibleMatchRecord bad = new(0, batch.Records[0].Action, batch.Records[0].Update.Take(3).ToArray());
        MatchLogBatch corrupt = new() { MatchId = batch.MatchId, RoundId = batch.RoundId, RoundGeneration = batch.RoundGeneration, LatestSequence = batch.LatestSequence, Records = new[] { bad } };
        Assert(!projection.Apply(corrupt) && projection.NeedsCheckpoint && projection.Current.Sequence == -1, "Malformed update partially installed.");
        Assert(projection.Install(journal.GetSnapshot(1)) && !projection.NeedsCheckpoint, "Checkpoint did not recover client.");
        SyncSameState(projection.Current, journal.GetSnapshot(1));
        projection.Current.State.PlayerList[1].PlayerHandTile.HandTileList.Clear();
        journal.RecordAppliedAction(null);
        Assert(!projection.Apply(journal.GetDelta(1, 0)) && projection.NeedsCheckpoint, "Local corruption was silently accepted.");
        projection.Install(journal.GetSnapshot(1)); SyncSameState(projection.Current, journal.GetSnapshot(1));
        return Task.CompletedTask;
    }

    private static Task SyncRoundIsolation()
    {
        MatchJournal journal = new(SyncCore(), () => 0); SyncDraw(journal, 0);
        VisibleMatchState old = journal.GetSnapshot(0); MatchLogBatch oldBatch = journal.GetDelta(0, -1);
        ClientMatchProjection projection = new(); projection.Install(old);
        journal.BeginRound(SyncCore());
        Assert(projection.Install(journal.GetSnapshot(0)), "New round baseline rejected.");
        Assert(!projection.Install(old) && !projection.Apply(oldBatch), "Old round rolled back current state.");
        Assert(projection.Current.RoundGeneration == 2 && projection.Current.Sequence == -1 && projection.Current.RoundId != old.RoundId, "Round identity did not reset independently.");
        return Task.CompletedTask;
    }

    private static Task SyncVariableLists()
    {
        MainMatchControl core = SyncCore();
        core.DiscardTileList[0] = Enumerable.Range(0, 30).Select(index => new Tile(Color.Wans, index % 9 + 1)).ToList();
        MatchJournal journal = new(core, () => 0);
        VisibleMatchState decoded = MatchSyncCodec.DecodeSnapshot(MatchSyncCodec.EncodeSnapshot(journal.GetSnapshot(0)));
        Assert(decoded.State.DiscardTileList[0].Count == 30, "River was truncated at the old fixed size.");
        core.PlayerList[1].PlayerHandTile.HandTileList = new() { new(Color.Tungs, 5), new(Color.Tungs, 5, true) };
        journal = new(core, () => 0);
        PlayerActionData pon = new(1, 0, new() { new(Color.Tungs, 5), new(Color.Tungs, 5, true) }, new(Color.Tungs, 5), PlayerAction.Peng);
        core.PlayerList[1].PlayerHandTile.HandTileList.Clear();
        core.PlayerOpenSetList[1].Add(new Group(GroupType.OpenTriple, pon.TargetTile.Color, pon.Tiles, pon.TargetPlayerNumber, pon.TargetTile));
        journal.RecordAppliedAction(pon);
        ClientMatchProjection projection = new(); projection.Install(journal.GetBaseline(1));
        Assert(projection.Apply(MatchSyncCodec.DecodeBatch(MatchSyncCodec.EncodeBatch(journal.GetDelta(1, -1)))), "Meld delta failed.");
        Assert(projection.Current.State.PlayerOpenSetList[1].Single().Tiles.Count == 3, "Called tile duplicated on replay.");
        SyncSameState(projection.Current, journal.GetSnapshot(1));
        return Task.CompletedTask;
    }

    private static Task SyncRetention()
    {
        MatchJournal journal = new(SyncCore(), () => 0, retentionWindow: 2);
        for (int index = 0; index < 5; index++) { journal.PhaseId++; journal.RecordAppliedAction(null); }
        Assert(journal.GetDelta(0, -1) == null && journal.GetDelta(0, 2).Records.Count == 2, "History window boundary is wrong.");
        ClientMatchProjection projection = new(); projection.Install(journal.GetSnapshot(0));
        Assert(projection.Current.Sequence == 4 && projection.Apply(journal.GetDelta(0, 4)), "Current empty delta failed.");
        return Task.CompletedTask;
    }

    private static async Task<LanRoomClient> SyncStart(Fixture fixture)
    {
        LanRoomClient host = await fixture.Join(true);
        await fixture.Join(); await fixture.Join(); await fixture.Join();
        foreach (LanRoomClient client in fixture.Clients) { client.SetReady(true); }
        await Until(() => host.Snapshot.CanStart, fixture.Pump); host.StartMatch();
        await Until(() => fixture.Clients.All(client => client.Match.Current != null), fixture.Pump);
        return host;
    }

    private static async Task SyncTcpInitialization()
    {
        Fixture fixture = new();
        try
        {
            await SyncStart(fixture);
            MatchJournal journal = fixture.Server.Match;
            foreach (LanRoomClient client in fixture.Clients)
            {
                VisibleMatchState view = client.Match.Current;
                int seat = client.Snapshot.Members.Single(member => member.PlayerId == client.PlayerId).Seat;
                Assert(view.SelfSeat == seat && view.Sequence == 0 && view.State.RemainTileCount == 69, "Initial network state was not installed.");
                Assert(view.HandCounts[0] == 14 && view.HandCounts.Skip(1).All(count => count == 13), "Initial dealer draw counts are wrong.");
                SyncSameState(view, journal.GetSnapshot(seat));
                Assert(seat == 0 ? client.RemainingActionMilliseconds > 0 && client.RemainingActionMilliseconds <= 25000
                    : client.RemainingActionMilliseconds == 0, "Initial private deadline is wrong.");
            }
            Assert(fixture.Server.MatchSnapshotsSent == 4, "Initial checkpoint repeatedly sent without state changes.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task SyncTcpUpdates()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await SyncStart(fixture);
            MatchJournal journal = fixture.Server.Match;
            int initialSnapshots = fixture.Server.MatchSnapshotsSent;
            SyncDiscard(journal, 0); SyncDraw(journal, 1);
            await Until(() => fixture.Clients.All(client => client.Match.Current.Sequence == journal.LatestSequence), fixture.Pump);
            Assert(fixture.Server.MatchSnapshotsSent == initialSnapshots && fixture.Server.MatchLogBatchesSent >= 4, "Normal updates used full checkpoints.");
            foreach (LanRoomClient client in fixture.Clients) { SyncSameState(client.Match.Current, journal.GetSnapshot(client.Match.Current.SelfSeat)); }
            int changes = 0; host.MatchChanged += _ => changes++;
            host.RequestMatchSync();
            await Until(() => changes > 0, fixture.Pump);
            host.Match.Current.State.PlayerList[host.Match.Current.SelfSeat].PlayerHandTile.HandTileList.Clear();
            host.RequestMatchSync(true);
            await Until(() => fixture.Server.MatchSnapshotsSent > initialSnapshots
                && host.Match.Current.State.PlayerList[host.Match.Current.SelfSeat].PlayerHandTile.HandTileList.Count == 13, fixture.Pump);
            SyncSameState(host.Match.Current, journal.GetSnapshot(host.Match.Current.SelfSeat));
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task SyncTcpReconnect()
    {
        Fixture fixture = new();
        try
        {
            await SyncStart(fixture);
            LanRoomClient guest = fixture.Clients[1]; int seat = guest.Match.Current.SelfSeat;
            Guid identity = guest.PlayerId; long deadline = guest.Match.Current.DeadlineMilliseconds;
            await guest.CloseAsync(); await Until(() => fixture.Server.ConnectionCount == 3, fixture.Pump);
            fixture.Server.Match.FuritenFlags[seat] = 4; fixture.Server.Match.HasResponded[seat] = true;
            int bank = fixture.Server.Match.GetSnapshot(seat).ThinkingMilliseconds;
            fixture.Server.Match.RecordAppliedAction(null);
            await guest.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "renamed");
            await Until(() => guest.IsJoined && guest.Match.Current.Sequence == fixture.Server.Match.LatestSequence, fixture.Pump);
            Assert(guest.PlayerId == identity && guest.Match.Current.SelfSeat == seat && guest.Match.Current.FuritenFlags == 4
                && guest.Match.Current.HasResponded && guest.Match.Current.ThinkingMilliseconds <= bank
                && guest.Match.Current.ThinkingMilliseconds >= bank - 1000, "Private reconnect state was lost.");
            Assert(guest.Match.Current.DeadlineMilliseconds == deadline, "Reconnect reset the action deadline.");
            SyncSameState(guest.Match.Current, fixture.Server.Match.GetSnapshot(seat));
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task SyncClockMonotonic()
    {
        Fixture fixture = new();
        try
        {
            await SyncStart(fixture);
            LanRoomClient host = fixture.Clients.Single(client => client.Match.Current.SelfSeat == 0);
            long before = host.RemainingActionMilliseconds;
            await Task.Delay(120);
            int changes = 0; host.MatchChanged += _ => changes++;
            host.RequestMatchSync();
            await Until(() => changes > 0, fixture.Pump);
            Assert(host.RemainingActionMilliseconds <= before - 80, "Old log clock refunded elapsed time.");
            Assert(new VisibleMatchState { DeadlineMilliseconds = 0, ServerMilliseconds = long.MaxValue }.RemainingMilliseconds(long.MaxValue) == 0, "Expired deadline overflowed.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task SyncTcpGap()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await SyncStart(fixture);
            int checkpoints = fixture.Server.MatchSnapshotsSent;
            host.Match.Current.Sequence = -1;
            SyncDiscard(fixture.Server.Match, 0);
            await Until(() => host.Match.Current.Sequence == fixture.Server.Match.LatestSequence, fixture.Pump);
            Assert(fixture.Server.MatchSnapshotsSent == checkpoints, "A repairable log gap unnecessarily installed a checkpoint.");
            SyncSameState(host.Match.Current, fixture.Server.Match.GetSnapshot(host.Match.Current.SelfSeat));
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task SyncTcpFutureSequence()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await SyncStart(fixture);
            host.Match.Current.Sequence = 99999;
            host.RequestMatchSync(true);
            await Until(() => host.Match.Current.Sequence == fixture.Server.Match.LatestSequence, fixture.Pump);
            SyncSameState(host.Match.Current, fixture.Server.Match.GetSnapshot(host.Match.Current.SelfSeat));
        }
        finally { await fixture.Cleanup(); }
    }

    private static Task SyncSourceIsolation()
    {
        MainMatchControl core = SyncCore(); MatchJournal journal = new(core, () => 0);
        PlayerActionData draw = SyncDraw(journal, 0); Tile drawn = draw.TargetTile;
        draw.TargetTile = new(Color.Honor, 7); draw.Tiles = new() { new(Color.Honor, 7) };
        VisibleMatchRecord published = journal.GetDelta(0, -1).Records[0];
        Assert(Tile.ExactlyEquals(published.Action.TargetTile, drawn), "Caller rewrote the stored action.");
        byte[] copy = published.Update; copy[0] ^= 0xFF;
        Assert(!copy.SequenceEqual(published.Update), "Update bytes were shared with callers.");
        VisibleMatchState snapshot = journal.GetSnapshot(0); snapshot.State.PlayerList[0].PlayerHandTile.HandTileList.Clear();
        Assert(journal.GetSnapshot(0).State.PlayerList[0].PlayerHandTile.HandTileList.Count == 13, "Checkpoint hand shared mutable lists.");
        core.MatchSettingData.HaveRedDora = false; journal.RecordAppliedAction(null);
        Assert(journal.GetSnapshot(0).State.MatchSettingData.HaveRedDora, "Round rule settings were not frozen.");
        return Task.CompletedTask;
    }

    private static Task SyncInvalidSource()
    {
        MatchJournal journal = new(SyncCore(), () => 0);
        Throws<InvalidDataException>(() => journal.RecordAppliedAction(new PlayerActionData(PlayerAction.Take) { SelfNumber = 256 }));
        Throws<InvalidDataException>(() => journal.RecordAppliedAction(new PlayerActionData(PlayerAction.Take) { TargetPlayerNumber = 256 }));
        Assert(journal.LatestSequence == -1, "Rejected metadata advanced the journal.");
        return Task.CompletedTask;
    }
}
