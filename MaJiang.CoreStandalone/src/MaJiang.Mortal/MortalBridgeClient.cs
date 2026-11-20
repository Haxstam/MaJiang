using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace MaJiang.Mortal
{
    /// <summary>一名玩家的有状态模型会话；每发一条事件必须得到一个响应封装。</summary>
    public interface IMjaiClient : IAsyncDisposable
    {
        /// <summary>返回原生 mjai 建议；null 为无建议，type:none 为明确跳过。</summary>
        Task<JsonObject> ReactAsync(JsonObject gameEvent, CancellationToken cancellationToken = default);
    }

    /// <summary>模型进程配置；进程等待期限与麻将思考时间无关。</summary>
    public sealed class MortalBridgeOptions
    {
        public string PythonPath { get; set; } = @"D:\miniconda3\envs\mortal\python.exe";
        public string ModelDirectory { get; set; } = @"G:\Documents\Codex\2026-10-24\qing-n\outputs\Mortal-S42";
        /// <summary>可选的项目本地兼容桥接器；为空时使用部署目录的原桥接器。</summary>
        public string BridgeScriptPath { get; set; }
        /// <summary>与所选Python ABI兼容的 libriichi.pyd 所在目录。</summary>
        public string NativeModuleDirectory { get; set; }
        public string Device { get; set; } = "cpu";
        public int Threads { get; set; } = 2;
        public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromSeconds(60);
    }

    /// <summary>通过所选JSONL桥接器调用现有模型，不修改其代码、权重或部署目录。</summary>
    public sealed class MortalBridgeClient : IMjaiClient
    {
        private readonly Process process;
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly TimeSpan timeout;
        private readonly StringBuilder errors = new();
        private JsonObject runtimeInfo;
        private bool broken;
        private bool disposed;
        /// <summary>桥接器明确报告的实际运行设备/版本；旧桥接器未报告时为null。</summary>
        public JsonObject RuntimeInfo { get { lock (errors) { return (JsonObject)runtimeInfo?.DeepClone(); } } }

        public MortalBridgeClient(int seat, MortalBridgeOptions options = null)
        {
            options ??= new MortalBridgeOptions();
            if (seat < 0 || seat > 3) { throw new ArgumentOutOfRangeException(nameof(seat)); }
            if (options.Threads < 1 || options.ResponseTimeout <= TimeSpan.Zero || (options.Device != "cpu" && options.Device != "cuda"))
            {
                throw new ArgumentException("Invalid model options.", nameof(options));
            }
            string bridge = string.IsNullOrWhiteSpace(options.BridgeScriptPath)
                ? Path.Combine(options.ModelDirectory, "mjai_bridge.py") : Path.GetFullPath(options.BridgeScriptPath);
            if (!File.Exists(options.PythonPath) || !File.Exists(bridge)) { throw new FileNotFoundException("Python or the selected bridge script was not found. Configure the local model paths."); }
            timeout = options.ResponseTimeout;
            ProcessStartInfo start = new(options.PythonPath)
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string argument in new[] { "-B", "-u", bridge, "--seat", seat.ToString(), "--device", options.Device, "--threads", options.Threads.ToString() })
            {
                start.ArgumentList.Add(argument);
            }
            start.Environment["PYTHONDONTWRITEBYTECODE"] = "1";
            start.Environment["MAJIANG_MORTAL_MODEL_DIR"] = Path.GetFullPath(options.ModelDirectory);
            if (!string.IsNullOrWhiteSpace(options.NativeModuleDirectory))
            {
                start.Environment["MAJIANG_LIBRIICHI_DIR"] = Path.GetFullPath(options.NativeModuleDirectory);
            }
            process = new Process { StartInfo = start };
            process.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) { return; }
                lock (errors)
                {
                    errors.AppendLine(data.Data);
                    if (errors.Length > 8192) { errors.Remove(0, errors.Length - 8192); }
                    const string prefix = "Mortal runtime: ";
                    if (data.Data.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        try { runtimeInfo = JsonNode.Parse(data.Data.Substring(prefix.Length)) as JsonObject; }
                        catch (System.Text.Json.JsonException) { }
                    }
                }
            };
            process.Start(); process.BeginErrorReadLine();
        }

        public async Task<JsonObject> ReactAsync(JsonObject gameEvent, CancellationToken cancellationToken = default)
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                if (disposed || broken) { throw new InvalidOperationException("Model session must be replaced and replayed before reuse."); }
                using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(timeout);
                await process.StandardInput.WriteLineAsync(gameEvent.ToJsonString().AsMemory(), deadline.Token);
                await process.StandardInput.FlushAsync(deadline.Token);
                string line = await process.StandardOutput.ReadLineAsync(deadline.Token);
                if (line == null) { throw new IOException("Model bridge closed stdout. " + ErrorText()); }
                JsonObject envelope = JsonNode.Parse(line) as JsonObject ?? throw new IOException("Model response is not an object.");
                if (envelope["ok"]?.GetValue<bool>() != true)
                {
                    throw new IOException("Model bridge requested a reset: " + envelope["error"]?.GetValue<string>());
                }
                JsonNode reaction = envelope["reaction"];
                if (!envelope.ContainsKey("reaction") || (reaction != null && reaction is not JsonObject)) { throw new IOException("Malformed model response envelope."); }
                return (JsonObject)reaction?.DeepClone();
            }
            catch
            {
                broken = true; Stop(); throw;
            }
            finally { gate.Release(); }
        }

        private string ErrorText() { lock (errors) { return errors.ToString(); } }
        private void Stop()
        {
            try { if (!process.HasExited) { process.Kill(entireProcessTree: true); } }
            catch (InvalidOperationException) { }
        }

        public async ValueTask DisposeAsync()
        {
            await gate.WaitAsync();
            try
            {
                if (disposed) { return; }
                disposed = true;
                try { process.StandardInput.Close(); } catch (IOException) { }
                Stop(); await process.WaitForExitAsync(); process.Dispose();
            }
            finally { gate.Release(); }
        }
    }
}
