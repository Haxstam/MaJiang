using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using static MaJiangLib.GlobalFunction;

internal static partial class Program
{
    private static int Main()
    {
        (string Name, Action Run)[] checks =
        {
            ("Seat assignment belongs to the room", SeatsArePreserved),
            ("Four-player dealing conserves 136 tiles", DealingConservesTiles),
            ("Seeded matches can be reproduced", SeededMatchesAreReproducible),
            ("Red-dora configuration is respected", RedDoraConfigurationIsRespected),
            ("Malformed match setup is rejected", InvalidSetupIsRejected),
            ("Hand discard keeps the drawn tile", HandDiscardKeepsDrawnTile),
            ("Discard selects the specified red tile", DiscardPreservesRedIdentity),
            ("A stale discard does not consume the current turn", InvalidDiscardDoesNotChangeState),
            ("A complete draw/discard wall conserves tiles", CompleteWallConservesTiles),
            ("Rinshan consumes four reserved tiles and shortens live wall", RinshanPreservesDeadWall),
            ("New honba clears private hand and action state", NewHonbaClearsState),
            ("Noten dealer passes with honba incremented", NotenDealerPreservesHonba),
            ("Red-tile display and dictionary lookup are stable", TileDisplayAndEqualityAreStable),
            ("Meld construction does not rewrite an action", MeldDoesNotMutateAction),
            ("Meld serialization does not duplicate called tile", MeldRoundTrip),
            ("Meld equality accepts null and obeys the hash contract", MeldEquality),
            ("Hand cloning preserves drawn tile", ClonePreservesDrawnTile),
            ("Primitive lists preserve zero, negative and false values", PrimitiveListRoundTrip),
            ("Public-state round trip preserves four-player flags", PublicStateRoundTrip),
            ("Action validation checks concrete tiles without advancing state", ActionValidationIsPure),
            ("Three standard hand families remain detectable", TenpaiFamilies),
            ("Sorted presentation preserves red identity and source indices", SortedPresentation),
            ("Chi uses the two ordered concealed tiles and hides kan", DisplayChi),
            ("Pon puts own red five first and preserves called red", DisplayPon),
            ("Open kan puts a claimed red on the upper card", DisplayOpenKan),
            ("Added kan retains called red and prioritizes own red", DisplayAddedKan),
            ("Closed kan uses top group, outer backs and upper red", DisplayClosedKan),
            ("Claim directions are relative to each owner seat", DisplayDirections),
            ("Normal discards pack edge to edge in six columns", RiverNormalPacking),
            ("Riichi in every column expands only its row without gaps", RiverRiichiPacking),
            ("Multiple sideways rows stay independent and empty rivers work", RiverIndependentRows),
            ("Fixed hand strips keep drawn spacing and right-aligned melds", HandStripEdges),
            ("Calling four with 234506 preserves five exact chi combinations", FiveExactChiChoices),
            ("A full turn cycle preserves thirteen concealed tiles plus one private draw", PrivateDrawAndTurnCycle),
            ("All dealer and observer seats preserve counterclockwise wind order", CounterclockwiseSeats),
            ("Hover highlights only visible river and meld tiles, including red fives", HoverVisibility),
            ("Turn lights retain discard response and stop at round end", TurnLightStages),
            ("Changing the controlled hand preserves fixed public seats", FixedPublicSeats),
            ("Tenpai rows use guaranteed ron han and the chosen riichi declaration", TenpaiRonHan),
            ("Discard forecasts recalculate discard furiten and retain missed-win flags", TenpaiDiscardFuriten),
            ("Tenpai remaining counts deduplicate melds and their source rivers", TenpaiRemainingTiles),
            ("Riichi declaration menus expose only wait-producing discard faces", TenpaiRiichiChoices),
            ("Dora sweeps use face-up red tiles and visible indicator successors", DoraSweepTiles),
            ("Discard frames follow the live response tile and ignore skip-only menus", DiscardFrames)
        };
        int failures = 0;
        foreach (var check in checks)
        {
            try
            {
                check.Run();
                Console.WriteLine($"PASS {check.Name}");
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine($"FAIL {check.Name}: {exception}");
            }
        }
        Console.WriteLine($"{checks.Length - failures}/{checks.Length} core checks passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void SortedPresentation()
    {
        List<Tile> tiles = "7z 3s 0p 1w 5p 1w 2z".Split(' ').Select(value => Tile.BytesTo(value)).ToList();
        Tile[] original = tiles.ToArray();
        int[] indices = MaJiang.Presentation.MahjongHandOrder.SortedIndices(tiles);
        if (!indices.SequenceEqual(new[] { 3, 5, 4, 2, 1, 6, 0 })) { throw new InvalidOperationException("Unexpected suit, red or duplicate ordering."); }
        for (int i = 0; i < tiles.Count; i++)
        {
            if (!ReferenceEquals(tiles[i], original[i])) { throw new InvalidOperationException("Sorting mutated the received hand."); }
        }
        if (!tiles[indices[3]].IsRedDora || tiles[indices[2]].IsRedDora) { throw new InvalidOperationException("Red and ordinary five click identity lost."); }
        if (MaJiang.Presentation.MahjongHandOrder.SortedIndices(Array.Empty<Tile>()).Length != 0) { throw new InvalidOperationException("Empty hand failed."); }
    }

    private static MainMatchControl NewMatch(bool redDora = true)
    {
        MainMatchControl match = new();
        MatchSettingData settings = new(MatchType.FourMahjongSouth) { HaveRedDora = redDora };
        match.ServerFourMatchInit(settings, Profiles(), new Xoshiro256StarStar(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
        return match;
    }

    private static UserProfile[] Profiles() => new[] { new UserProfile("A"), new UserProfile("B"), new UserProfile("C"), new UserProfile("D") };

    private static string Face(Tile tile) => $"{(int)tile.Color}:{tile.Number}:{tile.IsRedDora}";

    private static IEnumerable<Tile> AccountedTiles(MainMatchControl match)
    {
        return match.MainTileList.Skip(match.CurrentTileIndex)
            .Concat(match.PrimeTileList.Take(10))
            .Concat(match.PrimeTileList.Skip(10 + match.PrimeTileIndex))
            .Concat(match.PlayerList.SelectMany(player => player.PlayerHandTile.HandTileList))
            .Concat(match.PlayerList.Select(player => player.PlayerHandTile.SingleTile).Where(tile => tile != null))
            .Concat(match.DiscardTileList.SelectMany(river => river));
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void MeldEquality()
    {
        Group first = new(GroupType.OpenTriple, Color.Wans, new() { new(Color.Wans, 5), new(Color.Wans, 5) }, 1, new(Color.Wans, 5));
        Group second = new(GroupType.OpenTriple, Color.Wans, new() { new(Color.Wans, 5), new(Color.Wans, 5, true) }, 2, new(Color.Wans, 5));
        Assert(first != null && !(first == null) && first.Equals(second) && first == second && !(first != second)
            && first.GetHashCode() == second.GetHashCode(), "Meld null/equality/hash behavior disagrees.");
        Assert(new List<Group> { first }.IndexOf(first) == 0 && !first.Equals(null), "Meld lookup throws or matches null.");
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private static void SeatsArePreserved()
    {
        MainMatchControl match = NewMatch();
        for (int seat = 0; seat < 4; seat++)
        {
            Assert(match.PlayerList[seat].PlayerNumber == seat && match.PlayerList[seat].PlayerHandTile.PlayerNumber == seat, "Seat mismatch.");
            Assert(match.PlayerList[seat].PlayerProfile.Name == Profiles()[seat].Name, "The core reshuffled the room's seats.");
        }
    }

    private static void DealingConservesTiles()
    {
        MainMatchControl match = NewMatch();
        Assert(match.PlayerList.All(player => player.PlayerHandTile.HandTileList.Count == 13), "Each player needs 13 concealed tiles.");
        Assert(match.MainTileList.Count == 70 && match.RemainTileCount == 70 && match.PrimeTileList.Count == 14, "Expected 70 live and 14 dead-wall tiles.");
        List<Tile> tiles = AccountedTiles(match).ToList();
        Assert(tiles.Count == 136 && tiles.GroupBy(tile => (tile.Color, tile.Number)).All(group => group.Count() == 4), "Wall composition changed.");
        Assert(tiles.Count(tile => tile.IsRedDora) == 3, "Expected three red fives.");
    }

    private static void SeededMatchesAreReproducible()
    {
        Assert(AccountedTiles(NewMatch()).Select(Face).SequenceEqual(AccountedTiles(NewMatch()).Select(Face)), "Same seed produced different initial state.");
    }

    private static void RedDoraConfigurationIsRespected()
    {
        Assert(AccountedTiles(NewMatch(false)).All(tile => !tile.IsRedDora), "Disabled red dora still appeared.");
    }

    private static void InvalidSetupIsRejected()
    {
        MainMatchControl match = new();
        Throws<ArgumentException>(() => match.ServerFourMatchInit(new(MatchType.FourMahjongSouth), Profiles().Take(3).ToArray()));
        Throws<ArgumentException>(() => match.ServerFourMatchInit(new(MatchType.ThreeMahjongSouth), Profiles()));
        UserProfile[] profiles = Profiles();
        profiles[2] = null;
        Throws<ArgumentException>(() => match.ServerFourMatchInit(new(MatchType.FourMahjongSouth), profiles));
        Throws<ArgumentNullException>(() => match.ServerFourMatchInit(null, Profiles()));
    }

    private static AuthoritativeMatchMachine Machine(MainMatchControl match)
    {
        return new AuthoritativeMatchMachine(new MatchJournal(match, () => 0), () => 0);
    }

    private static MatchCommand Command(AuthoritativeMatchMachine machine, PlayerActionData action) => new()
    {
        MatchId = machine.Journal.MatchId, RoundId = machine.Journal.RoundId, RoundGeneration = machine.Journal.RoundGeneration,
        PhaseId = machine.Journal.PhaseId, CommandId = Guid.NewGuid(), Action = action
    };

    private static void Submit(AuthoritativeMatchMachine machine, PlayerActionData action)
    {
        Assert(machine.Submit(action.SelfNumber, Command(machine, action)).Accepted, "Shared query action was rejected.");
    }

    private static void HandDiscardKeepsDrawnTile()
    {
        MainMatchControl match = NewMatch();
        HandTile hand = match.PlayerList[0].PlayerHandTile;
        AuthoritativeMatchMachine machine = Machine(match);
        Tile drawn = hand.SingleTile;
        Tile discarded = hand.HandTileList[0];
        Submit(machine, machine.GetActions(0).First(action => action.PlayerAction == PlayerAction.HandCut && Tile.ExactlyEquals(action.TargetTile, discarded)));
        Assert(hand.HandTileList.Count == 13 && hand.SingleTile == null, "Hand discard lost a tile.");
        Assert(hand.HandTileList.Any(tile => ReferenceEquals(tile, drawn)), "Drawn tile did not enter the concealed hand.");
        Assert(ReferenceEquals(match.DiscardTileList[0].Single(), discarded) && AccountedTiles(match).Count() == 136, "Discard/wall accounting changed.");
    }

    private static void DiscardPreservesRedIdentity()
    {
        MainMatchControl match = NewMatch();
        HandTile hand = match.PlayerList[0].PlayerHandTile;
        Tile normal = new(Color.Wans, 5);
        Tile red = new(Color.Wans, 5, true);
        hand.HandTileList = new() { normal, red, new(Color.Tungs, 1), new(Color.Tungs, 2), new(Color.Tungs, 3), new(Color.Tungs, 4), new(Color.Tungs, 6),
            new(Color.Bamboo, 1), new(Color.Bamboo, 2), new(Color.Bamboo, 3), new(Color.Bamboo, 7), new(Color.Honor, 5), new(Color.Honor, 6) };
        AuthoritativeMatchMachine machine = Machine(match);
        Submit(machine, machine.GetActions(0).First(action => action.PlayerAction == PlayerAction.HandCut && action.TargetTile.IsRedDora));
        Assert(hand.HandTileList.Any(tile => Tile.ExactlyEquals(tile, normal)) && match.DiscardTileList[0].Single().IsRedDora, "Wrong five was discarded.");
    }

    private static void InvalidDiscardDoesNotChangeState()
    {
        MainMatchControl match = NewMatch();
        HandTile hand = match.PlayerList[0].PlayerHandTile;
        AuthoritativeMatchMachine machine = Machine(match);
        List<Tile> before = hand.HandTileList.ToList();
        MatchCommand stale = Command(machine, machine.GetActions(0).First(action => action.PlayerAction == PlayerAction.TakeCut)); stale.PhaseId--;
        Assert(machine.Submit(0, stale).Reason == "StalePhase" && before.SequenceEqual(hand.HandTileList) && match.DiscardTileList[0].Count == 0,
            "Stale discard partially changed current state.");
    }

    private static void CompleteWallConservesTiles()
    {
        MainMatchControl match = NewMatch();
        string[] initial = AccountedTiles(match).Select(Face).OrderBy(face => face).ToArray();
        AuthoritativeMatchMachine machine = Machine(match);
        int steps = 0;
        while (machine.Stage != MatchOperationStage.RoundEnded && steps++ < 400)
        {
            PlayerActionData choice = Enumerable.Range(0, 4).SelectMany(machine.GetActions).FirstOrDefault(action => action.PlayerAction == PlayerAction.Skip);
            if (choice == null)
            {
                var actions = machine.GetActions(match.CurrentPlayerIndex);
                choice = steps % 2 == 0 ? actions.FirstOrDefault(action => action.PlayerAction == PlayerAction.HandCut) : null;
                choice ??= actions.First(action => action.PlayerAction == PlayerAction.TakeCut);
            }
            Submit(machine, choice);
            Assert(initial.SequenceEqual(AccountedTiles(match).Select(Face).OrderBy(face => face)), $"Tile inventory changed after operation {steps}.");
        }
        Assert(machine.Journal.Outcome == RoundOutcomeKind.ExhaustiveDraw && match.RemainTileCount == 0
            && match.PlayerList.All(player => player.PlayerHandTile.HandTileList.Count == 13), "Wall exhausted with wrong counts.");
        Throws<InvalidOperationException>(() => match.GetNextTile(false));
    }

    private static void RinshanPreservesDeadWall()
    {
        MainMatchControl match = NewMatch();
        Tile[] expected = match.PrimeTileList.Skip(10).Take(4).ToArray();
        string[] initial = AccountedTiles(match).Select(Face).OrderBy(face => face).ToArray();
        List<Tile> taken = new();
        for (int index = 0; index < 4; index++)
        {
            Tile tile = match.GetNextTile(true); taken.Add(tile);
            Assert(ReferenceEquals(tile, expected[index]), "Indicator used as a rinshan tile.");
            Assert(initial.SequenceEqual(AccountedTiles(match).Concat(taken).Select(Face).OrderBy(face => face)), "Rinshan changed tile inventory.");
            Assert(match.MainTileList.Count == 69 - index && match.RemainTileCount == 69 - index, "Live-wall tail was not reserved.");
        }
        Throws<InvalidOperationException>(() => match.GetNextTile(true));
    }

    private static void NewHonbaClearsState()
    {
        MainMatchControl match = NewMatch();
        Machine(match);
        match.PlayerList[0].PlayerHandTile.OpenTileList.Add(new(GroupType.ClosedKan, Color.Wans, new() { new(Color.Wans, 1) }));
        match.IsRiichi[0] = true; match.HaveIppatsu[0] = true; match.KangCount = 1;
        match.ServerFourHonbaInit();
        Assert(match.PlayerList.All(player => player.PlayerHandTile.SingleTile == null && player.PlayerHandTile.OpenTileList.Count == 0 && player.PlayerHandTile.HandTileList.Count == 13), "New honba kept old private state.");
        Assert(!match.IsRiichi[0] && !match.HaveIppatsu[0] && match.KangCount == 0, "New honba kept old action flags.");
    }

    private static void NotenDealerPreservesHonba()
    {
        MainMatchControl match = NewMatch();
        match.Honba = 2;
        match.RiichiStickCount = 3;
        for (int seat = 0; seat < 4; seat++)
        {
            match.PlayerList[seat].PlayerHandTile.HandTileList = "1w 1w 1w 2w 2w 2w 4p 4p 4p 7s 7s 5z 6z".Split(' ').Select(tile => Tile.BytesTo(tile)).ToList();
            match.DiscardTileList[seat].Add(new(Color.Tungs, 2));
        }
        RoundSettlementResult result = RoundSettlement.Calculate(match, RoundOutcomeKind.ExhaustiveDraw, 0, 3, null);
        Assert(result.NextHonba == 3 && result.NextRound == 2 && result.NextDealer == 1 && result.SticksAfter == 3, "Noten draw reset honba or supply.");
    }

    private static void TileDisplayAndEqualityAreStable()
    {
        Tile red = new(Color.Wans, 5, true);
        byte[] before = red.GetBytes();
        Assert(red.ToString() == "0w" && red.Number == 5 && before.SequenceEqual(red.GetBytes()), "Display mutated tile data.");
        Dictionary<Tile, int> waits = new() { [red] = 1 };
        Assert(waits.ContainsKey(new(Color.Wans, 5)), "Shape equality and dictionary hash disagree.");
        Assert(red.CompareTo(new(Color.Wans, 5, true)) == 0 && !Tile.ExactlyEquals(red, new(Color.Wans, 5)), "Red identity/comparison changed.");
    }

    private static void MeldDoesNotMutateAction()
    {
        List<Tile> selected = new() { new(Color.Wans, 1), new(Color.Wans, 2) };
        PlayerActionData action = new(1, 0, selected, new(Color.Wans, 3), PlayerAction.Chi);
        byte[] before = action.GetBytes();
        Group group = new(GroupType.OpenStraight, action.TargetTile.Color, action.Tiles, action.TargetPlayerNumber, action.TargetTile);
        Assert(selected.Count == 2 && before.SequenceEqual(action.GetBytes()) && group.Tiles.Count == 3, "Meld construction rewrote the recorded command.");
    }

    private static void MeldRoundTrip()
    {
        Group meld = new(GroupType.OpenTriple, Color.Tungs, new() { new(Color.Tungs, 5), new(Color.Tungs, 5, true) }, 3, new(Color.Tungs, 5));
        byte[] bytes = meld.GetBytes();
        byte[] packet = new byte[bytes.Length + 9];
        Array.Copy(bytes, 0, packet, 9, bytes.Length);
        Group decoded = Group.StaticBytesTo(packet, 9);
        Assert(decoded.Tiles.Count == 3 && decoded.FuluSource == 3 && bytes.SequenceEqual(decoded.GetBytes()), "Meld decoding appended the called tile twice.");
    }

    private static void ClonePreservesDrawnTile()
    {
        HandTile hand = new(0) { SingleTile = new(Color.Bamboo, 4), HandTileList = new() { new(Color.Bamboo, 3) } };
        HandTile clone = (HandTile)hand.Clone();
        clone.HandTileList.Clear();
        Assert(Tile.ExactlyEquals(clone.SingleTile, hand.SingleTile) && hand.HandTileList.Count == 1, "Clone lost draw or shared the concealed list.");
    }

    private static void PrimitiveListRoundTrip()
    {
        List<int> points = new() { 0, 256, -1000, 25000 };
        byte[] pointBytes = new byte[19];
        Array.Copy(ListToBytes(points), 0, pointBytes, 3, 16);
        Assert(points.SequenceEqual(BytesToIntList(pointBytes, 3, 4)), "Zero or low-byte-zero terminated integer list.");
        Assert(new[] { false, true, false, true }.SequenceEqual(BytesToBoolList(new byte[] { 99, 0, 1, 0, 1 }, 1, 4)), "False terminated boolean list or stride was incorrect.");
        Assert(BytesToBoolList(new byte[] { 0, 1, 0 }, 0, -1).Count == 3, "Remaining booleans were truncated.");
        Throws<ArgumentException>(() => BytesToIntList(new byte[3], 0, -1));
        Throws<ArgumentOutOfRangeException>(() => BytesToBoolList(new byte[1], 0, 2));
        Throws<ArgumentException>(() => BytesToBoolList(new byte[] { 2 }, 0, 1));
    }

    private static void PublicStateRoundTrip()
    {
        MainMatchControl match = NewMatch();
        match.PlayerPoint = new() { 0, 25600, -1000, 75400 };
        match.IsRiichi = new() { false, true, false, true };
        match.HaveIppatsu = new() { true, false, false, true };
        byte[] bytes = MatchSyncCodec.EncodeSnapshot(new MatchJournal(match, () => 0).GetSnapshot(0));
        MainMatchControl decoded = MatchSyncCodec.DecodeSnapshot(bytes).State;
        Assert(decoded.PlayerPoint.SequenceEqual(match.PlayerPoint) && decoded.IsRiichi.SequenceEqual(match.IsRiichi) && decoded.HaveIppatsu.SequenceEqual(match.HaveIppatsu), "Public state lost values or player slots.");
    }

    private static void ActionValidationIsPure()
    {
        MainMatchControl match = NewMatch();
        PlayerActionData legal = new(1, 0, new() { new(Color.Wans, 1), new(Color.Wans, 2) }, new(Color.Wans, 3), PlayerAction.Chi);
        HandTile hand = match.PlayerList[1].PlayerHandTile;
        hand.HandTileList = "1w 2w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z".Split(' ').Select(tile => Tile.BytesTo(tile)).ToList();
        string[] before = hand.HandTileList.Select(Face).ToArray();
        var actions = MatchActionRules.GetActions(match, hand, new MatchActionContext { Stage = MatchOperationStage.DiscardResponse, SourceSeat = 0, ResponseTile = legal.TargetTile });
        PlayerActionData reversed = new(1, 0, legal.Tiles.AsEnumerable().Reverse().ToList(), legal.TargetTile, PlayerAction.Chi);
        Assert(actions.Any(action => MatchActionRules.Matches(action, reversed)) && before.SequenceEqual(hand.HandTileList.Select(Face)), "Query changed response state or rejected equivalent tile order.");
        Assert(!actions.Any(action => MatchActionRules.Matches(action, new PlayerActionData(PlayerAction.Ron) { SelfNumber = 1 })), "Unknown action accepted.");
        PlayerActionData invalid = new(1, 0, new() { new(Color.Wans, 1), new(Color.Wans, 1) }, legal.TargetTile, PlayerAction.Chi);
        Assert(!actions.Any(action => MatchActionRules.Matches(action, invalid)), "Wrong tile multiplicity accepted.");
        Assert(actions.Any(action => action.PlayerAction == PlayerAction.Skip), "Skip rejected.");
    }

    private static void TenpaiFamilies()
    {
        string[][] hands =
        {
            new[] { "1w", "2w", "3w", "1p", "2p", "3p", "1s", "2s", "3s", "7z", "7z", "7z", "5z" },
            new[] { "1w", "1w", "3w", "3w", "5p", "5p", "7p", "7p", "2s", "2s", "4s", "4s", "6z" },
            new[] { "1w", "9w", "1p", "9p", "1s", "9s", "1z", "2z", "3z", "4z", "5z", "6z", "7z" }
        };
        foreach (string[] tiles in hands)
        {
            HandTile hand = new(0) { HandTileList = tiles.Select(value => Tile.BytesTo(value)).ToList() };
            var waits = HandAnalysis.Waits(hand);
            Assert(waits.Count > 0 && waits.All(tile => tile.Color != Color.Honor || tile.Number <= 7), "A standard tenpai family returned no real waits.");
        }
    }
}
