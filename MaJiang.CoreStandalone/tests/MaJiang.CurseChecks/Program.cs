using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MaJiangLib;

internal static class Program
{
    private const string Inert = "1w 1w 1w 2w 2w 2w 4p 4p 4p 7s 7s 5z 6z";
    private const string Ready = "1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z";
    private static readonly DemonCurse[] DefaultCurses = { DemonCurse.ScatteredInitialHand, DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw, DemonCurse.NoBonusTiles };
    private static List<Tile> Tiles(string value) => value.Split(' ').Select(text => Tile.BytesTo(text)).ToList();
    private static Tile T(string value) => Tile.BytesTo(value);
    private static void Assert(bool value, string message) { if (!value) { throw new InvalidOperationException(message); } }
    private static byte[] Seed(int number = 1) => System.Security.Cryptography.SHA256.HashData(BitConverter.GetBytes(number));
    private static UserProfile[] Profiles() => Enumerable.Range(0, 4).Select(seat => new UserProfile("p" + seat)).ToArray();
    private static MainMatchControl Core(int seed = 1, bool audit = true, DemonCurse[] curses = null, DemonCurseRulesVersion version = DemonCurseRulesVersion.V1)
    {
        MainMatchControl core = new();
        core.ServerFourMatchInit(new(MatchType.FourMahjongSouth), Profiles(), new Xoshiro256StarStar(Seed(seed)), new(curses ?? DefaultCurses, rulesVersion: version, recordAudit: audit));
        return core;
    }
    private static void Hand(MainMatchControl core, int seat, string text = Inert)
    {
        core.PlayerList[seat].PlayerHandTile.HandTileList = Tiles(text);
        core.PlayerList[seat].PlayerHandTile.SingleTile = null;
    }
    private static void Wall(MainMatchControl core, int seat, string live, string dead = "1w")
    {
        core.CurrentPlayerIndex = seat; core.CurrentTileIndex = 0; core.PrimeTileIndex = 0;
        core.MainTileList = Tiles(live); core.RemainTileCount = core.MainTileList.Count;
        core.PrimeTileList = Enumerable.Range(0, 14).Select(_ => T(dead)).ToList(); core.DoraPointerList.Clear();
    }
    private static CurseAuditRecord Last(MainMatchControl core) => core.CurseController.Audit.Last();
    private static bool Scattered(IEnumerable<Tile> source)
    {
        Tile[] hand = source.ToArray();
        return hand.Length == 13 && hand.Select(HandAnalysis.Face).Distinct().Count() == 13 &&
            hand.Where(tile => tile.Color != Color.Honor).GroupBy(tile => tile.Color).All(group =>
            {
                int[] numbers = group.Select(tile => tile.Number).OrderBy(value => value).ToArray();
                return numbers.Zip(numbers.Skip(1), (left, right) => right - left >= 3).All(ok => ok);
            });
    }
    private static string Faces(IEnumerable<Tile> tiles) => string.Join(',', tiles.Select(tile => tile.ToString()).OrderBy(value => value));
    private static List<Tile> Inventory(MainMatchControl core) => core.MainTileList.Skip(core.CurrentTileIndex)
        .Concat(core.PrimeTileList.Take(10)).Concat(core.PrimeTileList.Skip(10 + core.PrimeTileIndex))
        .Concat(core.PlayerList.SelectMany(player => player.PlayerHandTile.HandTileList))
        .Concat(core.PlayerList.Select(player => player.PlayerHandTile.SingleTile).Where(tile => tile != null))
        .Concat(core.DiscardTileList.SelectMany(river => river)).ToList();

