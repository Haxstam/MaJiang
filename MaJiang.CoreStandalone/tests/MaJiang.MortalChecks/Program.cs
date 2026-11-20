using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MaJiang.Mortal;
using MaJiangLib;
using MatchType = MaJiangLib.MatchType;

internal static class Program
{
    private const string Inert = "1w 1w 1w 2w 2w 2w 4p 4p 4p 7s 7s 5z 6z";
    private static List<Tile> Tiles(string text) => text.Split(' ').Select(value => Tile.BytesTo(value)).ToList();
    private static void Assert(bool ok, string message) { if (!ok) { throw new InvalidOperationException(message); } }
    private static string State(MahjongEngine game) => JsonSerializer.Serialize(game.GetState(), new JsonSerializerOptions { IncludeFields = true });
    private static MainMatchControl Core(string draw = "7z")
    {
        MainMatchControl core = new();
        core.ServerFourMatchInit(new(MatchType.FourMahjongSouth), Enumerable.Range(0, 4).Select(seat => new UserProfile("p" + seat)).ToArray(),
            new Xoshiro256StarStar(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
        for (int seat = 0; seat < 4; seat++) { core.PlayerList[seat].PlayerHandTile.HandTileList = Tiles(Inert); }
        core.MainTileList[0] = Tile.BytesTo(draw); core.DoraPointerList.Add(core.PrimeTileList[0]);
        return core;
    }
    private static MatchCommand Pick(MortalMatchAdapter adapter, int seat, PlayerAction action) => adapter.GetAvailableCommands(seat).First(choice => choice.Action.PlayerAction == action);
    private static JsonObject Dahai(int seat, string tile, bool draw = true) => new() { ["type"] = "dahai", ["actor"] = seat, ["pai"] = tile, ["tsumogiri"] = draw };

    private sealed class FakeClient : IMjaiClient
    {
        public List<JsonObject> Events = new();
        public Func<JsonObject, JsonObject> React;
        public bool ThrowOnDiscard;
        public bool ThrowOnStart;
        public bool Disposed;
        public Task<JsonObject> ReactAsync(JsonObject evt, CancellationToken cancellationToken = default)
        {
            Events.Add((JsonObject)evt.DeepClone());
            if (ThrowOnStart && evt["type"]?.GetValue<string>() == "start_game") { throw new IOException("test initialization failure"); }
            if (ThrowOnDiscard && evt["type"]?.GetValue<string>() == "dahai") { throw new IOException("test bridge failure"); }
            return Task.FromResult(React?.Invoke(evt));
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }

    private static async Task<int> Main()
    {
        (string Name, Func<Task> Run)[] checks =
        {
            ("Tile names preserve all 34 faces and red fives", TileNames),
            ("Experiment seeds remain deterministic and do not repeat every 256 games", SeedNumbers),
            ("Action mapping preserves exact red selections, actor, target and tsumogiri", Mapping),
            ("Kan and win recommendations map to the exact local action", KanAndWinMapping),
            ("Start events preserve absolute seats, 13 tiles and hidden information", StartPrivacy),
            ("Curse assignments and hidden adjustments never enter model input", CursePrivacy),
            ("Model recommendation is passive and committed discard is echoed once", PassiveAndOnce),
            ("Illegal model action cannot invoke core default behavior", IllegalSuggestion),
            ("Null reaction is different from an explicit pass", NullAndPass),
            ("Reach handshake stays private until the combined operation is executed", ReachHandshake),
            ("A ronned reach declaration never emits reach_accepted", RonnedReach),
            ("Overriding a previewed reach resets the model without declaring riichi", OverrideReach),
            ("Competing chi is not echoed when pon wins", LosingClaim),
            ("Kan declarations precede rob windows and are not duplicated on commit", KanEvents),
            ("Kokushi rob-ankan defaults to an explicit legal skip without phantom actions", SkipKokushiAnkan),
            ("Failed model synchronization preserves applied feedback and can be replayed", Recovery),
            ("Failed model initialization releases its process client without advancing core", FailedAttachment),
            ("End and next-round events require an explicit next-round call", ExplicitNextRound),
            ("Bypassing the adapter is detected", BypassDetected),
            ("Durable records contain successful actions and preserve failures", RecordingFacts),
            ("Round transitions keep records contiguous and audit incremental", RecordingRounds),
            ("Only completed experimental matches are eligible for statistics", RecordingEligibility),
            ("V2 rules remain absent from model input", () => CursePrivacy(DemonCurseRulesVersion.V2)),
            ("Both curse rule versions are explicit in match metadata", RecordingRuleVersions)
        };
        int passed = 0;
        foreach (var check in checks)
        {
            try { await check.Run(); passed++; Console.WriteLine("PASS " + check.Name); }
            catch (Exception exception) { Console.Error.WriteLine("FAIL " + check.Name + ": " + exception); }
        }
        Console.WriteLine($"{passed}/{checks.Length} Mortal adapter checks passed.");
        return passed == checks.Length ? 0 : 1;
    }

    private static Task TileNames()
    {
        for (int face = 0; face < 34; face++)
        {
            Tile tile = HandAnalysis.TileOf(face); Assert(Tile.ExactlyEquals(tile, MjaiTiles.Parse(MjaiTiles.Format(tile))), "Tile round trip.");
        }
        foreach (Color color in new[] { Color.Wans, Color.Tungs, Color.Bamboo })
        {
            Tile red = new(color, 5, true); Assert(Tile.ExactlyEquals(red, MjaiTiles.Parse(MjaiTiles.Format(red))), "Red round trip.");
        }
        foreach (string invalid in new[] { "?", "0m", "5zr", "1mr", "10p", "5px" })
        {
            try { MjaiTiles.Parse(invalid); throw new Exception("Invalid tile accepted."); } catch (FormatException) { }
        }
        return Task.CompletedTask;
    }

    private static Task SeedNumbers()
    {
        Assert(ExperimentSeeds.FromNumber(1).SequenceEqual(ExperimentSeeds.FromNumber(1)) &&
            !ExperimentSeeds.FromNumber(1).SequenceEqual(ExperimentSeeds.FromNumber(257)), "Experiment seed repeats or is not stable.");
        Assert(Enumerable.Range(0, 1024).Select(value => Convert.ToHexString(ExperimentSeeds.FromNumber(value))).Distinct().Count() == 1024,
            "Seed numbering collapsed different experiment runs.");
        return Task.CompletedTask;
    }

    private static Task Mapping()
    {
        MahjongEngine engine = new(Core()); VisibleMatchState view = engine.GetPlayerView(0);
        MatchCommand cut = engine.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut);
        Assert(ReferenceEquals(MjaiActionMapper.Match(Dahai(0, "C"), view, new[] { cut }), cut), "Draw discard mapping.");
        Assert(MjaiActionMapper.Match(Dahai(1, "C"), view, new[] { cut }) == null &&
            MjaiActionMapper.Match(Dahai(0, "C", false), view, new[] { cut }) == null, "Actor or tsumogiri ignored.");
        MatchCommand pon = new() { Action = new(0, 2, Tiles("0p 5p"), Tile.BytesTo("5p"), PlayerAction.Peng) };
        JsonObject reaction = new() { ["type"] = "pon", ["actor"] = 0, ["target"] = 2, ["pai"] = "5p", ["consumed"] = new JsonArray("5p", "5pr") };
        Assert(ReferenceEquals(MjaiActionMapper.Match(reaction, view, new[] { pon }), pon), "Exact red pon not matched.");
        reaction["consumed"] = new JsonArray("5p", "5p");
        Assert(MjaiActionMapper.Match(reaction, view, new[] { pon }) == null, "Red selection silently substituted.");
        MatchCommand abort = new() { Action = new(0, 0, new(), null, PlayerAction.Draw) };
        Assert(ReferenceEquals(MjaiActionMapper.Match(new JsonObject { ["type"] = "ryukyoku" }, view, new[] { abort }), abort),
            "Native ryukyoku has no actor field and must map to a legal nine-terminals declaration.");
        return Task.CompletedTask;
    }

    private static async Task StartPrivacy()
    {
        MahjongEngine engine = new(Core()); await using MortalMatchAdapter adapter = new(engine);
        for (int seat = 0; seat < 4; seat++)
        {
            FakeClient client = new(); await adapter.AttachModelAsync(seat, client);
            Assert(client.Events.Select(evt => evt["type"].GetValue<string>()).SequenceEqual(new[] { "start_game", "start_kyoku", "tsumo" }), "Initial event order or duplicate initial dora.");
            JsonObject start = client.Events[1]; Assert(start["oya"].GetValue<int>() == 0 && start["scores"][0].GetValue<int>() == 25000, "Absolute seat/point units changed.");
            for (int other = 0; other < 4; other++)
            {
                JsonArray hand = (JsonArray)start["tehais"][other];
                Assert(hand.Count == 13 && hand.All(tile => (tile.GetValue<string>() == "?") == (other != seat)), "Hidden or 13-tile start contract violated.");
            }
            Assert(client.Events[2]["pai"].GetValue<string>() == (seat == 0 ? "C" : "?"), "Private draw leaked.");
            Assert(client.Events.Count(evt => evt["can_act"].GetValue<bool>()) == (seat == 0 ? 1 : 0), "Replay inferred at non-current points.");
        }
    }

    private static Task KanAndWinMapping()
    {
        MahjongEngine game = new(Core()); VisibleMatchState view = game.GetPlayerView(0);
        view.State.PlayerOpenSetList[0].Add(new Group(GroupType.OpenTriple, Color.Tungs, Tiles("0p 5p"), 2, Tile.BytesTo("5p")));
        MatchCommand added = new() { Action = new(0, 0, Tiles("5p"), Tile.BytesTo("5p"), PlayerAction.AddedKan) };
        MatchCommand closed = new() { Action = new(0, 0, Tiles("1w 1w 1w 1w"), Tile.BytesTo("1w"), PlayerAction.ClosedKan) };
        MatchCommand open = new() { Action = new(0, 2, Tiles("7s 7s 7s"), Tile.BytesTo("7s"), PlayerAction.OpenKan) };
        MatchCommand ron = new() { Action = new(0, 2, new(), Tile.BytesTo("5p"), PlayerAction.Ron) };
        MatchCommand tsumo = new() { Action = new(0, 0, new(), Tile.BytesTo("5p"), PlayerAction.Tsumo) };
        Assert(ReferenceEquals(MjaiActionMapper.Match(new JsonObject { ["type"] = "kakan", ["actor"] = 0, ["pai"] = "5p", ["consumed"] = new JsonArray("5p", "5pr", "5p") }, view, new[] { added }), added), "Kakan old pon not matched.");
        Assert(ReferenceEquals(MjaiActionMapper.Match(new JsonObject { ["type"] = "ankan", ["actor"] = 0, ["consumed"] = new JsonArray("1m", "1m", "1m", "1m") }, view, new[] { closed }), closed), "Ankan without pai/target not matched.");
        Assert(ReferenceEquals(MjaiActionMapper.Match(new JsonObject { ["type"] = "daiminkan", ["actor"] = 0, ["target"] = 2, ["pai"] = "7s", ["consumed"] = new JsonArray("7s", "7s", "7s") }, view, new[] { open }), open), "Daiminkan wrong consumed contract.");
        Assert(ReferenceEquals(MjaiActionMapper.Match(new JsonObject { ["type"] = "hora", ["actor"] = 0, ["target"] = 2 }, view, new[] { ron, tsumo }), ron), "Ron target not matched.");
        Assert(ReferenceEquals(MjaiActionMapper.Match(new JsonObject { ["type"] = "hora", ["actor"] = 0, ["target"] = 0 }, view, new[] { ron, tsumo }), tsumo), "Self-draw target not matched.");
        return Task.CompletedTask;
    }

    private static async Task PassiveAndOnce()
    {
        MahjongEngine engine = new(Core()); await using MortalMatchAdapter adapter = new(engine);
        FakeClient client = new() { React = evt => evt["can_act"]?.GetValue<bool>() == true ? Dahai(0, "C") : null };
        await adapter.AttachModelAsync(0, client); string before = State(engine);
        MortalDecision decision = await adapter.RecommendAsync(0);
        Assert(decision.Available && before == State(engine), "Recommendation advanced core.");
        MortalStepResult step = await adapter.SubmitModelAsync(decision);
        Assert(step.Feedback.Accepted && !step.Feedback.UsedDefault && client.Events.Count(evt => evt["type"]?.GetValue<string>() == "dahai") == 1, "Discard not committed/echoed once.");
        int count = adapter.GetProtocolHistory(0).Count;
        await adapter.SubmitAsync(0, decision.Command);
        Assert(adapter.GetProtocolHistory(0).Count == count, "Idempotent retry duplicated protocol events.");
        string after = State(engine);
        try { await adapter.SubmitModelAsync(decision); throw new Exception("Stale model choice accepted."); }
        catch (InvalidOperationException) { }
        Assert(State(engine) == after, "Stale model choice invoked core defaults.");
    }

    private static Task CursePrivacy() => CursePrivacy(DemonCurseRulesVersion.V1);
    private static async Task CursePrivacy(DemonCurseRulesVersion version)
    {
        DemonCurse[] curses = { DemonCurse.ScatteredInitialHand, DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw, DemonCurse.NoBonusTiles };
        MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Enumerable.Range(0, 4).Select(seat => new UserProfile("p" + seat)).ToArray(),
            ExperimentSeeds.FromNumber(250), new DemonCurseOptions(curses, version));
        await using MortalMatchAdapter adapter = new(game);
        FakeClient observer = new(); await adapter.AttachModelAsync(0, observer);
        int operations = 0;
        while (adapter.GetState().OperationStage != MatchOperationStage.RoundEnded && operations++ < 400)
        {
            int seat = adapter.GetState().PendingSeats[0]; var choices = adapter.GetAvailableCommands(seat);
            await adapter.SubmitAsync(seat, choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                ?? choices.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
        }
        string[] allowedFields = { "type", "actor", "target", "pai", "consumed", "tsumogiri", "names", "tehais", "bakaze", "kyoku", "honba", "kyotaku", "oya", "scores", "dora_marker", "can_act", "deltas", "ura_markers" };
        Assert(game.GetCurseAudit().Any(record => record.Adjustment == CurseAdjustment.SwappedLiveWall || record.Adjustment == CurseAdjustment.SwappedDeadWall), "Fixture never adjusted any hidden tiles.");
        Assert(observer.Events.All(evt => evt.All(field => allowedFields.Contains(field.Key))), "An experiment-only field entered model input.");
        Assert(observer.Events.Where(evt => evt["type"]?.GetValue<string>() == "tsumo" && evt["actor"].GetValue<int>() != 0)
            .All(evt => evt["pai"].GetValue<string>() == "?"), "Hidden draw was exposed during curse adjustment.");
    }

    private static async Task IllegalSuggestion()
    {
        MahjongEngine engine = new(Core()); await using MortalMatchAdapter adapter = new(engine);
        await adapter.AttachModelAsync(0, new FakeClient { React = evt => evt["can_act"]?.GetValue<bool>() == true ? Dahai(0, "9m") : null });
        string before = State(engine); MortalDecision decision = await adapter.RecommendAsync(0);
        Assert(!decision.Available && decision.Reason == "ModelActionNotLegalLocally" && before == State(engine), "Invalid model move defaulted a discard.");
    }

    private static MainMatchControl Calls()
    {
        MainMatchControl core = Core("3w");
        core.PlayerList[1].PlayerHandTile.HandTileList = Tiles("1w 2w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z");
        core.PlayerList[2].PlayerHandTile.HandTileList = Tiles("3w 3w 4w 6w 8w 1p 2p 4p 5p 7s 8s 5z 6z");
        return core;
    }

    private static async Task NullAndPass()
    {
        MahjongEngine engine = new(Calls()); await using MortalMatchAdapter adapter = new(engine);
        FakeClient client = new(); await adapter.AttachModelAsync(2, client);
        await adapter.SubmitAsync(0, Pick(adapter, 0, PlayerAction.TakeCut));
        string before = State(engine);
        Assert(!(await adapter.RecommendAsync(2)).Available && before == State(engine), "Null recommendation was treated as a pass.");
        await adapter.AttachModelAsync(2, new FakeClient { React = evt => evt["can_act"]?.GetValue<bool>() == true ? new JsonObject { ["type"] = "none" } : null });
        MortalDecision pass = await adapter.RecommendAsync(2);
        Assert(pass.Available && pass.Command.Action.PlayerAction == PlayerAction.Skip, "Explicit none was not mapped to skip.");
        await adapter.SubmitModelAsync(pass);
        Assert(engine.GetState().PendingSeats.SequenceEqual(new[] { 1 }), "Pass was not applied to the actual response window.");
    }

    private static MahjongEngine RiichiGame()
    {
        MainMatchControl core = Core("1p");
        core.PlayerList[0].PlayerHandTile.HandTileList = Tiles("2w 3w 4w 5p 6p 7p 2s 3s 4s 7s 8s 1z 1z");
        return new MahjongEngine(core);
    }

    private static FakeClient ReachClient() => new()
    {
        React = evt => evt["can_act"]?.GetValue<bool>() != true ? null : evt["type"]?.GetValue<string>() == "reach"
            ? Dahai(0, "1p") : new JsonObject { ["type"] = "reach", ["actor"] = 0 }
    };

    private static async Task ReachHandshake()
    {
        MahjongEngine engine = RiichiGame(); await using MortalMatchAdapter adapter = new(engine);
        FakeClient model = ReachClient(), observer = new();
        await adapter.AttachModelAsync(0, model); await adapter.AttachModelAsync(1, observer);
        string before = State(engine); MortalDecision decision = await adapter.RecommendAsync(0);
        Assert(decision.Available && decision.Command.Action.PlayerAction == PlayerAction.Riichi && State(engine) == before, "Reach handshake changed core or lost combined discard.");
        Assert(!adapter.GetProtocolHistory(0).Any(line => JsonNode.Parse(line)["type"].GetValue<string>() == "reach"), "Preview was logged as a public event.");
        await adapter.SubmitModelAsync(decision);
        Assert(model.Events.Count(evt => evt["type"]?.GetValue<string>() == "reach") == 1 && observer.Events.Count(evt => evt["type"]?.GetValue<string>() == "reach") == 1,
            "Reach preview was echoed twice or never broadcast.");
        string[] types = adapter.GetProtocolHistory(0).Select(line => JsonNode.Parse(line)["type"].GetValue<string>()).ToArray();
        Assert(types.Skip(3).Take(3).SequenceEqual(new[] { "reach", "dahai", "reach_accepted" }) && engine.GetState().State.PlayerPoint[0] == 24000,
            "Riichi event ordering or single deposit charge is wrong.");
    }

    private static async Task OverrideReach()
    {
        MahjongEngine engine = RiichiGame(); await using MortalMatchAdapter adapter = new(engine);
        FakeClient model = ReachClient(); await adapter.AttachModelAsync(0, model); await adapter.RecommendAsync(0);
        await adapter.SubmitAsync(0, Pick(adapter, 0, PlayerAction.TakeCut));
        Assert(model.Events.Count(evt => evt["type"]?.GetValue<string>() == "start_game") == 2 && engine.GetState().State.PlayerPoint[0] == 25000,
            "Overridden preview left a false riichi or did not replay the model.");
        Assert(!adapter.GetProtocolHistory(0).Any(line => JsonNode.Parse(line)["type"].GetValue<string>() == "reach"), "Overridden preview entered public history.");
    }

    private static async Task RonnedReach()
    {
        MainMatchControl core = Core("9p");
        core.PlayerList[0].PlayerHandTile.HandTileList = Tiles("1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z");
        core.PlayerList[2].PlayerHandTile.HandTileList = Tiles("1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 9p");
        MahjongEngine game = new(core); await using MortalMatchAdapter adapter = new(game);
        MatchCommand reach = adapter.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.Riichi && choice.Action.TargetTile == Tile.BytesTo("9p"));
        await adapter.SubmitAsync(0, reach); await adapter.SubmitAsync(2, Pick(adapter, 2, PlayerAction.Ron));
        string[] types = adapter.GetProtocolHistory(0).Select(line => JsonNode.Parse(line)["type"].GetValue<string>()).ToArray();
        Assert(types.Contains("reach") && types.Contains("hora") && !types.Contains("reach_accepted") && game.GetState().State.RiichiStickCount == 0,
            "Ronned declaration was accepted or charged twice in the model stream.");
    }

    private static async Task LosingClaim()
    {
        MahjongEngine engine = new(Calls()); await using MortalMatchAdapter adapter = new(engine);
        await adapter.SubmitAsync(0, Pick(adapter, 0, PlayerAction.TakeCut)); int count = adapter.GetProtocolHistory(0).Count;
        await adapter.SubmitAsync(1, Pick(adapter, 1, PlayerAction.Chi));
        Assert(adapter.GetProtocolHistory(0).Count == count, "Pending chi was sent to models.");
        await adapter.SubmitAsync(2, Pick(adapter, 2, PlayerAction.Peng));
        string[] types = adapter.GetProtocolHistory(0).Select(line => JsonNode.Parse(line)["type"].GetValue<string>()).ToArray();
        Assert(types.Count(type => type == "pon") == 1 && !types.Contains("chi"), "Losing chi was echoed.");
    }

    private static MatchState Snapshot(MatchJournal journal) => new()
    {
        MatchId = journal.MatchId, RoundId = journal.RoundId, RoundGeneration = journal.RoundGeneration, PhaseId = journal.PhaseId,
        Sequence = journal.LatestSequence, State = StateCopy.Core(journal.AuthoritativeState), OperationStage = journal.OperationStage,
        PendingSeats = Enumerable.Range(0, 4).Where(seat => !journal.HasResponded[seat]).ToArray(),
        ResponseSourceSeat = journal.ResponseSourceSeat, ResponseTile = journal.ResponseTile, Settlement = StateCopy.Settlement(journal.Settlement)
    };

    private static async Task KanEvents()
    {
        foreach (bool rob in new[] { false, true })
        {
            MainMatchControl core = Core("5p");
            core.PlayerList[0].PlayerHandTile.HandTileList = Tiles("1w 2w 3w 4w 5w 6w 7s 8s 9s 5z");
            core.PlayerOpenSetList[0].Add(new Group(GroupType.OpenTriple, Color.Tungs, Tiles("5p 5p"), 1, Tile.BytesTo("5p")));
            core.PlayerList[2].PlayerHandTile.HandTileList = Tiles("4w 5w 6w 3p 4p 6s 7s 8s 2z 2z");
            core.PlayerOpenSetList[2].Add(new Group(GroupType.OpenStraight, Color.Wans, Tiles("1w 2w"), 3, Tile.BytesTo("3w")));
            MatchJournal journal = new(core); MatchSession session = new(journal); MjaiEventTranslator translator = new();
            List<JsonObject> events = new();
            void Act(int seat, PlayerAction type)
            {
                MatchState before = Snapshot(journal);
                PlayerActionData action = session.Machine.GetActions(seat).First(choice => choice.PlayerAction == type);
                MatchCommand command = new() { MatchId = journal.MatchId, RoundId = journal.RoundId, RoundGeneration = journal.RoundGeneration,
                    PhaseId = journal.PhaseId, CommandId = Guid.NewGuid(), Action = action };
                session.Machine.Submit(seat, command); session.SettleIfEnded();
                events.AddRange(translator.Translate(before, Snapshot(journal), new OperationFeedback { Records = journal.GetRecords(journal.RoundId, before.Sequence) }, action));
            }
            Act(0, PlayerAction.AddedKan);
            Assert(events.Count == 1 && events[0]["type"].GetValue<string>() == "kakan" && ((JsonArray)events[0]["consumed"]).Count == 3 &&
                journal.GetRecords(journal.RoundId).All(record => record.Action?.PlayerAction != PlayerAction.AddedKan), "Kakan declaration mapping/consumed count invalid.");
            Act(2, rob ? PlayerAction.Ron : PlayerAction.Skip);
            Assert(events.Count(evt => evt["type"].GetValue<string>() == "kakan") == 1, "Committed kan was echoed twice.");
            Assert(rob ? events.Any(evt => evt["type"].GetValue<string>() == "hora") && !events.Any(evt => evt["type"].GetValue<string>() == "tsumo")
                : events.Any(evt => evt["type"].GetValue<string>() == "tsumo"), "Rob/commit follow-up sequence invalid.");
        }
        await Task.CompletedTask;
    }

    private static async Task SkipKokushiAnkan()
    {
        foreach (bool modelResponds in new[] { false, true })
        {
            MainMatchControl core = Core("1w");
            core.PlayerList[0].PlayerHandTile.HandTileList = Tiles("1w 1w 1w 2p 3p 4p 6s 7s 8s 5z 6z 7z 9p");
            core.PlayerList[2].PlayerHandTile.HandTileList = Tiles("9w 9w 1p 9p 1s 9s 1z 2z 3z 4z 5z 6z 7z");
            MahjongEngine engine = new(core); await using MortalMatchAdapter adapter = new(engine);
            await adapter.AttachModelAsync(2, new FakeClient { React = evt => modelResponds && evt["can_act"]?.GetValue<bool>() == true
                ? new JsonObject { ["type"] = "hora", ["actor"] = 2, ["target"] = 0 } : null });
            await adapter.SubmitAsync(0, Pick(adapter, 0, PlayerAction.ClosedKan));
            Assert(adapter.GetAvailableCommands(2).Any(choice => choice.Action.PlayerAction == PlayerAction.Ron), "Fixture lost its legal kokushi rob.");
            string before = State(engine); MortalDecision decision = await adapter.RecommendAsync(2);
            Assert(decision.Available && decision.Reason == "KokushiRobClosedKanSkippedByPolicy" && decision.Command.Action.PlayerAction == PlayerAction.Skip && before == State(engine),
                "Policy did not provide a passive, legal skip.");
            Assert(decision.ReactionJson == null && (await adapter.RecommendAsync(2)).Command.CommandId == decision.Command.CommandId, "Policy fabricated a model reply or unstable recommendation.");
            MortalStepResult step = await adapter.SubmitModelAsync(decision);
            Assert(step.Feedback.Accepted && !step.Feedback.UsedDefault && step.ModelErrors.Count == 0 && engine.GetState().Outcome == RoundOutcomeKind.None,
                "Policy skip failed or ended the round.");
            Assert(engine.GetRecords().Count(record => record.Action?.PlayerAction == PlayerAction.ClosedKan) == 1 &&
                engine.GetRecords().All(record => record.Action?.PlayerAction != PlayerAction.Ron && record.Action?.PlayerAction != PlayerAction.Skip),
                "Successful journal lost the kan or added phantom win/skip actions.");
            Assert((engine.GetState().FuritenFlags[2] & 2) != 0 && adapter.GetAvailableCommands(0).Count > 0, "Pass did not apply temporary furiten or resume the kan player.");
            string[] types = adapter.GetProtocolHistory(2).Select(line => JsonNode.Parse(line)["type"].GetValue<string>()).ToArray();
            Assert(types.Count(type => type == "ankan") == 1 && types.Last() == "tsumo" && !types.Contains("hora"), "Kan was duplicated or rinshan update missing.");
        }
    }

    private static async Task Recovery()
    {
        MahjongEngine engine = new(Core()); await using MortalMatchAdapter adapter = new(engine);
        await adapter.AttachModelAsync(0, new FakeClient { ThrowOnDiscard = true });
        MortalStepResult step = await adapter.SubmitAsync(0, Pick(adapter, 0, PlayerAction.TakeCut));
        Assert(step.Feedback.Accepted && step.ModelErrors.ContainsKey(0) && engine.GetState().State.DiscardTileList[0].Count == 1,
            "Model failure hid or reverted an applied discard.");
        string before = State(engine); FakeClient replacement = new(); await adapter.AttachModelAsync(0, replacement);
        Assert(State(engine) == before && replacement.Events.Count(evt => evt["type"].GetValue<string>() == "dahai") == 1,
            "Replacement replay changed core or duplicated a real action.");
    }

    private static async Task ExplicitNextRound()
    {
        MahjongEngine engine = new(new MatchSettingData(MatchType.FourMahjongSouth), Enumerable.Range(0, 4).Select(seat => new UserProfile("p" + seat)).ToArray(),
            Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        await using MortalMatchAdapter adapter = new(engine);
        int steps = 0;
        while (adapter.GetState().OperationStage != MatchOperationStage.RoundEnded && steps++ < 1000)
        {
            int seat = adapter.GetState().PendingSeats[0]; var choices = adapter.GetAvailableCommands(seat);
            await adapter.SubmitAsync(seat, choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                ?? choices.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
        }
        string before = State(engine); int count = adapter.GetProtocolHistory(0).Count;
        Assert(JsonNode.Parse(adapter.GetProtocolHistory(0).Last())["type"].GetValue<string>() == "end_kyoku", "Round end event missing.");
        adapter.GetPlayerView(0); Assert(State(engine) == before, "Query started next round.");
        await adapter.StartNextRoundAsync();
        string[] types = adapter.GetProtocolHistory(0).Skip(count).Select(line => JsonNode.Parse(line)["type"].GetValue<string>()).ToArray();
        Assert(types.SequenceEqual(new[] { "start_kyoku", "tsumo" }), "Explicit next-round event order invalid.");
    }

    private static async Task FailedAttachment()
    {
        MahjongEngine game = new(Core()); await using MortalMatchAdapter adapter = new(game);
        FakeClient client = new() { ThrowOnStart = true }; string before = State(game);
        try { await adapter.AttachModelAsync(0, client); throw new Exception("Initialization unexpectedly succeeded."); }
        catch (IOException) { }
        Assert(client.Disposed && before == State(game) && (await adapter.RecommendAsync(0)).Reason == "NoModelAttached",
            "Failed attachment leaked its client or altered core.");
    }

    private static async Task BypassDetected()
    {
        MahjongEngine engine = new(Core()); await using MortalMatchAdapter adapter = new(engine);
        engine.Submit(0, engine.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut));
        try { await adapter.RecommendAsync(0); throw new Exception("Bypass not detected."); }
        catch (InvalidOperationException exception) { Assert(exception.Message.Contains("outside the adapter"), "Wrong bypass diagnostic."); }
    }

    private static ExperimentMatchRecorder Recorder(MahjongEngine game, string purpose = "validation", DemonCurseOptions curses = null) =>
        new(Path.Combine(Path.GetFullPath(".utmp/recording-checks"), Guid.NewGuid().ToString("N")), game, 2,
            ExperimentSeeds.FromNumber(2), purpose, new[] { 0, 1, 2, 3 }, curses, new MortalBridgeOptions { ModelDirectory = ".utmp/no-model-needed" });
    private static JsonObject Manifest(ExperimentMatchRecorder recorder) =>
        (JsonObject)JsonNode.Parse(File.ReadAllText(Path.Combine(recorder.DirectoryPath, "match.json")));
    private static string[] ReadLiveLines(string path)
    {
        using StreamReader reader = new(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        List<string> lines = new(); string line;
        while ((line = reader.ReadLine()) != null) { lines.Add(line); }
        return lines.ToArray();
    }
    private static JsonNode[] Events(ExperimentMatchRecorder recorder) =>
        ReadLiveLines(Path.Combine(recorder.DirectoryPath, "events.jsonl")).Select(line => JsonNode.Parse(line)).ToArray();

    private static async Task RecordingFacts()
    {
        MahjongEngine game = new(Calls()); await using MortalMatchAdapter adapter = new(game);
        using ExperimentMatchRecorder recorder = Recorder(game);
        await adapter.AttachModelAsync(0, new FakeClient { ThrowOnDiscard = true });
        MortalStepResult discard = await adapter.SubmitAsync(0, Pick(adapter, 0, PlayerAction.TakeCut)); recorder.RecordStep(discard.Feedback);
        Assert(discard.ModelErrors.Count == 1 && Events(recorder).Any(evt => evt["record"]?["action"]?["playerAction"]?.GetValue<string>() == "TakeCut"),
            "An applied action was lost after model synchronization failed.");
        int count = Events(recorder).Length;
        recorder.RecordStep((await adapter.SubmitAsync(1, Pick(adapter, 1, PlayerAction.Chi))).Feedback);
        Assert(Events(recorder).Length == count, "A pending claim was recorded as a successful action.");
        recorder.RecordStep((await adapter.SubmitAsync(2, Pick(adapter, 2, PlayerAction.Peng))).Feedback);
        string[] actions = Events(recorder).Select(evt => evt["record"]?["action"]?["playerAction"]?.GetValue<string>()).ToArray();
        Assert(actions.Count(action => action == "Peng") == 1 && !actions.Contains("Chi"), "Losing claim or duplicate final call entered records.");
        recorder.ExportProtocol(adapter, new[] { 0 });
        recorder.Finish("failed", new { playSeconds = 0 }, new Dictionary<int, JsonObject>(), "test model failure");
        JsonObject manifest = Manifest(recorder);
        Assert(manifest["status"].GetValue<string>() == "failed" && !manifest["eligibleForStatistics"].GetValue<bool>() &&
            manifest["error"].GetValue<string>() == "test model failure", "Failed match was eligible or lost its diagnostic.");
        Assert(File.Exists(Path.Combine(recorder.DirectoryPath, "final-state.json")) && File.Exists(Path.Combine(recorder.DirectoryPath, "seat-0.mjai.jsonl")), "Diagnostic state/protocol missing.");
    }

    private static async Task RecordingRounds()
    {
        DemonCurseOptions curses = new(new[] { DemonCurse.ScatteredInitialHand, DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw, DemonCurse.NoBonusTiles });
        MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Enumerable.Range(0, 4).Select(seat => new UserProfile("p" + seat)).ToArray(),
            ExperimentSeeds.FromNumber(2), curses);
        using ExperimentMatchRecorder recorder = Recorder(game, curses: curses);
        Assert(Manifest(recorder)["status"].GetValue<string>() == "running" && !Manifest(recorder)["eligibleForStatistics"].GetValue<bool>(), "Unfinished run was eligible.");
        for (int round = 0; round < 2; round++)
        {
            for (int steps = 0; game.GetState().OperationStage != MatchOperationStage.RoundEnded; steps++)
            {
                Assert(steps < 1000, "Fixture round did not end.");
                int seat = game.GetState().PendingSeats[0]; var choices = game.GetAvailableCommands(seat);
                recorder.RecordStep(game.Submit(seat, choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                    ?? choices.First(choice => choice.Action.PlayerAction == PlayerAction.TakeCut)));
            }
            if (round == 0) { Assert(game.StartNextRound().Accepted, "Fixture could not advance."); recorder.RecordRoundStart(); }
        }
        recorder.Finish("truncated", new { playSeconds = 0 }, new Dictionary<int, JsonObject>());
        Assert(recorder.SettledRounds == 2 && ReadLiveLines(Path.Combine(recorder.DirectoryPath, "rounds.jsonl")).Length == 2, "Settlement count mismatched.");
        JsonNode[] events = Events(recorder);
        Assert(events.Count(evt => evt["type"].GetValue<string>() == "round_start") == 2, "Round checkpoints missing.");
        foreach (Guid roundId in game.RoundIds)
        {
            long[] sequences = events.Where(evt => evt["record"]?["roundId"]?.GetValue<string>() == roundId.ToString())
                .Select(evt => evt["record"]["sequence"].GetValue<long>()).ToArray();
            Assert(sequences.SequenceEqual(game.GetRoundRecords(roundId).Select(record => record.Sequence)), "Sequence gap, duplicated or missing records.");
        }
        JsonNode[] audit = ReadLiveLines(Path.Combine(recorder.DirectoryPath, "curse-audit.jsonl")).Select(line => JsonNode.Parse(line)).ToArray();
        Assert(audit.Length == game.GetCurseAudit().Count && audit.Select(evt => evt["index"].GetValue<int>()).SequenceEqual(Enumerable.Range(0, audit.Length)), "Audit incremental export lost/duplicated entries.");
        Assert(game.GetCurseAudit(audit.Length).Count == 0 && !Manifest(recorder)["eligibleForStatistics"].GetValue<bool>(), "Audit tail or truncated eligibility invalid.");
        await Task.CompletedTask;
    }

    private static Task RecordingRuleVersions()
    {
        foreach (DemonCurseRulesVersion version in new[] { DemonCurseRulesVersion.V1, DemonCurseRulesVersion.V2 })
        {
            DemonCurseOptions curses = new(new[] { DemonCurse.ScatteredInitialHand, DemonCurse.NoWinningDraw, DemonCurse.NoDuplicateDraw, DemonCurse.NoBonusTiles }, version);
            MahjongEngine game = new(new MatchSettingData(MatchType.FourMahjongSouth), Enumerable.Range(0, 4).Select(seat => new UserProfile("p" + seat)).ToArray(), ExperimentSeeds.FromNumber(2), curses);
            using ExperimentMatchRecorder recorder = Recorder(game, curses: curses);
            Assert(Manifest(recorder)["curseRuleVersion"].GetValue<string>() == (version == DemonCurseRulesVersion.V1 ? "v1" : "v2"), "Rule version missing or mislabeled.");
        }
        return Task.CompletedTask;
    }

    private static async Task RecordingEligibility()
    {
        foreach (string purpose in new[] { "experiment", "benchmark" })
        {
            MainMatchControl core = Core("5z"); core.Wind = WindType.South; core.Round = 4;
            core.PlayerPoint[0] = 35000;
            core.PlayerList[0].PlayerHandTile.HandTileList = Tiles("1w 2w 3w 1p 2p 3p 1s 2s 3s 7z 7z 7z 5z");
            MahjongEngine game = new(core); using ExperimentMatchRecorder recorder = Recorder(game, purpose);
            recorder.RecordStep(game.Submit(0, game.GetAvailableCommands(0).First(choice => choice.Action.PlayerAction == PlayerAction.Tsumo)));
            Assert(game.GetState().MatchEnded, "Fixture did not reach match end.");
            recorder.Finish("completed", new { playSeconds = 0 }, new Dictionary<int, JsonObject>());
            JsonObject manifest = Manifest(recorder);
            Assert(manifest["eligibleForStatistics"].GetValue<bool>() == (purpose == "experiment") && ((JsonArray)manifest["ranking"]).Count == 4,
                "Purpose filter or final ranking missing.");
        }
        await Task.CompletedTask;
    }
}
