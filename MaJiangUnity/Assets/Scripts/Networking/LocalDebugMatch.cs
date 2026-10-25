using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MaJiangLib
{
    /// <summary>单人四座调试：一个回环服务端、四个真实TCP客户端；所有业务由调用线程Pump。</summary>
    public sealed class LocalDebugMatch
    {
        private readonly Stopwatch transportClock = Stopwatch.StartNew();
        private readonly CancellationTokenSource lifetime = new();
        private readonly LanRoomClient[] clients = new LanRoomClient[4];
        private readonly NetworkDecisionInput[] inputs = new NetworkDecisionInput[4];
        private Task connecting = Task.CompletedTask;
        private Task stopping;
        private int joining;
        private bool readySent;
        private bool startSent;
        private long lastPoll;
        private long milliseconds;
        public LanRoomServer Server { get; private set; }
        public long NowMilliseconds => milliseconds;
        public bool Ready => stopping == null && Server?.Match != null && clients.All(client => client?.IsMatchSynchronized == true);
        public string Error { get; private set; }
        public string Status => Error ?? (Ready ? "单机四座已就绪" : $"正在建立本机四座连接（{Math.Min(joining, 4)}/4）");

        public void Start()
        {
            if (Server != null || stopping != null) { throw new InvalidOperationException("Create a new debug match to restart."); }
            Server = new LanRoomServer(() => milliseconds);
            try { Server.Start(IPAddress.Loopback, 0); }
            catch (Exception exception) { Error = exception.Message; throw; }
            for (int seat = 0; seat < 4; seat++)
            {
                clients[seat] = new LanRoomClient();
                inputs[seat] = new NetworkDecisionInput(clients[seat]);
            }
            ConnectNext();
        }

        public IMatchDecisionInput GetInput(int seat)
        {
            if (seat < 0 || seat >= 4) { throw new ArgumentOutOfRangeException(nameof(seat)); }
            return inputs[ClientIndex(seat)];
        }
        // 房间开局会随机分配座位，不能把连接顺序当作牌局座位。
        private int ClientIndex(int seat)
        {
            int found = Array.FindIndex(clients, client => client?.Match.Current?.SelfSeat == seat);
            return found >= 0 ? found : seat;
        }
        public string SeatError(int seat) => clients[ClientIndex(seat)]?.LastError;
        public bool SeatConnected(int seat) => clients[ClientIndex(seat)]?.IsMatchSynchronized == true;

        private void ConnectNext()
        {
            connecting = clients[joining].ConnectAsync(IPAddress.Loopback, Server.Port, $"调试玩家 {joining + 1}",
                joining == 0 ? Server.HostJoinToken : Guid.Empty, lifetime.Token);
        }

        public void Pump()
        {
            if (Server == null || stopping != null || Error != null) { return; }
            Server.Pump();
            foreach (LanRoomClient client in clients) { client.Pump(); }
            if (connecting.IsFaulted) { Error = connecting.Exception.GetBaseException().Message; return; }
            if (Server.LastError != null) { Error = Server.LastError.Message; return; }
            // 对局时间可以暂停，TCP心跳仍按真实时间运行。请求房间快照保证服务端持续回包。
            long realNow = transportClock.ElapsedMilliseconds;
            if (realNow - lastPoll >= 1000)
            {
                foreach (LanRoomClient client in clients.Where(client => client.IsJoined)) { client.RequestState(); }
                lastPoll = realNow;
            }
            if (joining < 4)
            {
                if (!clients[joining].IsJoined) { return; }
                joining++;
                if (joining < 4) { ConnectNext(); return; }
            }
            if (!readySent)
            {
                foreach (LanRoomClient client in clients) { client.SetReady(true); }
                readySent = true;
            }
            RoomSnapshot room = clients[0].Snapshot;
            if (!startSent && room?.Members.Count == 4 && room.Members.All(member => member.Ready))
            {
                clients[0].StartMatch(); startSent = true;
            }
        }

        /// <summary>仅调试时钟推进；超时、默认动作、结算与换局仍由原服务端负责。</summary>
        public void AdvanceTime(long amount)
        {
            if (amount < 0 || amount > 60000) { throw new ArgumentOutOfRangeException(nameof(amount)); }
            milliseconds = checked(milliseconds + amount);
        }
        public bool NextRound()
        {
            MatchJournal match = Server?.Match;
            if (match?.OperationStage != MatchOperationStage.RoundEnded || match.Settlement == null || match.Settlement.MatchEnded) { return false; }
            AdvanceTime(Math.Max(0, match.NextRoundMilliseconds - milliseconds));
            return true;
        }
        public Task StopAsync()
        {
            if (stopping != null) { return stopping; }
            lifetime.Cancel();
            stopping = Task.WhenAll(clients.Where(client => client != null).Select(client => client.CloseAsync())
                .Concat(new[] { Server?.StopAsync() ?? Task.CompletedTask, ObserveConnect() }));
            return stopping;
        }
        private async Task ObserveConnect()
        {
            try { await connecting.ConfigureAwait(false); }
            catch (Exception) { /* 关闭时观察连接失败，运行期错误由Pump报告。 */ }
        }
    }
}
