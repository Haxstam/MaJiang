using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MaJiangLib;

namespace MaJiang.Mortal
{
    /// <summary>每场独立落盘的宿主记录器；成功动作、起手检查点、结算和隐藏审计分文件保存。</summary>
    public sealed class ExperimentMatchRecorder : IDisposable
    {
        private readonly MahjongEngine game;
        private readonly StreamWriter events;
        private readonly StreamWriter rounds;
        private readonly StreamWriter audit;
        private readonly JsonObject manifest;
        private readonly Stopwatch writeTime = new();
        private readonly HashSet<Guid> settled = new();
        private Guid currentRound;
        private long nextSequence;
        private int auditCount;
        private bool finished;
        private bool disposed;
        public string DirectoryPath { get; }
        public double WriteSeconds => writeTime.Elapsed.TotalSeconds;
        public int SettledRounds => settled.Count;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            IncludeFields = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        public ExperimentMatchRecorder(string root, MahjongEngine game, long seedNumber, byte[] seed, string purpose,
            IReadOnlyList<int> modelSeats, DemonCurseOptions curses, MortalBridgeOptions modelOptions)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            if (!new[] { "experiment", "benchmark", "validation" }.Contains(purpose)) { throw new ArgumentException("Unknown purpose.", nameof(purpose)); }
            if (modelOptions == null) { throw new ArgumentNullException(nameof(modelOptions)); }
            MatchState state = game.GetState();
            DirectoryPath = Path.Combine(Path.GetFullPath(root), "match-" + state.MatchId.ToString("N"));
            if (Directory.Exists(DirectoryPath)) { throw new IOException("A recording for this match already exists; it will not be overwritten."); }
            Directory.CreateDirectory(DirectoryPath);
            events = Open("events.jsonl"); rounds = Open("rounds.jsonl"); audit = Open("curse-audit.jsonl");
            manifest = new JsonObject
            {
                ["schemaVersion"] = 1, ["matchId"] = state.MatchId.ToString(), ["purpose"] = purpose,
                ["status"] = "running", ["eligibleForStatistics"] = false,
                ["startedAtUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                ["seedNumber"] = seedNumber, ["seedEncoding"] = ExperimentSeeds.EncodingVersion,
                ["seedHex"] = Convert.ToHexString(seed),
                ["modelSeats"] = JsonSerializer.SerializeToNode(modelSeats),
                ["curses"] = JsonSerializer.SerializeToNode(curses?.CursesBySeat.Select(value => (int)value).ToArray()),
                ["curseRuleVersion"] = curses == null ? null : curses.RulesVersion == DemonCurseRulesVersion.V1 ? "v1" : "v2",
                ["detailedCurseAudit"] = curses?.RecordAudit == true,
                ["modelPolicies"] = new JsonObject { ["kokushiRobClosedKan"] = MortalMatchAdapter.KokushiRobClosedKanPolicy },
                ["rules"] = JsonSerializer.SerializeToNode(state.State.MatchSettingData, JsonOptions),
                ["modelOptions"] = JsonSerializer.SerializeToNode(modelOptions, JsonOptions),
                ["coreAssemblySha256"] = HashFile(typeof(MahjongEngine).Assembly.Location),
                ["adapterAssemblySha256"] = HashFile(typeof(ExperimentMatchRecorder).Assembly.Location),
                ["bridgeSha256"] = HashFile(string.IsNullOrWhiteSpace(modelOptions.BridgeScriptPath)
                    ? Path.Combine(modelOptions.ModelDirectory, "mjai_bridge.py") : modelOptions.BridgeScriptPath),
                ["nativeSha256"] = string.IsNullOrWhiteSpace(modelOptions.NativeModuleDirectory) ? null : HashFile(Path.Combine(modelOptions.NativeModuleDirectory, "libriichi.pyd"))
            };
            string identity = Path.Combine(modelOptions.ModelDirectory, "model-identity.json");
            if (File.Exists(identity)) { manifest["modelIdentity"] = JsonNode.Parse(File.ReadAllText(identity)); }
            WriteManifest(); RecordRoundStart();
        }

        private StreamWriter Open(string name) => new(new FileStream(Path.Combine(DirectoryPath, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
        private static string HashFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return null; }
            using FileStream file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file));
        }