    private static int Main()
    {
        (string Name, Action Run)[] tests =
        {
            ("Assignment is an immutable permutation of the four curses", Assignment),
            ("Conditional initial hands conserve all physical tiles across seats and seeds", InitialHands),
            ("Curse one is allocated first regardless of its seat", FirstAllocation),
            ("A curse-four dealer has no initial red or dora including the first draw", BonusDealer),
            ("Kokushi thirteen-sided tenpai satisfies the scattered-hand definition", KokushiAllowed),
            ("Waiting tiles are swapped before the actual draw", WinningDraw),
            ("No-winning-draw does not constrain an unready hand", UnreadyDraw),
            ("Duplicate draw merges normal and red tile faces", DuplicateDraw),
            ("Open melds do not constrain no-duplicate draws", OpenMeldIgnored),
            ("No-bonus draw rejects red and all currently exposed dora", BonusDraw),
            ("Live wall replacement is preferred over dead wall", PreferLive),
            ("Last live tile may swap with fifth indicator slot", FifthSlot),
            ("Published indicators, exposed ura and used rinshan are frozen", FrozenPositions),
            ("Already drawn live prefix is excluded from replacement", ConsumedPrefix),
            ("Hidden reserved tail tiles remain eligible", ReservedTail),
            ("Unavoidable failure is only allowed after the full hidden pool is exhausted", Unavoidable),
            ("Rinshan adjustment precedes the normal dead-wall replenishment", Rinshan),
            ("Initial and kan indicators check concealed, drawn and open tiles", Indicators),
            ("A later indicator can swap within the same hidden dead-wall list", SameDeadList),
            ("Ura constraints apply even when cursed player neither wins nor declares riichi", OtherWinnerUra),
            ("A ron winner's separate winning tile also constrains ura", RonWinningTileUra),
            ("Scoring remains pure while actual settlement prepares ura exactly once", SettlementUra),
            ("Curse four may call another player's red tile", RedCallAllowed),
            ("Curse four may ron an already exposed dora and red tile", BonusRonAllowed),
            ("Audit can be disabled while counters and state isolation remain", AuditAndIsolation),
            ("A full explicitly driven cursed match conserves inventory and fixed assignments", WholeMatch),
            ("V1 preserves the exact historical state fingerprint", LegacyFingerprint),
            ("V2 initial curse-three hands have distinct faces without scattered spacing", UniqueInitialHands),
            ("V2 curse-three dealer has fourteen distinct tiles after the first draw", UniqueDealer),
            ("V2 preserves distinct concealed hands and inventory through a full match", () => WholeMatch(DemonCurseRulesVersion.V2))
        };
        int passed = 0;
        foreach (var test in tests)
        {
            try { test.Run(); Console.WriteLine("PASS " + test.Name); passed++; }
            catch (Exception exception) { Console.Error.WriteLine("FAIL " + test.Name + ": " + exception); }
        }
        Console.WriteLine($"{passed}/{tests.Length} curse/dealing checks passed.");
        return passed == tests.Length ? 0 : 1;
    }

    private static void Assignment()
    {
        DemonCurse[] values = DefaultCurses.ToArray(); DemonCurseOptions options = new(values);
        values[0] = DemonCurse.NoBonusTiles;
        Assert(options.CursesBySeat[0] == DemonCurse.ScatteredInitialHand, "Assignment alias escaped.");
        try { new DemonCurseOptions(new[] { DemonCurse.NoBonusTiles, DemonCurse.NoBonusTiles, DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw }); throw new Exception("Duplicate assignment accepted."); }
        catch (ArgumentException) { }
        Assert(options.RulesVersion == DemonCurseRulesVersion.V1, "Legacy constructor changed its default version.");
        try { new DemonCurseOptions(DefaultCurses, (DemonCurseRulesVersion)99); throw new Exception("Invalid version accepted."); }
        catch (ArgumentOutOfRangeException) { }
    }

