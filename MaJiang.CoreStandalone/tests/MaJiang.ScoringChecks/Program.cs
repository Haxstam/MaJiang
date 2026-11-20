using MaJiangLib;
using System;
using System.Collections.Generic;
using System.Linq;
using MatchType = MaJiangLib.MatchType;

internal static class Program
{
    private const string Pinfu = "2w 3w 4w 3p 4p 5p 4s 5s 6s 7s 8s 6p 6p";
    private const string Ready = "1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z";
    private const string Noten = "1w 1w 1w 2w 2w 2w 4p 4p 4p 7s 7s 5z 6z";
    private static int Main()
    {
        (string Name, Action Run)[] checks =
        {
            ("Pinfu ron and tsumo have 30/20 fu and real rounded payments", PinfuScores),
            ("Open tanyao pinfu shape is 30 fu", OpenPinfuShape),
            ("Seven pairs is fixed at 25 fu", SevenPairs),
            ("Double wind pair is four fu relative to actual dealer", DoubleWindFu),
            ("Winning assignment distinguishes edge and two-sided wait", WaitFu),
            ("Ron triplet loses concealed fu and sanankou", RonTriple),
            ("All decompositions compete by final rounded payments", HighestDecomposition),
            ("Ambiguous 222333444p chooses concealed triplets when they pay more", AmbiguousTripletsPayMore),
            ("Ambiguous 222333444p chooses sequences when ron opens a triplet", AmbiguousSequencesPayMore),
            ("Equal han chooses higher fu when its actual payment is higher", EqualHanHigherFuPaysMore),
            ("An ambiguous decomposition must not hide a double yakuman", AmbiguousYakuman),
            ("Dora alone never supplies the required yaku", NoYaku),
            ("Dora, ura and red tiles count only after legal yaku", Dora),
            ("Dora indicators wrap independently for suits, winds and dragons", DoraCycles),
            ("Kokushi thirteen-sided and ordinary are double/single", Kokushi),
            ("Pure and ordinary nine gates are double/single", NineGates),
            ("Concealed tanki, big winds and all honors stack to five yakuman", CompoundYakuman),
            ("Green hand and four kans have their own yakuman", OtherYakuman),
            ("Kazoe caps at one yakuman and discards ordinary limit growth", CountedYakuman),
            ("Limits do not round 4 han 30 fu up to mangan", Limits),
            ("Rinshan excludes haitei while final discard allows houtei", LastTile),
            ("Scoring contexts are independent and never mutate input", Isolation),
            ("Straight yaku use correct closed and open han", StraightYaku),
            ("Outside hands distinguish junchan/chanta and open reduction", OutsideYaku),
            ("Triplet yaku include sanshoku, sanankou, shousangen and kans", TripleYaku),
            ("Honroutou stacks with seven pairs without toitoi", HonroutouPairs),
            ("First uninterrupted draws award heaven/earth yakuman", FirstDrawYakuman),
            ("Open tanyao setting and yaku minimum exclude bonus han", ConfiguredYaku),
            ("Double wind triplet counts both seat and round roles", DoubleWindYaku),
            ("Chankan can be the only yaku of an open hand", ChankanOnly),
            ("Single ron transfers honba and deposits exactly once", SinglePayment),
            ("Double/triple ron award all extras to nearest winner", MultiplePayment),
            ("Noten penalties cover all five tenpai counts", NotenPayments),
            ("Nagashi keeps deposits and follows actual dealer tenpai", Nagashi),
            ("Simultaneous nagashi settles independent tsumo payments", MultipleNagashi),
            ("Pao follows final required meld and survives added kan", PaoTrigger),
            ("Pao ron splits liable yakuman and takes all honba", PaoRon),
            ("Pao tsumo separates liable and other compound yakuman", PaoTsumo),
            ("Big four winds pao covers both yakuman units", BigWindsPao),
            ("Bankruptcy is checked after all payments; zero continues", Bankruptcy),
            ("South four dealer top win/tenpai stops and takes tail deposits", DealerStop),
            ("South four enters west without a target score", WestEntry),
            ("West target stops except multi-ron dealer continuation", WestPriority),
            ("West four can repeat but never advances north", WestFour),
            ("Final equal scores prefer initial seat order", Ranking),
            ("Abortive draws preserve deposits and increment honba", AbortiveDraw)
        };
        int failed = 0;
        foreach (var check in checks)
        {
            try { check.Run(); Console.WriteLine("PASS " + check.Name); }
            catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + check.Name + ": " + error); }
        }
        Console.WriteLine($"{checks.Length - failed}/{checks.Length} scoring checks passed.");
        return failed == 0 ? 0 : 1;
    }

    private static List<Tile> Tiles(string text) => text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(tile => Tile.BytesTo(tile)).ToList();
    private static void Assert(bool condition, string message) { if (!condition) { throw new InvalidOperationException(message); } }
    private static bool Has(ScoredHand hand, YakuType yaku) => hand.Yaku.Any(item => item.Yaku == yaku);
    private static MainMatchControl Core()
    {
        MainMatchControl core = new();
        core.ServerFourMatchInit(new MatchSettingData(MatchType.FourMahjongSouth), new[] { new UserProfile("A"), new UserProfile("B"), new UserProfile("C"), new UserProfile("D") });
        core.FirstCycleIppatsu = false;
        core.DoraPointerList.Clear();
        for (int seat = 0; seat < 4; seat++)
        {
            Hand(core, seat, Noten);
            core.DiscardTileList[seat].Add(Tile.BytesTo("2p"));
        }
        return core;
    }
    private static void Hand(MainMatchControl core, int seat, string text) => core.PlayerList[seat].PlayerHandTile.HandTileList = Tiles(text);
    private static void Open(MainMatchControl core, int seat, Group group) => core.PlayerOpenSetList[seat].Add(group);
    private static Group Pon(string text, int source) => new(GroupType.OpenTriple, Tile.BytesTo(text).Color, Tiles($"{text} {text}"), source, Tile.BytesTo(text));
    private static WinContext Context(string text = Pinfu, string winning = "9s", bool tsumo = false)
    {
        MainMatchControl core = Core(); Hand(core, 1, text);
        return new() { State = core, Hand = core.PlayerList[1].PlayerHandTile, WinningTile = Tile.BytesTo(winning), Tsumo = tsumo };
    }
    private static void PinfuScores()
    {
        ScoredHand ron = RiichiScoring.Evaluate(Context());
        ScoredHand tsumo = RiichiScoring.Evaluate(Context(tsumo: true));
        Assert(ron.Han == 1 && ron.Fu == 30 && ron.BasePoints == 240 && RiichiScoring.WinningPayment(ron.BasePoints, false, false) == 1000, "Pinfu ron wrong.");
        Assert(tsumo.Han == 2 && tsumo.Fu == 20 && tsumo.BasePoints == 320 && RiichiScoring.WinningPayment(tsumo.BasePoints, false, true) == 1500, "Pinfu tsumo wrong.");
    }
    private static void OpenPinfuShape()
    {
        WinContext context = Context("2p 3p 4p 6s 7s 3w 4w 5w 6p 6p", "8s");
        Open((MainMatchControl)context.State, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("4w 5w"), 0, Tile.BytesTo("6w")));
        ScoredHand hand = RiichiScoring.Evaluate(context);
        Assert(hand.Han == 1 && hand.Fu == 30 && Has(hand, YakuType.Tanyao) && !Has(hand, YakuType.Pinfu), "Open pinfu-type score wrong.");
    }
    private static void SevenPairs()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("1w 1w 2w 2w 4p 4p 6p 6p 7s 7s 9s 9s 5z", "5z"));
        Assert(hand.Han == 2 && hand.Fu == 25 && hand.BasePoints == 400, "Seven pairs lost 25 fu.");
    }
    private static void DoubleWindFu()
    {
        MainMatchControl core = Core(); core.CurrentDealerIndex = 2; core.Wind = WindType.South;
        Hand(core, 3, "1w 2w 3w 6p 7p 8p 6s 7s 2z 2z");
        Open(core, 3, new(GroupType.OpenKan, Color.Tungs, Tiles("4p 4p 4p"), 0, Tile.BytesTo("4p")));
        WinContext context = new() { State = core, Hand = core.PlayerList[3].PlayerHandTile, WinningTile = Tile.BytesTo("8s"), Chankan = true };
        Assert(RiichiScoring.Evaluate(context).Fu == 40, "Double wind did not add four fu.");
        core.Wind = WindType.East;
        Assert(RiichiScoring.Evaluate(context).Fu == 30, "Single wind pair added four fu.");
    }
    private static void WaitFu()
    {
        WinContext context = Context("1w 2w 3w 6p 7p 8p 1s 2s 6s 6s", "3s", true);
        MainMatchControl core = (MainMatchControl)context.State;
        Open(core, 1, new(GroupType.OpenKan, Color.Tungs, Tiles("4p 4p 4p"), 0, Tile.BytesTo("4p"))); core.IsKan[1] = true;
        Assert(RiichiScoring.Evaluate(context).Fu == 40, "Edge wait did not add two fu.");
        Hand(core, 1, "1w 2w 3w 6p 7p 8p 2s 3s 6s 6s"); context.WinningTile = Tile.BytesTo("1s");
        Assert(RiichiScoring.Evaluate(context).Fu == 30, "Two-sided wait got edge fu.");
    }
    private static void RonTriple()
    {
        WinContext context = Context("1w 1w 2p 2p 2p 4s 4s 4s 6p 7p 8p 5z 5z", "1w");
        ((MainMatchControl)context.State).IsRiichi[1] = true;
        ScoredHand ron = RiichiScoring.Evaluate(context); context.Tsumo = true; ScoredHand tsumo = RiichiScoring.Evaluate(context);
        Assert(ron.Fu == 50 && !Has(ron, YakuType.Sananko) && tsumo.Fu == 40 && Has(tsumo, YakuType.Sananko), "Ron triplet concealment wrong.");
    }
    private static void HighestDecomposition()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("1w 1w 2w 2w 3w 3w 4w 4w 5w 5w 6w 6w 7w", "7w"));
        Assert(RiichiScoring.WinningPayment(hand.BasePoints, false, false) == 16000,
            "A lower-paying decomposition was selected.");
        Assert((hand.Han == 8 && hand.Fu == 25 && Has(hand, YakuType.Chiitoitsu)) ||
            (hand.Han == 10 && hand.Fu == 30 && Has(hand, YakuType.Ryanpeiko) && Has(hand, YakuType.Pinfu)),
            "The tied result is not a valid seven-pairs or standard decomposition.");
    }

    private static void AmbiguousTripletsPayMore()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("2p 2p 2p 3p 3p 3p 4p 4p 4p 5s 6s 7s 5z", "5z"));
        Assert(hand.Han == 2 && hand.Fu == 50 && Has(hand, YakuType.Sananko) && !Has(hand, YakuType.Iipeikou),
            "The three concealed triplets were missed in favor of three 234p sequences.");
        Assert(RiichiScoring.WinningPayment(hand.BasePoints, false, false) == 3200, "Expected 2 han 50 fu child ron.");
    }

    private static void AmbiguousSequencesPayMore()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("2p 2p 3p 3p 3p 4p 4p 4p 6s 7s 8s 5w 5w", "2p"));
        Assert(hand.Han == 3 && hand.Fu == 30 && Has(hand, YakuType.Tanyao) && Has(hand, YakuType.Pinfu) &&
            Has(hand, YakuType.Iipeikou) && !Has(hand, YakuType.Sananko),
            "The scorer always chose triplets or treated the ron-completed triplet as concealed.");
        Assert(RiichiScoring.WinningPayment(hand.BasePoints, false, false) == 3900, "Expected 3 han 30 fu child ron.");
    }

    private static void EqualHanHigherFuPaysMore()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("2p 2p 2p 3p 3p 3p 4p 4p 4p 6s 7s 5w 5w", "8s"));
        Assert(hand.Han == 3 && hand.Fu == 50 && Has(hand, YakuType.Sananko) && Has(hand, YakuType.Tanyao) && !Has(hand, YakuType.Pinfu),
            "Equal-han 30-fu sequences displaced the 50-fu triplets.");
        Assert(RiichiScoring.WinningPayment(hand.BasePoints, false, false) == 6400, "Expected 3 han 50 fu child ron.");
    }

    private static void AmbiguousYakuman()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("1p 1p 1p 2p 2p 2p 3p 3p 3p 4s 4s 4s 5w", "5w"));
        Assert(hand.Yakuman == 2 && Has(hand, YakuType.SuuankouTanki) &&
            RiichiScoring.WinningPayment(hand.BasePoints, false, false) == 64000,
            "The three 123p sequences hid the four-concealed-triplet single-wait decomposition.");
    }
    private static void NoYaku()
    {
        WinContext context = Context("4w 5w 6w 2p 3p 4p 6s 7s 8s 5p", "5p");
        MainMatchControl core = (MainMatchControl)context.State;
        Open(core, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 0, Tile.BytesTo("3w"))); core.DoraPointerList.Add(Tile.BytesTo("4p"));
        Assert(RiichiScoring.Evaluate(context) == null, "Dora created a legal hand without yaku.");
    }
    private static void Dora()
    {
        WinContext context = Context(Pinfu.Replace("5p", "0p")); MainMatchControl core = (MainMatchControl)context.State;
        core.IsRiichi[1] = true; core.DoraPointerList.Add(Tile.BytesTo("4p")); context.UraIndicators = Tiles("5p");
        ScoredHand hand = RiichiScoring.Evaluate(context);
        Assert(hand.Han == 6 && hand.Fu == 30 && Has(hand, YakuType.AkaDora) && hand.Yaku.Single(item => item.Yaku == YakuType.UraDora).Han == 2, "Bonus tile counts wrong.");
        core.IsRiichi[1] = false;
        Assert(!Has(RiichiScoring.Evaluate(context), YakuType.UraDora), "Non-riichi counted ura.");
    }
    private static void DoraCycles()
    {
        Assert(RiichiScoring.NextDora(8) == 0 && RiichiScoring.NextDora(17) == 9 && RiichiScoring.NextDora(26) == 18
            && RiichiScoring.NextDora(30) == 27 && RiichiScoring.NextDora(33) == 31 && RiichiScoring.NextDora(31) == 32, "Dora cycle crossed a suit or honor family.");
    }
    private static void Kokushi()
    {
        Assert(RiichiScoring.Evaluate(Context("1w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z", "1w")).Yakuman == 2, "Thirteen wait not double.");
        Assert(RiichiScoring.Evaluate(Context("1w 1w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z", "7z")).Yakuman == 1, "Ordinary kokushi doubled.");
    }
    private static void NineGates()
    {
        Assert(RiichiScoring.Evaluate(Context("1w 1w 1w 2w 3w 4w 5w 6w 7w 8w 9w 9w 9w", "5w")).Yakuman == 2, "Pure nine gates not double.");
        Assert(RiichiScoring.Evaluate(Context("1w 1w 1w 2w 3w 4w 5w 5w 6w 7w 9w 9w 9w", "8w")).Yakuman == 1, "Ordinary nine gates doubled.");
    }
    private static void CompoundYakuman()
    {
        ScoredHand hand = RiichiScoring.Evaluate(Context("1z 1z 1z 2z 2z 2z 3z 3z 3z 4z 4z 4z 5z", "5z"));
        Assert(hand.Yakuman == 5 && hand.BasePoints == 40000 && hand.Han == 0, "Actual yakuman did not stack independently.");
    }
    private static void OtherYakuman()
    {
        WinContext green = Context("2s 2s 2s 6s 6s 6s 8s 8s 6z 6z", "8s");
        Open((MainMatchControl)green.State, 1, new(GroupType.OpenStraight, Color.Bamboo, Tiles("2s 3s"), 0, Tile.BytesTo("4s")));
        Assert(Has(RiichiScoring.Evaluate(green), YakuType.Ryuiishoku), "All green missing.");
        WinContext kans = Context("7z", "7z");
        foreach (string tile in new[] { "1w", "2p", "3s", "4w" }) { Open((MainMatchControl)kans.State, 1, new(GroupType.ClosedKan, Tile.BytesTo(tile).Color, Tiles($"{tile} {tile} {tile} {tile}"))); }
        Assert(RiichiScoring.Evaluate(kans).Yakuman == 3, "Four kans and concealed tanki did not combine.");
    }
    private static void CountedYakuman()
    {
        WinContext context = Context("1w 1w 2w 2w 3w 3w 4w 4w 5w 5w 6w 6w 7w", "7w");
        MainMatchControl core = (MainMatchControl)context.State; core.IsRiichi[1] = true; core.HaveIppatsu[1] = true;
        core.DoraPointerList = Tiles("1w 1w 1w 1w 1w");
        ScoredHand hand = RiichiScoring.Evaluate(context);
        Assert(hand.Han >= 13 && hand.BasePoints == 8000 && hand.Yakuman == 0, "Counted yakuman grew past single limit.");
    }
    private static void Limits()
    {
        Assert(RiichiScoring.BasicPoints(4, 30) == 1920 && RiichiScoring.BasicPoints(3, 60) == 1920 && RiichiScoring.BasicPoints(4, 40) == 2000
            && RiichiScoring.BasicPoints(6, 30) == 3000 && RiichiScoring.BasicPoints(8, 30) == 4000 && RiichiScoring.BasicPoints(11, 30) == 6000, "Limit threshold wrong.");
    }
    private static void LastTile()
    {
        WinContext context = Context(tsumo: true); MainMatchControl core = (MainMatchControl)context.State; core.RemainTileCount = 0; core.IsKan[1] = true;
        ScoredHand hand = RiichiScoring.Evaluate(context);
        Assert(Has(hand, YakuType.Rinshankaiho) && !Has(hand, YakuType.HaiteiRaoyui), "Rinshan incorrectly stacked haitei.");
        context.Tsumo = false;
        Assert(Has(RiichiScoring.Evaluate(context), YakuType.HoteiRaoyui), "Last discard lost houtei.");
    }
    private static void Isolation()
    {
        WinContext first = Context(); WinContext second = Context(tsumo: true);
        string before = string.Join(" ", first.Hand.HandTileList);
        RiichiScoring.Evaluate(second); ScoredHand result = RiichiScoring.Evaluate(first);
        Assert(result.Han == 1 && before == string.Join(" ", first.Hand.HandTileList) && first.State.PlayerPoint.All(points => points == 25000), "Scoring leaked state or mutated input.");
    }
    private static int Han(ScoredHand hand, YakuType yaku) => hand.Yaku.Single(item => item.Yaku == yaku).Han;
    private static void StraightYaku()
    {
        WinContext closed = Context("1w 2w 3w 4w 5w 6w 7w 8w 9w 2p 3p 5s 5s", "4p");
        Assert(Han(RiichiScoring.Evaluate(closed), YakuType.Ittsu) == 2, "Closed ittsuu not two han.");
        WinContext open = Context("4w 5w 6w 7w 8w 9w 2p 3p 4p 5s", "5s");
        Open((MainMatchControl)open.State, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("2w 3w"), 0, Tile.BytesTo("1w")));
        Assert(Han(RiichiScoring.Evaluate(open), YakuType.Ittsu) == 1, "Open ittsuu not one han.");
        closed = Context("1w 2w 3w 1p 2p 3p 1s 2s 3s 6p 7p 5z 5z", "8p");
        Assert(Han(RiichiScoring.Evaluate(closed), YakuType.SanshokuDoujun) == 2, "Closed sanshoku missing.");
        open = Context("1p 2p 3p 1s 2s 3s 6p 7p 5z 5z", "8p");
        Open((MainMatchControl)open.State, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 0, Tile.BytesTo("3w")));
        Assert(Han(RiichiScoring.Evaluate(open), YakuType.SanshokuDoujun) == 1, "Open sanshoku not reduced.");
        Assert(Has(RiichiScoring.Evaluate(Context("2w 3w 4w 2w 3w 4w 5p 6p 7p 6s 7s 5z 5z", "8s")), YakuType.Iipeikou), "Closed identical sequence pair missing.");
    }
    private static void OutsideYaku()
    {
        foreach (bool open in new[] { false, true })
        {
            foreach (bool honor in new[] { false, true })
            {
                string pair = honor ? "5z 5z" : "1s 1s";
                WinContext context = Context((open ? "" : "1w 2w 3w ") + "7w 8w 9w 1p 2p 3p 7s 8s " + pair, "9s");
                if (open) { Open((MainMatchControl)context.State, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 0, Tile.BytesTo("3w"))); }
                ScoredHand score = RiichiScoring.Evaluate(context);
                Assert(Han(score, honor ? YakuType.Chanta : YakuType.Junchan) == (honor ? open ? 1 : 2 : open ? 2 : 3), "Outside yaku role/reduction wrong.");
            }
        }
    }
    private static void TripleYaku()
    {
        ScoredHand score = RiichiScoring.Evaluate(Context("2w 2w 2w 2p 2p 2p 2s 2s 2s 7z 7z 5p 5p", "7z"));
        Assert(Has(score, YakuType.Toitoi) && Has(score, YakuType.SanshokuDoukou) && Has(score, YakuType.Sananko) && Has(score, YakuType.Chun), "Triplet yaku omitted.");
        score = RiichiScoring.Evaluate(Context("5z 5z 5z 6z 6z 6z 7z 7z 2w 3w 4w 6p 7p", "8p"));
        Assert(Han(score, YakuType.ShoSanGen) == 2 && score.Han == 4 && score.Fu == 50, "Small dragons or yakuhai/fu wrong.");
        WinContext kans = Context("2w 3w 5p 5p", "4w"); MainMatchControl core = (MainMatchControl)kans.State;
        Open(core, 1, new(GroupType.OpenKan, Color.Honor, Tiles("5z 5z 5z"), 0, Tile.BytesTo("5z")));
        Open(core, 1, new(GroupType.OpenKan, Color.Honor, Tiles("6z 6z 6z"), 2, Tile.BytesTo("6z")));
        Open(core, 1, new(GroupType.ClosedKan, Color.Bamboo, Tiles("1s 1s 1s 1s")));
        score = RiichiScoring.Evaluate(kans);
        Assert(Has(score, YakuType.Sankantsu) && score.Fu == 90, "Three kans or kan fu missing.");
    }
    private static void HonroutouPairs()
    {
        ScoredHand score = RiichiScoring.Evaluate(Context("1w 1w 9w 9w 1p 1p 9p 9p 1s 1s 9s 9s 5z", "5z"));
        Assert(score.Han == 4 && score.Fu == 25 && Has(score, YakuType.Honroutou) && !Has(score, YakuType.Toitoi), "Honroutou seven pairs scored wrong.");
    }
    private static void FirstDrawYakuman()
    {
        WinContext context = Context(tsumo: true); context.FirstDraw = true;
        Assert(Has(RiichiScoring.Evaluate(context), YakuType.Chiiho), "Earthly hand missing.");
        ((MainMatchControl)context.State).CurrentDealerIndex = 1;
        Assert(Has(RiichiScoring.Evaluate(context), YakuType.Tenho), "Heavenly hand missing.");
    }
    private static void ConfiguredYaku()
    {
        WinContext context = Context("2p 3p 4p 6s 7s 3w 4w 5w 6p 6p", "8s"); MainMatchControl core = (MainMatchControl)context.State;
        Open(core, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("4w 5w"), 0, Tile.BytesTo("6w")));
        core.MatchSettingData.OpenTanyao = false;
        Assert(RiichiScoring.Evaluate(context) == null, "Disabled open tanyao still won.");
        core.MatchSettingData.OpenTanyao = true; core.MatchSettingData.MinimumYakuFan = 2; core.DoraPointerList = Tiles("5p 5p");
        Assert(RiichiScoring.Evaluate(context) == null, "Dora satisfied minimum yaku requirement.");
    }
    private static void DoubleWindYaku()
    {
        WinContext context = Context("2z 2z 2z 1w 2w 3w 5p 6p 7p 6s 7s 5z 5z", "8s"); MainMatchControl core = (MainMatchControl)context.State;
        core.Wind = WindType.South;
        ScoredHand score = RiichiScoring.Evaluate(context);
        Assert(Has(score, YakuType.Jikaze) && Has(score, YakuType.Bakaze), "Double wind counted one role.");
    }
    private static void ChankanOnly()
    {
        WinContext context = Context("4w 5w 6w 2p 3p 4p 6s 7s 8s 5p", "5p");
        Open((MainMatchControl)context.State, 1, new(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 0, Tile.BytesTo("3w"))); context.Chankan = true;
        ScoredHand score = RiichiScoring.Evaluate(context);
        Assert(score.Han == 1 && Has(score, YakuType.Chankan), "Robbing added kan required another yaku.");
    }
    private static RoundSettlementResult Win(MainMatchControl core, int source = 0, byte winners = 2, bool tsumo = false, string tile = "9s") => RoundSettlement.Calculate(core,
        tsumo ? RoundOutcomeKind.Tsumo : RoundOutcomeKind.Ron, winners, source, Tile.BytesTo(tile));
    private static void SinglePayment()
    {
        MainMatchControl core = Core(); Hand(core, 1, Pinfu); core.Honba = 2; core.RiichiStickCount = 3;
        RoundSettlementResult result = Win(core);
        Assert(result.Deltas.SequenceEqual(new[] { -1600, 4600, 0, 0 }) && result.SticksAfter == 0 && result.NextHonba == 0 && result.NextDealer == 1,
            "Single ron ledger or next dealer wrong.");
    }
    private static void MultiplePayment()
    {
        foreach (byte mask in new byte[] { 6, 14 })
        {
            MainMatchControl core = Core(); for (int seat = 1; seat < 4; seat++) { Hand(core, seat, Pinfu); }
            core.Honba = 2; core.RiichiStickCount = 3; RoundSettlementResult result = Win(core, winners: mask);
            Assert(result.Deltas[1] == 4600 && result.Deltas[2] == 1000 && result.Deltas[3] == (mask == 14 ? 1000 : 0)
                && result.Deltas[0] == (mask == 14 ? -3600 : -2600), "Multiple ron duplicated or lost extras.");
        }
        MainMatchControl wrapped = Core(); Hand(wrapped, 0, Pinfu); Hand(wrapped, 3, Pinfu); wrapped.Honba = 2; wrapped.RiichiStickCount = 3;
        RoundSettlementResult aroundZero = Win(wrapped, 2, 9);
        Assert(aroundZero.Deltas.SequenceEqual(new[] { 1500, 0, -3100, 4600 }) && aroundZero.DealerRepeats,
            "Extras did not follow nearest winner across seat zero or dealer failed to repeat.");
    }
    private static RoundSettlementResult Draw(MainMatchControl core) => RoundSettlement.Calculate(core, RoundOutcomeKind.ExhaustiveDraw, 0, 3, null);
    private static void NotenPayments()
    {
        for (int count = 0; count <= 4; count++)
        {
            MainMatchControl core = Core(); for (int seat = 0; seat < count; seat++) { Hand(core, seat, Ready); }
            RoundSettlementResult result = Draw(core);
            Assert(result.TenpaiMask == (1 << count) - 1 && result.NextHonba == 1 && result.DealerRepeats == (count > 0), "Tenpai mask/continuation wrong.");
            for (int seat = 0; seat < 4; seat++) { Assert(result.Deltas[seat] == (count == 0 || count == 4 ? 0 : seat < count ? 3000 / count : -3000 / (4 - count)), "Noten payment wrong."); }
        }
    }
    private static void Nagashi()
    {
        MainMatchControl core = Core(); core.Honba = 2; core.RiichiStickCount = 2; core.DiscardTileList[1] = Tiles("1w 9w 1z");
        RoundSettlementResult result = Draw(core);
        Assert(result.NagashiMask == 2 && result.Deltas.SequenceEqual(new[] { -4200, 8600, -2200, -2200 }) && result.SticksAfter == 2
            && !result.DealerRepeats && result.NextHonba == 3, "Nagashi was treated as ordinary win or noten draw.");
        Open(core, 1, Pon("5z", 2)); Assert(RoundSettlement.IsNagashi(core, 1), "Own call cancelled nagashi.");
        Open(core, 2, Pon("1z", 1)); Assert(!RoundSettlement.IsNagashi(core, 1), "Called river still qualified for nagashi.");
    }
    private static void MultipleNagashi()
    {
        MainMatchControl core = Core(); core.DiscardTileList[0] = Tiles("1w"); core.DiscardTileList[1] = Tiles("9w");
        Assert(Draw(core).Deltas.SequenceEqual(new[] { 8000, 4000, -6000, -6000 }), "Simultaneous nagashi ledger wrong.");
    }
    private static MainMatchControl Dragons(bool compound = false)
    {
        MainMatchControl core = Core(); Hand(core, 1, compound ? "1z 1z 1z 2z" : "1w 2w 3w 5p");
        Open(core, 1, Pon("5z", 0)); Open(core, 1, Pon("6z", 2)); Open(core, 1, Pon("7z", 3)); return core;
    }
    private static void PaoTrigger()
    {
        MainMatchControl core = Dragons(); List<Group> groups = core.PlayerOpenSetList[1];
        Assert(RoundSettlement.PaoSeat(groups, true) == 3, "Final pon source not responsible.");
        groups[2] = new(GroupType.AddedKan, Color.Honor, Tiles("7z 7z 7z 7z")) { FuluSource = 3, SingleTile = Tile.BytesTo("7z") };
        Assert(RoundSettlement.PaoSeat(groups, true) == 3, "Added kan changed responsibility.");
        groups[2] = new(GroupType.ClosedKan, Color.Honor, Tiles("7z 7z 7z 7z"));
        Assert(RoundSettlement.PaoSeat(groups, true) == -1, "Final closed kan incorrectly assigned responsibility.");
        groups[0] = new(GroupType.ClosedKan, Color.Honor, Tiles("5z 5z 5z 5z")); groups[2] = Pon("7z", 3);
        Assert(RoundSettlement.PaoSeat(groups, true) == 3, "Prior visible closed kan prevented final open responsibility.");
    }
    private static void PaoRon()
    {
        MainMatchControl core = Dragons(); core.Honba = 2;
        Assert(Win(core, tile: "5p").Deltas.SequenceEqual(new[] { -16000, 32600, 0, -16600 }), "Split pao ron/honba wrong.");
        Assert(Win(core, source: 3, tile: "5p").Deltas.SequenceEqual(new[] { 0, 32600, 0, -32600 }), "Same pao/discarder double charged.");
    }
    private static void PaoTsumo()
    {
        MainMatchControl core = Dragons(true); core.Honba = 2;
        Assert(Win(core, source: 1, tsumo: true, tile: "2z").Deltas.SequenceEqual(new[] { -16000, 64600, -8000, -40600 }), "Compound pao transferred unrelated yakuman liability.");
    }
    private static void BigWindsPao()
    {
        MainMatchControl core = Core(); Hand(core, 1, "5p");
        for (int wind = 1; wind <= 4; wind++) { Open(core, 1, Pon(wind + "z", wind == 4 ? 3 : 0)); }
        Assert(Win(core, source: 1, tsumo: true, tile: "5p").Deltas.SequenceEqual(new[] { 0, 64000, 0, -64000 }), "Big winds liability covered only one unit.");
    }
    private static void Bankruptcy()
    {
        MainMatchControl core = Core(); Hand(core, 1, Pinfu); core.PlayerPoint[0] = 1000;
        Assert(!Win(core).MatchEnded && Win(core).PointsAfter[0] == 0, "Zero points caused bankruptcy.");
        core.PlayerPoint[0] = 900; Assert(Win(core).MatchEnded, "Negative score did not end match.");
        core = Core(); core.PlayerPoint = new() { 10000, 8000, 10000, 72000 };
        Hand(core, 0, "1w 2w 3w 5p"); Hand(core, 1, "1p 1p 2p 2p 3p 3p 4p 4p 5p 6p 6p 7p 7p");
        Open(core, 0, Pon("5z", 2)); Open(core, 0, Pon("6z", 3)); Open(core, 0, Pon("7z", 1));
        RoundSettlementResult multiple = Win(core, 3, 3, tile: "5p");
        Assert(!multiple.MatchEnded && multiple.PointsAfter[1] == 0 && multiple.PointsAfter.All(points => points >= 0),
            "Temporary negative during pao was checked before the other winner's payment.");
    }
    private static void DealerStop()
    {
        MainMatchControl core = Core(); core.Wind = WindType.South; core.Round = 4; core.CurrentDealerIndex = 3;
        core.PlayerPoint = new() { 24000, 24000, 22000, 30000 }; Hand(core, 3, Ready); core.RiichiStickCount = 2;
        RoundSettlementResult result = Draw(core);
        Assert(result.MatchEnded && result.PointsAfter[3] == 35000 && result.SticksAfter == 0, "Dealer tenpai stop or tail deposits wrong.");
        core = Core(); core.Wind = WindType.South; core.Round = 4; core.CurrentDealerIndex = 3;
        core.PlayerPoint = new() { 25000, 24000, 22000, 29000 }; Hand(core, 0, Ready); Hand(core, 3, Ready);
        Assert(Win(core, 2, 9, tile: "5z").MatchEnded, "South four multi-ron dealer top failed automatic win stop.");
    }
    private static void WestEntry()
    {
        MainMatchControl core = Core(); core.Wind = WindType.South; core.Round = 4; core.CurrentDealerIndex = 3;
        RoundSettlementResult result = Draw(core);
        Assert(!result.MatchEnded && result.NextWind == WindType.West && result.NextRound == 1 && result.NextDealer == 0, "No-target south four failed west entry.");
    }
    private static void WestPriority()
    {
        MainMatchControl core = Core(); core.Wind = WindType.West; core.PlayerPoint[0] = 30000;
        Assert(Draw(core).MatchEnded, "West target did not stop.");
        core.PlayerPoint[0] = 29000; Hand(core, 0, Ready); Hand(core, 1, Ready);
        RoundSettlementResult result = Win(core, 3, 3, tile: "5z");
        Assert(!result.MatchEnded && result.DealerRepeats && result.PointsAfter[0] >= 30000, "West target overrode multi-ron dealer continuation.");
    }
    private static void WestFour()
    {
        MainMatchControl core = Core(); core.Wind = WindType.West; core.Round = 4; core.CurrentDealerIndex = 3;
        Assert(Draw(core).MatchEnded, "West four advanced north.");
        for (int seat = 0; seat < 4; seat++) { Hand(core, seat, Ready); }
        Assert(!Draw(core).MatchEnded && Draw(core).DealerRepeats && Draw(core).NextWind == WindType.West, "West four failed legal dealer repeat.");
    }
    private static void Ranking()
    {
        MainMatchControl core = Core(); core.Wind = WindType.West; core.Round = 4; core.CurrentDealerIndex = 3; core.RiichiStickCount = 2;
        RoundSettlementResult result = Draw(core);
        Assert(result.Ranking.SequenceEqual(new[] { 0, 1, 2, 3 }) && result.PointsAfter[0] == 27000 && result.SticksAfter == 0, "Tie rank or tail award ignored initial seats.");
    }
    private static void AbortiveDraw()
    {
        MainMatchControl core = Core(); core.Honba = 2; core.RiichiStickCount = 3;
        RoundSettlementResult result = RoundSettlement.Calculate(core, RoundOutcomeKind.FourKans, 0, 0, null);
        Assert(result.Deltas.All(delta => delta == 0) && result.DealerRepeats && result.NextHonba == 3 && result.SticksAfter == 3, "Abortive draw charged or reset deposits.");
        core.Wind = WindType.West; core.PlayerPoint[0] = 31000;
        Assert(!RoundSettlement.Calculate(core, RoundOutcomeKind.FourKans, 0, 0, null).MatchEnded, "West target overrode abortive-draw continuation.");
    }
}