        /// <summary>包括13张暗手、庄家首摸、剩余牌山和王牌的可信检查点；不会交给模型。</summary>
        public void RecordRoundStart()
        {
            MatchState state = game.GetState();
            if (state.RoundId == currentRound) { throw new InvalidOperationException("This round has already been recorded."); }
            currentRound = state.RoundId; nextSequence = 0;
            Timed(() =>
            {
                Append(events, new { type = "round_start", checkpointSequence = state.Sequence, state });
                WriteRecords(game.GetRecords()); FlushAudit(); events.Flush(); audit.Flush();
            });
        }

        /// <summary>仅写本次已执行事实。模型同步失败时仍应先调用本方法保存核心执行结果。</summary>
        public void RecordStep(OperationFeedback feedback)
        {
            Timed(() =>
            {
                WriteRecords(feedback.Records); FlushAudit(); events.Flush(); rounds.Flush(); audit.Flush();
            });
        }

        private void WriteRecords(IReadOnlyList<MatchRecord> records)
        {
            foreach (MatchRecord record in records)
            {
                if (record.RoundId != currentRound || record.Sequence != nextSequence)
                {
                    throw new InvalidOperationException("Recording sequence gap or unexpected round identity.");
                }
                Append(events, new { type = "applied", record }); nextSequence++;
                if (record.Kind == MatchRecordKind.RoundSettled && settled.Add(record.RoundId))
                {
                    Append(rounds, new { roundId = record.RoundId, roundGeneration = record.RoundGeneration, settlement = record.Settlement });
                }
            }
        }

        private void FlushAudit()
        {
            foreach (CurseAuditRecord record in game.GetCurseAudit(auditCount))
            {
                Append(audit, new { index = auditCount++, record });
            }
        }

        /// <summary>保存玩家可见协议流以便重新接入模型；主统计记录使用独立的完整成功动作流。</summary>
        public void ExportProtocol(MortalMatchAdapter adapter, IEnumerable<int> seats)
        {
            Timed(() =>
            {
                foreach (int seat in seats) { File.WriteAllLines(Path.Combine(DirectoryPath, $"seat-{seat}.mjai.jsonl"), adapter.GetProtocolHistory(seat), new UTF8Encoding(false)); }
            });
        }

        /// <summary>只有正常终局且purpose为experiment的样本才可计入正式统计。</summary>
        public void Finish(string status, object timing, IReadOnlyDictionary<int, JsonObject> runtimes, string error = null)
        {
            if (finished) { return; }
            MatchState state = game.GetState();
            if (status != "failed" && status != (state.MatchEnded ? "completed" : "truncated")) { throw new ArgumentException("Status disagrees with final game state.", nameof(status)); }
            Timed(() =>
            {
                FlushAudit(); events.Flush(); rounds.Flush(); audit.Flush();
                manifest["status"] = status;
                manifest["eligibleForStatistics"] = status == "completed" && manifest["purpose"]?.GetValue<string>() == "experiment";
                manifest["finishedAtUtc"] = DateTimeOffset.UtcNow.ToString("O");
                manifest["matchEnded"] = state.MatchEnded;
                manifest["settledRounds"] = SettledRounds;
                manifest["scores"] = JsonSerializer.SerializeToNode(state.State.PlayerPoint);
                manifest["kyotaku"] = state.State.RiichiStickCount;
                manifest["ranking"] = state.MatchEnded ? JsonSerializer.SerializeToNode(state.Settlement.Ranking) : null;
                manifest["timing"] = JsonSerializer.SerializeToNode(timing, JsonOptions);
                manifest["modelRuntimes"] = JsonSerializer.SerializeToNode(runtimes, JsonOptions);
                manifest["curseStatistics"] = JsonSerializer.SerializeToNode(game.GetCurseStatistics(), JsonOptions);
                manifest["error"] = error;
                File.WriteAllText(Path.Combine(DirectoryPath, "final-state.json"), JsonSerializer.Serialize(state, JsonOptions), new UTF8Encoding(false));
                WriteManifest();
            });
            finished = true;
        }

        private void WriteManifest()
        {
            string temporary = Path.Combine(DirectoryPath, "match.json.pending");
            File.WriteAllText(temporary, manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
            File.Move(temporary, Path.Combine(DirectoryPath, "match.json"), true);
        }
        private static void Append(StreamWriter writer, object value) => writer.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        private void Timed(Action action) { writeTime.Start(); try { action(); } finally { writeTime.Stop(); } }
        public void Dispose()
        {
            if (disposed) { return; }
            disposed = true; events.Dispose(); rounds.Dispose(); audit.Dispose();
        }
    }
}
