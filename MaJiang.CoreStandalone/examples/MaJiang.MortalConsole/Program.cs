using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MaJiang.Mortal;
using MaJiangLib;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Stopwatch total = Stopwatch.StartNew(), play = new();
        MahjongEngine game = null;
        MortalMatchAdapter adapter = null;
        ExperimentMatchRecorder recorder = null;
        Dictionary<int, MortalBridgeClient> clients = new();
        int modelDecisions = 0, localDecisions = 0, rounds = 0, reaches = 0, calls = 0, inferenceCalls = 0;
        double startupSeconds = 0, inferenceSeconds = 0;
        Dictionary<int, JsonObject> Runtimes() => clients.ToDictionary(pair => pair.Key, pair => pair.Value.RuntimeInfo);
        object Timing() => new
        {
            startupSeconds, playSeconds = play.Elapsed.TotalSeconds, recordWriteSeconds = recorder?.WriteSeconds ?? 0,
            totalBeforeCleanupSeconds = total.Elapsed.TotalSeconds,
            decisionsPerPlaySecond = play.Elapsed.TotalSeconds > 0 ? modelDecisions / play.Elapsed.TotalSeconds : 0,
            inferenceCalls, inferenceSeconds,
            averageInferenceMilliseconds = inferenceCalls > 0 ? inferenceSeconds * 1000 / inferenceCalls : 0
        };
        void CountInference(string reaction)
        {
            if (reaction == null) { return; }
            JsonNode node = JsonNode.Parse(reaction);
            if (node?["meta"]?["eval_time_ns"] is JsonNode elapsed)
            {
                inferenceSeconds += elapsed.GetValue<double>() / 1_000_000_000; inferenceCalls++;
            }
        }
        try
        {
            string Value(string name, string fallback)
            {
                int index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
            }
            int[] seats = Value("--seats", "0").Split(',').Select(int.Parse).Distinct().ToArray();
            if (seats.Length == 0 || seats.Any(seat => seat < 0 || seat > 3)) { throw new ArgumentException("--seats must list absolute seats 0..3."); }
            int maxRounds = int.Parse(Value("--rounds", "1"));
            if (maxRounds < 1 || maxRounds > 100) { throw new ArgumentException("--rounds must be 1..100."); }
            string purpose = Value("--purpose", "experiment");
            if (!new[] { "experiment", "benchmark", "validation" }.Contains(purpose)) { throw new ArgumentException("Unknown recording purpose."); }
            long seedNumber = long.Parse(Value("--seed", "1"));
            byte[] seed = ExperimentSeeds.FromNumber(seedNumber);
            string curseText = Value("--curses", "");
            string curseRuleVersion = Value("--curse-rule-version", "v1").ToLowerInvariant();
            if (curseRuleVersion != "v1" && curseRuleVersion != "v2") { throw new ArgumentException("--curse-rule-version must be v1 or v2."); }
            if (curseText.Length == 0 && curseRuleVersion == "v2") { throw new ArgumentException("v2 requires --curses."); }
            DemonCurseOptions curses = curseText.Length == 0 ? null : new DemonCurseOptions(
                curseText.Split(',').Select(value => (DemonCurse)int.Parse(value)).ToArray(), recordAudit: args.Contains("--audit"),
                rulesVersion: curseRuleVersion == "v1" ? DemonCurseRulesVersion.V1 : DemonCurseRulesVersion.V2);
            MortalBridgeOptions options = new()
            {
                PythonPath = Value("--python", new MortalBridgeOptions().PythonPath),
                ModelDirectory = Value("--model-dir", new MortalBridgeOptions().ModelDirectory),
                BridgeScriptPath = Value("--bridge", ""), NativeModuleDirectory = Value("--native-dir", ""),
                Threads = int.Parse(Value("--threads", "2")), Device = Value("--device", "cpu")
            };
            game = new(new MatchSettingData(MatchType.FourMahjongSouth),
                Enumerable.Range(0, 4).Select(seat => new UserProfile(seats.Contains(seat) ? "Mortal-" + seat : "Local-" + seat)).ToArray(), seed, curses);
            string output = Value("--output", "experiments");
            if (output.Length > 0) { recorder = new(output, game, seedNumber, seed, purpose, seats, curses, options); }
            adapter = new(game);
            foreach (int seat in seats)
            {
                MortalBridgeClient client = new(seat, options); clients.Add(seat, client);
                await adapter.AttachModelAsync(seat, client);
            }
            startupSeconds = total.Elapsed.TotalSeconds; play.Start();
            Console.WriteLine($"Ready: startupSeconds={startupSeconds:F3}; matchId={game.GetState().MatchId}");
            while (rounds < maxRounds)
            {
                int operations = 0;
                while (adapter.GetState().OperationStage != MatchOperationStage.RoundEnded)
                {
                    if (++operations > 2000) { throw new InvalidOperationException("Round operation limit exceeded."); }
                    int seat = adapter.GetState().PendingSeats[0];
                    MortalStepResult step;
                    if (seats.Contains(seat))
                    {
                        MortalDecision decision = await adapter.RecommendAsync(seat);
                        CountInference(decision.ReactionJson); CountInference(decision.FollowupJson);
                        if (!decision.Available)
                        {
                            string legal = JsonSerializer.Serialize(adapter.GetAvailableCommands(seat).Select(choice => new
                            {
                                type = choice.Action.PlayerAction.ToString(), pai = MjaiTiles.Format(choice.Action.TargetTile),
                                target = choice.Action.TargetPlayerNumber, consumed = choice.Action.Tiles.Select(MjaiTiles.Format)
                            }));
                            throw new InvalidOperationException($"Seat {seat}: {decision.Reason}; reaction={decision.ReactionJson}; followup={decision.FollowupJson}; legal={legal}. No automatic fallback was executed.");
                        }
                        if (decision.Command.Action.PlayerAction == PlayerAction.Riichi) { reaches++; }
                        if (new[] { PlayerAction.Chi, PlayerAction.Peng, PlayerAction.OpenKan, PlayerAction.AddedKan, PlayerAction.ClosedKan }.Contains(decision.Command.Action.PlayerAction)) { calls++; }
                        step = await adapter.SubmitModelAsync(decision); modelDecisions++;
                    }
                    else
                    {
                        var choices = adapter.GetAvailableCommands(seat);
                        MatchCommand command = choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.Skip)
                            ?? choices.FirstOrDefault(choice => choice.Action.PlayerAction == PlayerAction.TakeCut)
                            ?? choices.First(choice => choice.Action.PlayerAction == PlayerAction.HandCut);
                        step = await adapter.SubmitAsync(seat, command); localDecisions++;
                    }
                    recorder?.RecordStep(step.Feedback);
                    if (!step.Feedback.Accepted || step.Feedback.UsedDefault || step.ModelErrors.Count != 0)
                    {
                        throw new InvalidOperationException("Step failed: " + step.Feedback.Reason + " " + JsonSerializer.Serialize(step.ModelErrors));
                    }
                }
                rounds++;
                MatchState state = adapter.GetState();
                Console.WriteLine($"Round {rounds}: {state.Outcome}; scores={string.Join(',', state.State.PlayerPoint)}; records={adapter.GetRecords().Count}");
                if (state.MatchEnded || rounds == maxRounds) { break; }
                MortalStepResult next = await adapter.StartNextRoundAsync();
                if (next.Feedback.Accepted) { recorder?.RecordRoundStart(); }
                if (!next.Feedback.Accepted || next.ModelErrors.Count != 0)
                {
                    throw new InvalidOperationException("Next-round synchronization failed: " + next.Feedback.Reason + " " + JsonSerializer.Serialize(next.ModelErrors));
                }
            }
            play.Stop(); recorder?.ExportProtocol(adapter, seats);
            MatchState final = adapter.GetState();
            string status = final.MatchEnded ? "completed" : "truncated";
            recorder?.Finish(status, Timing(), Runtimes());
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                result = "pass", status, purpose, rounds, modelDecisions, localDecisions, reaches, calls, seats, seedNumber,
                seedEncoding = ExperimentSeeds.EncodingVersion, device = options.Device,
                curses = curses?.CursesBySeat.Select(curse => (int)curse).ToArray(),
                curseRuleVersion = curses == null ? null : curseRuleVersion,
                curseStatistics = game.GetCurseStatistics(), matchEnded = final.MatchEnded,
                ranking = final.MatchEnded ? final.Settlement.Ranking : null, scores = final.State.PlayerPoint,
                kyotaku = final.State.RiichiStickCount, timing = Timing(), recordDirectory = recorder?.DirectoryPath
            }));
            return 0;
        }
        catch (Exception exception)
        {
            play.Stop();
            try
            {
                if (adapter != null) { recorder?.ExportProtocol(adapter, clients.Keys); }
                recorder?.Finish("failed", Timing(), Runtimes(), exception.ToString());
            }
            catch (Exception recordError) { Console.Error.WriteLine("Recording finalization failed: " + recordError); }
            Console.Error.WriteLine(exception);
            Console.WriteLine(JsonSerializer.Serialize(new { result = "error", status = "failed", rounds, modelDecisions,
                timing = Timing(), recordDirectory = recorder?.DirectoryPath, error = exception.ToString() }));
            return 1;
        }
        finally
        {
            if (adapter != null) { await adapter.DisposeAsync(); }
            recorder?.Dispose();
        }
    }
}