    private static void LegacyFingerprint()
    {
        System.Text.StringBuilder states = new();
        for (int seed = 1; seed <= 4; seed++)
        for (int offset = 0; offset < 4; offset++)
        {
            DemonCurse[] curses = Enumerable.Range(0, 4).Select(seat => DefaultCurses[(seat + offset) % 4]).ToArray();
            MahjongEngine legacy = new(new MatchSettingData(MatchType.FourMahjongSouth), Profiles(), Seed(seed), new(curses));
            MahjongEngine explicitV1 = new(new MatchSettingData(MatchType.FourMahjongSouth), Profiles(), Seed(seed), new(curses, DemonCurseRulesVersion.V1));
            string state = JsonSerializer.Serialize(legacy.GetState().State, new JsonSerializerOptions { IncludeFields = true });
            Assert(state == JsonSerializer.Serialize(explicitV1.GetState().State, new JsonSerializerOptions { IncludeFields = true }), "Explicit V1 changed legacy results.");
            states.AppendLine(state);
        }
        string digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(states.ToString())));
        Assert(digest == "3AA9162B3F80B8CCB04D32BB68DDD24678F1D228090F4DF30AAA4C78ACE8D7D3", "V1 changed relative to the pre-V2 binary.");
    }

    private static void UniqueInitialHands()
    {
        bool sawLegacyPair = false, sawAdjacent = false, sawRed = false;
        for (int seed = 1; seed <= 24; seed++)
        for (int offset = 0; offset < 4; offset++)
        {
            DemonCurse[] curses = Enumerable.Range(0, 4).Select(seat => DefaultCurses[(seat + offset) % 4]).ToArray();
            int third = Array.IndexOf(curses, DemonCurse.NoDuplicateDraw);
            MainMatchControl core = Core(seed, curses: curses, version: DemonCurseRulesVersion.V2);
            List<Tile> hand = core.PlayerList[third].PlayerHandTile.HandTileList;
            Assert(hand.Count == 13 && hand.Select(HandAnalysis.Face).Distinct().Count() == 13, "V2 initial hand has duplicate faces (including red/normal).");
            List<Tile> inventory = Inventory(core);
            Assert(inventory.Count == 136 && inventory.Count(tile => tile.IsRedDora) == 3 && Enumerable.Range(0, 34).All(face => inventory.Count(tile => HandAnalysis.Face(tile) == face) == 4), "V2 dealing changed physical inventory.");
            Assert(Scattered(core.PlayerList[Array.IndexOf(curses, DemonCurse.ScatteredInitialHand)].PlayerHandTile.HandTileList), "V2 changed curse one's constraint.");
            Assert(core.PlayerList[Array.IndexOf(curses, DemonCurse.NoBonusTiles)].PlayerHandTile.HandTileList.All(tile => !tile.IsRedDora), "V2 changed curse four's red constraint.");
            Assert(core.CurseController.Statistics[third].PreparedHands == 1 && core.CurseController.Audit.Any(record => record.Seat == third && record.Trigger == CurseTrigger.InitialHand), "V2 initial preparation was not audited.");
            sawAdjacent |= !Scattered(hand); sawRed |= hand.Any(tile => tile.IsRedDora);
            var oldHand = Core(seed, curses: curses).PlayerList[third].PlayerHandTile.HandTileList;
            sawLegacyPair |= oldHand.Select(HandAnalysis.Face).Distinct().Count() < 13;
        }
        Assert(sawLegacyPair && sawAdjacent && sawRed, "Fixtures did not cover legacy pairs, allowed spacing and red tile selection.");
    }

    private static void UniqueDealer()
    {
        DemonCurse[] curses = { DemonCurse.NoDuplicateDraw, DemonCurse.NoBonusTiles, DemonCurse.ScatteredInitialHand, DemonCurse.NoWinningDraw };
        for (int seed = 1; seed <= 12; seed++)
        {
            MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Profiles(), Seed(seed), new(curses, DemonCurseRulesVersion.V2));
            HandTile hand = game.GetState().State.PlayerList[0].PlayerHandTile;
            Assert(hand.SingleTile != null && hand.HandTileList.Concat(new[] { hand.SingleTile }).Select(HandAnalysis.Face).Distinct().Count() == 14, "First draw duplicated a V2 dealer's initial face.");
        }
    }

    private static void InitialHands()
    {
        for (int seed = 1; seed <= 24; seed++)
        {
            for (int offset = 0; offset < 4; offset++)
            {
                DemonCurse[] curses = Enumerable.Range(0, 4).Select(seat => DefaultCurses[(seat + offset) % 4]).ToArray();
                MainMatchControl core = Core(seed, curses: curses); List<Tile> inventory = Inventory(core);
                Assert(inventory.Count == 136 && inventory.Count(tile => tile.IsRedDora) == 3 &&
                    Enumerable.Range(0, 34).All(face => inventory.Count(tile => HandAnalysis.Face(tile) == face) == 4), "Initial physical inventory changed.");
                Assert(core.MainTileList.Count == 70 && core.PrimeTileList.Count == 14 && core.PlayerList.All(player => player.PlayerHandTile.HandTileList.Count == 13), "Initial region counts changed.");
                int first = Array.IndexOf(curses, DemonCurse.ScatteredInitialHand), fourth = Array.IndexOf(curses, DemonCurse.NoBonusTiles);
                Assert(Scattered(core.PlayerList[first].PlayerHandTile.HandTileList), "Initial scattered constraints failed.");
                Assert(core.PlayerList[fourth].PlayerHandTile.HandTileList.All(tile => !tile.IsRedDora), "Red tile dealt to curse four.");
            }
        }
    }

    private static void FirstAllocation()
    {
        MainMatchControl a = Core(39), b = Core(39, curses: new[] { DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw, DemonCurse.NoBonusTiles, DemonCurse.ScatteredInitialHand });
        Assert(Faces(a.PlayerList[0].PlayerHandTile.HandTileList) == Faces(b.PlayerList[3].PlayerHandTile.HandTileList), "Earlier ordinary seats consumed tiles before curse one was prepared.");
        MainMatchControl same = Core(39);
        Assert(Faces(Inventory(a)) == Faces(Inventory(same)) && a.MainTileList.Select(tile => tile.ToString()).SequenceEqual(same.MainTileList.Select(tile => tile.ToString())), "Seeded dealing is not reproducible.");
    }

    private static void KokushiAllowed()
    {
        HandTile hand = new(0) { HandTileList = Tiles("1w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z") };
        Assert(Scattered(hand.HandTileList) && HandAnalysis.Waits(hand).Count == 13, "Scattered definition incorrectly rules out kokushi tenpai.");
    }

    private static void BonusDealer()
    {
        DemonCurse[] assignment = { DemonCurse.NoBonusTiles, DemonCurse.ScatteredInitialHand, DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw };
        for (int seed = 1; seed <= 16; seed++)
        {
            MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Profiles(), Seed(seed), new(assignment));
            MainMatchControl state = game.GetState().State;
            int dora = RiichiScoring.NextDora(HandAnalysis.Face(state.DoraPointerList.Single()));
            Assert(state.PlayerList[0].PlayerHandTile.HandTileList.Concat(new[] { state.PlayerList[0].PlayerHandTile.SingleTile })
                .All(tile => !tile.IsRedDora && HandAnalysis.Face(tile) != dora), "Initial marker or dealer first draw evaded curse four.");
        }
    }

    private static void WinningDraw()
    {
        MainMatchControl core = Core(); Hand(core, 1, Ready); Wall(core, 1, "5z 9p 5z", "5z");
        Assert(core.GetNextTile(false) == T("9p") && Last(core).Adjustment == CurseAdjustment.SwappedLiveWall && core.RemainTileCount == 2, "Winning draw was not replaced in live wall.");
    }

    private static void UnreadyDraw()
    {
        MainMatchControl core = Core(); Hand(core, 1); Wall(core, 1, "1w"); int before = core.CurseController.Audit.Count;
        Assert(HandAnalysis.Waits(core.PlayerList[1].PlayerHandTile).Count == 0 && core.GetNextTile(false) == T("1w") && core.CurseController.Audit.Count == before, "Unready hand was constrained.");
    }

    private static void DuplicateDraw()
    {
        MainMatchControl core = Core(); Hand(core, 2, "1w 1w 1w 2w 2w 2w 4p 4p 4p 7s 7s 5p 6z"); Wall(core, 2, "0p 9s", "0p");
        Assert(core.GetNextTile(false) == T("9s") && Last(core).OriginalTile.IsRedDora, "Red five was not considered a duplicate of ordinary five.");
    }

    private static void OpenMeldIgnored()
    {
        MainMatchControl core = Core(); Hand(core, 2, "1w 2w 3w 2p 3p 4p 6s 7s 8s 5z"); Wall(core, 2, "9s");
        core.PlayerOpenSetList[2].Add(new(GroupType.OpenTriple, Color.Bamboo, Tiles("9s 9s"), 0, T("9s")));
        Assert(core.GetNextTile(false) == T("9s") && Last(core).Adjustment == CurseAdjustment.AlreadyValid, "Open meld incorrectly forbade a draw.");
    }

    private static void BonusDraw()
    {
        MainMatchControl core = Core(); Hand(core, 3); Wall(core, 3, "0w 5p 9s 2w", "0w");
        core.DoraPointerList.AddRange(Tiles("4p 8s"));
        Assert(core.GetNextTile(false) == T("2w"), "Red or current dora was not filtered.");
    }

    private static void PreferLive()
    {
        MainMatchControl core = Core(); Hand(core, 2); Wall(core, 2, "1w 8p"); core.PrimeTileList[8] = T("4z");
        Assert(core.GetNextTile(false) == T("8p") && core.PrimeTileList[8] == T("4z") && Last(core).ReplacementZone == WallZone.LiveWall, "Dead wall used despite a live candidate.");
    }

    private static void FifthSlot()
    {
        MainMatchControl core = Core(); Hand(core, 2); Wall(core, 2, "1w"); core.DoraPointerList.Add(core.PrimeTileList[0]);
        core.PrimeTileList[8] = T("4z"); core.PrimeTileList[9] = T("2z"); core.PrimeTileList[6] = T("3z");
        Assert(core.GetNextTile(false) == T("4z") && Last(core).ReplacementIndex == 8 && core.RemainTileCount == 0, "Last draw did not prefer the fifth indicator slot.");
    }

    private static void FrozenPositions()
    {
        MainMatchControl core = Core(); Hand(core, 2); Hand(core, 3); Wall(core, 2, "1w");
        core.PrimeTileList[8] = T("4z"); core.PrimeTileList[9] = T("2z");
        core.DoraPointerList.AddRange(Enumerable.Range(0, 5).Select(index => core.PrimeTileList[index * 2]));
        core.CurseController.BeforeIndicator(core, 9, true);
        for (int index = 10; index < 14; index++) { core.PrimeTileList[index] = T("3z"); }
        core.PrimeTileIndex = 4;
        Assert(core.GetNextTile(false) == T("1w") && Last(core).Adjustment == CurseAdjustment.Unavoidable &&
            core.PrimeTileList[8] == T("4z") && core.PrimeTileList[9] == T("2z") && core.PrimeTileList[13] == T("3z"), "A frozen or used dead position was changed.");
    }

    private static void ConsumedPrefix()
    {
        MainMatchControl core = Core(); Hand(core, 2); Wall(core, 2, "4z 1w"); core.CurrentTileIndex = 1; core.RemainTileCount = 1;
        Assert(core.GetNextTile(false) == T("1w") && Last(core).Adjustment == CurseAdjustment.Unavoidable && core.MainTileList[0] == T("4z"), "A previously drawn position was reused.");
    }

    private static void ReservedTail()
    {
        MainMatchControl core = Core(); Hand(core, 2); Wall(core, 2, "1w"); core.PrimeTileList.Add(T("4z"));
        Assert(core.GetNextTile(false) == T("4z") && Last(core).ReplacementIndex == 14 && core.PrimeTileList.Count == 15, "Hidden reserved tail excluded or resized.");
    }

    private static void Unavoidable()
    {
        MainMatchControl core = Core(); Hand(core, 1, Ready); Wall(core, 1, "5z", "5z"); core.PrimeTileList[8] = T("4z");
        Assert(core.GetNextTile(false) == T("4z") && Last(core).Adjustment == CurseAdjustment.SwappedDeadWall, "Curse failed before checking dead wall.");
        MainMatchControl blocked = Core(); Hand(blocked, 1, Ready); Wall(blocked, 1, "5z", "5z");
        Assert(blocked.GetNextTile(false) == T("5z") && Last(blocked).Adjustment == CurseAdjustment.Unavoidable && blocked.CurseController.Statistics[1].Unavoidable == 1, "Genuine impossibility was not counted as a failure.");
    }

    private static void Rinshan()
    {
        MainMatchControl core = Core(); Hand(core, 2); Wall(core, 2, "1w 8p");
        Assert(core.GetNextTile(true) == T("8p") && Last(core).Trigger == CurseTrigger.RinshanDraw && Last(core).SourceIndex == 10 &&
            core.MainTileList.Count == 1 && core.RemainTileCount == 1 && core.PrimeTileIndex == 1 && core.PrimeTileList.Count == 15 && core.PrimeTileList[14] == T("1w"),
            "Rinshan swapping broke tail reservation or counts.");
    }

    private static void Indicators()
    {
        MainMatchControl core = Core(); Hand(core, 3); Wall(core, 0, "9p");
        core.PlayerOpenSetList[3].Add(new(GroupType.OpenTriple, Color.Tungs, Tiles("0p 5p"), 1, T("5p")));
        core.PlayerList[3].PlayerHandTile.SingleTile = T("4z"); core.PrimeTileList[0] = T("4p");
        Assert(core.RevealDoraIndicator() == T("9p"), "Initial indicator ignored open dora tiles.");
        core.PrimeTileList[2] = T("3z"); core.MainTileList.Add(T("7p")); core.RemainTileCount++;
        Assert(core.RevealDoraIndicator() == T("7p") && core.PrimeTileList[0] == T("9p"), "Kan indicator ignored the separate draw or changed a public marker.");
    }

    private static void OtherWinnerUra()
    {
        MainMatchControl core = Core(); Hand(core, 3); Wall(core, 0, "8p"); core.DoraPointerList.Add(core.PrimeTileList[0]); core.PrimeTileList[1] = T("7z");
        core.PrepareUraIndicators(1, T("9s"));
        Assert(core.PrimeTileList[1] == T("8p") && Last(core).Trigger == CurseTrigger.UraIndicator && Last(core).Seat == 3 && !core.IsRiichi[3], "Ura ignored a non-winning, non-riichi curse-four player.");
        int count = core.CurseController.Audit.Count; core.PrepareUraIndicators(1, T("9s"));
        Assert(core.CurseController.Audit.Count == count, "Ura was re-revealed or re-adjusted.");
    }

    private static void SameDeadList()
    {
        MainMatchControl core = Core(); Hand(core, 3); Wall(core, 0, "7z", "7z");
        core.PrimeTileList[9] = T("8p");
        core.DoraPointerList.AddRange(Enumerable.Range(0, 4).Select(index => core.PrimeTileList[index * 2]));
        Assert(core.RevealDoraIndicator() == T("8p") && Last(core).SourceIndex == 8 && Last(core).ReplacementIndex == 9 &&
            core.PrimeTileList[9] == T("7z"), "Dead-to-dead swap overwrote one side or failed to skip its own source.");
    }

    private static void RonWinningTileUra()
    {
        MainMatchControl core = Core(); Hand(core, 3, "2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z 5z"); Wall(core, 0, "7p");
        core.DoraPointerList.Add(core.PrimeTileList[0]); core.PrimeTileList[1] = T("9w");
        core.PrepareUraIndicators(8, T("1w"));
        Assert(core.PrimeTileList[1] == T("7p"), "Separate ron winning tile was not considered owned at ura reveal.");
    }

    private static void SettlementUra()
    {
        MainMatchControl core = Core();
        for (int seat = 0; seat < 4; seat++) { Hand(core, seat); }
        Hand(core, 3, "2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z 5z");
        core.FirstCycleIppatsu = false; core.IsRiichi[3] = true; core.MainTileList[0] = T("1w");
        core.DoraPointerList.Add(core.PrimeTileList[0]); core.PrimeTileList[1] = T("9w");
        string before = Faces(core.PrimeTileList) + "/" + core.CurseController.Audit.Count;
        RoundSettlement.Calculate(core, RoundOutcomeKind.Ron, 8, 0, T("1w"));
        Assert(before == Faces(core.PrimeTileList) + "/" + core.CurseController.Audit.Count, "Pure settlement query mutated the hidden wall.");
        MahjongEngine game = new(core);
        game.Submit(0, game.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
        foreach (int seat in game.GetState().PendingSeats.Where(seat => seat != 3).ToArray())
        {
            game.Submit(seat, game.GetAvailableCommands(seat).First(choice => choice.Action.PlayerAction == PlayerAction.Skip));
        }
        MatchCommand ron = game.GetAvailableCommands(3).First(choice => choice.Action.PlayerAction == PlayerAction.Ron);
        game.Submit(3, ron);
        var settlement = game.GetState().Settlement;
        Assert(settlement != null && settlement.Winners.Single().Score.Yaku.All(yaku => yaku.Yaku != YakuType.UraDora) &&
            game.GetCurseAudit().Count(record => record.Trigger == CurseTrigger.UraIndicator) == 1, "Real settlement missed or duplicated ura enforcement.");
        int count = game.GetCurseAudit().Count; game.Submit(3, ron); game.GetState();
        Assert(game.GetCurseAudit().Count == count, "Retry or query changed frozen ura.");
    }

    private static void AuditAndIsolation()
    {
        MainMatchControl core = Core(audit: false); Hand(core, 2); Wall(core, 2, "1w 8p"); core.GetNextTile(false);
        Assert(core.CurseController.Audit.Count == 0 && core.CurseController.Statistics[2].LiveWallSwaps == 1, "Disabled audit also disabled statistics.");
        CurseStatistics copy = core.CurseController.Statistics[2]; copy.LiveWallSwaps = 99;
        Assert(core.CurseController.Statistics[2].LiveWallSwaps == 1, "Statistics alias escaped.");
        MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Profiles(), Seed(), new(DefaultCurses));
        string json = JsonSerializer.Serialize(game.GetState(), new JsonSerializerOptions { IncludeFields = true });
        Assert(!json.Contains("Curse") && game.GetState().State.CurseController == null && game.GetPlayerView(0).State.CurseController == null, "Curse state leaked into public/core snapshots.");
        int count = game.GetCurseAudit().Count; game.GetState(); game.GetPlayerView(0); game.GetCurseStatistics();
        Assert(game.GetCurseAudit().Count == count, "Query triggered curse adjustment.");
    }

    private static void RedCallAllowed()
    {
        MainMatchControl core = Core();
        for (int seat = 0; seat < 4; seat++) { Hand(core, seat); }
        Hand(core, 3, "5p 5p 4w 6w 8w 1p 2p 4p 6p 7s 8s 5z 6z");
        core.MainTileList[0] = T("0p"); core.PrimeTileList[0] = T("8s"); core.RevealDoraIndicator();
        MahjongEngine game = new(core);
        game.Submit(0, game.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
        game.Submit(3, game.GetAvailableCommands(3).First(choice => choice.Action.PlayerAction == PlayerAction.Peng));
        Assert(game.GetState().State.PlayerOpenSetList[3].Single().Tiles.Any(tile => tile.IsRedDora) &&
            game.GetState().OperationStage == MatchOperationStage.DiscardAfterCall, "Red pon was forbidden or red tile erased.");
    }

    private static void BonusRonAllowed()
    {
        MainMatchControl core = Core();
        for (int seat = 0; seat < 4; seat++) { Hand(core, seat); }
        Hand(core, 3, "1w 2w 3w 3p 4p 1s 2s 3s 7z 7z 7z 2z 2z");
        core.MainTileList[0] = T("0p"); core.PrimeTileList[0] = T("4p"); core.RevealDoraIndicator();
        core.FirstCycleIppatsu = false;
        MahjongEngine game = new(core);
        game.Submit(0, game.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
        game.Submit(3, game.GetAvailableCommands(3).First(choice => choice.Action.PlayerAction == PlayerAction.Ron));
        var winner = game.GetState().Settlement.Winners.Single();
        Assert(winner.WinningTile.IsRedDora && winner.Score.Yaku.Any(yaku => yaku.Yaku == YakuType.Dora) &&
            winner.Score.Yaku.Any(yaku => yaku.Yaku == YakuType.AkaDora), "Ron was forbidden or existing dora/red bonus was erased.");
    }

    private static void WholeMatch() => WholeMatch(DemonCurseRulesVersion.V1);
    private static void WholeMatch(DemonCurseRulesVersion version)
    {
        MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Profiles(), Seed(27), new(DefaultCurses, version));
        int rounds = 0, operations = 0;
        while (rounds++ < 100)
        {
            string[] initialMarkers = game.GetState().State.DoraPointerList.Select(tile => tile.ToString()).ToArray();
            if (version == DemonCurseRulesVersion.V2) { Assert(game.GetState().State.PlayerList[2].PlayerHandTile.HandTileList.Select(HandAnalysis.Face).Distinct().Count() == 13, "V2 next round lost unique initial hands."); }
            while (game.GetState().OperationStage != MatchOperationStage.RoundEnded && operations++ < 30000)
            {
                var state = game.GetState(); int seat = state.PendingSeats[0]; var choices = game.GetAvailableCommands(seat);
                if (version == DemonCurseRulesVersion.V2 && seat == 2 && game.GetCurseStatistics()[2].Unavoidable == 0)
                {
                    Assert(choices.All(choice => !new[] { PlayerAction.Tsumo, PlayerAction.Peng, PlayerAction.OpenKan, PlayerAction.AddedKan, PlayerAction.ClosedKan }.Contains(choice.Action.PlayerAction)), "V2 offered a duplicate-dependent action without an unavoidable exception.");
                }
                var feedback = game.Submit(seat, choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                    ?? choices.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
                Assert(feedback.Accepted && !feedback.UsedDefault, "Explicit cursed match input defaulted.");
                MainMatchControl snapshot = game.GetState().State; List<Tile> inventory = Inventory(snapshot);
                if (version == DemonCurseRulesVersion.V2 && game.GetCurseStatistics()[2].Unavoidable == 0)
                {
                    HandTile hand = snapshot.PlayerList[2].PlayerHandTile;
                    Tile[] owned = hand.HandTileList.Concat(hand.SingleTile == null ? Array.Empty<Tile>() : new[] { hand.SingleTile }).ToArray();
                    Assert(owned.Select(HandAnalysis.Face).Distinct().Count() == owned.Length, "V2 gained a duplicate without unavoidable failure.");
                }
                Assert(inventory.Count == 136 && inventory.Count(tile => tile.IsRedDora) == 3 &&
                    Enumerable.Range(0, 34).All(face => inventory.Count(tile => HandAnalysis.Face(tile) == face) == 4), "Dynamic swapping changed tile inventory.");
                Assert(initialMarkers.SequenceEqual(snapshot.DoraPointerList.Select(tile => tile.ToString())), "Dynamic draw changed an exposed indicator.");
            }
            var ended = game.GetState();
            Assert(ended.OperationStage == MatchOperationStage.RoundEnded && ended.State.PlayerPoint.Sum() + ended.State.RiichiStickCount * 1000 == 100000, "Cursed match failed to settle or conserve points.");
            if (ended.MatchEnded)
            {
                Assert(game.GetCurseStatistics()[0].PreparedHands == rounds && game.GetCurseStatistics().Select(item => item.Curse).SequenceEqual(DefaultCurses), "Assignment or round initialization changed mid-match.");
                return;
            }
            Assert(game.StartNextRound().Accepted && Scattered(game.GetState().State.PlayerList[0].PlayerHandTile.HandTileList), "Explicit next round did not reapply curse one.");
        }
        throw new Exception("Cursed match did not finish within bounds.");
    }
}
