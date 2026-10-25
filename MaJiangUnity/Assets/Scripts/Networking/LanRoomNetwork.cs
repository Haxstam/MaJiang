using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MaJiangLib
{
    internal sealed class NetworkInbox
    {
        internal sealed class Entry
        {
            public NetworkConnection Connection;
            public NetworkMessage Message;
            public Exception Error;
        }
        private readonly ConcurrentQueue<Entry> entries = new();
        private int count;
        public bool Post(NetworkConnection connection, NetworkMessage message, Exception error = null)
        {
            if (Interlocked.Increment(ref count) > 256) { Interlocked.Decrement(ref count); return false; }
            entries.Enqueue(new Entry { Connection = connection, Message = message, Error = error });
            return true;
        }
        public bool TryRead(out Entry entry)
        {
            if (!entries.TryDequeue(out entry)) { return false; }
            Interlocked.Decrement(ref count);
            return true;
        }
    }

    /// <summary>后台只收发字节；Pump在调用者线程串行推进房间并触发业务事件。</summary>
    public sealed class LanRoomServer
    {
        private sealed class MatchCursor
        {
            public Guid PlayerId;
            public Guid RoundId;
            public long LastSent = -1;
            public long LastAcknowledged = -1;
            public bool ForceSnapshot = true;
            public bool ReplySync;
        }
        private readonly System.Collections.Generic.Dictionary<Guid, MatchCursor> matchCursors = new();
        private readonly ConcurrentDictionary<Guid, NetworkConnection> connections = new();
        private readonly ConcurrentDictionary<Guid, NetworkConnection> allConnections = new();
        private readonly System.Collections.Generic.HashSet<Guid> joinedConnections = new();
        private NetworkInbox inbox = new();
        private TcpListener listener;
        private Task acceptTask = Task.CompletedTask;
        private Task stopping = Task.CompletedTask;
        private volatile bool running;
        private long lastBroadcastVersion = -1;
        private readonly Func<long> clock;
        private double lastHeartbeat;

        public RoomService Room { get; private set; }
        public MatchJournal Match { get; private set; }
        public MatchSession Session { get; private set; }
        public AuthoritativeMatchMachine Machine => Session?.Machine;
        public int MatchSnapshotsSent { get; private set; }
        public int MatchLogBatchesSent { get; private set; }
        public Guid HostJoinToken { get; private set; }
        public int Port { get; private set; }
        public bool IsRunning => running;
        public int ConnectionCount => connections.Count;
        public Exception LastError { get; private set; }
        public event Action<RoomSnapshot> MatchStarted;

        /// <summary>默认使用单调毫秒时钟；注入时钟必须非负且单调，用于确定性验证超时、重连和换局。</summary>
        public LanRoomServer(Func<long> clock = null)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            this.clock = clock ?? (() => stopwatch.ElapsedMilliseconds);
        }

        public void Start(IPAddress address, int port)
        {
            if (running || !stopping.IsCompleted) { throw new InvalidOperationException("Server is already running or stopping."); }
            listener = new TcpListener(address, port);
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            HostJoinToken = Guid.NewGuid();
            Room = new RoomService(HostJoinToken);
            Match = null;
            Session = null;
            matchCursors.Clear(); MatchSnapshotsSent = 0; MatchLogBatchesSent = 0;
            LastError = null;
            inbox = new NetworkInbox();
            joinedConnections.Clear();
            lastBroadcastVersion = -1;
            lastHeartbeat = clock() / 1000.0;
            running = true;
            acceptTask = AcceptLoopAsync();
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                while (running)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    if (!running || connections.Count >= 16) { client.Close(); continue; }
                    NetworkConnection connection = new(client,
                        (source, message) => inbox.Post(source, message),
                        (source, error) => inbox.Post(source, null, error));
                    connections[connection.Id] = connection;
                    allConnections[connection.Id] = connection;
                    _ = connection.Completion.ContinueWith(task => allConnections.TryRemove(connection.Id, out _), TaskScheduler.Default);
                }
            }
            catch (Exception exception)
            {
                if (running) { LastError = exception; running = false; }
            }
        }

        public void Pump()
        {
            if (Room == null) { return; }
            if (!running && Room.Phase != RoomPhase.Closed) { _ = StopAsync(); return; }
            double now = clock() / 1000.0;
            Session?.Tick();
            for (int count = 0; count < 256 && inbox.TryRead(out NetworkInbox.Entry entry); count++)
            {
                if (!connections.ContainsKey(entry.Connection.Id)) { continue; }
                if (entry.Message == null)
                {
                    Detach(entry.Connection, now);
                    continue;
                }
                try { Handle(entry.Connection, entry.Message, now); }
                catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is ArgumentException || exception is OverflowException)
                {
                    entry.Connection.Close(exception);
                    Detach(entry.Connection, now);
                }
            }
            foreach (NetworkConnection connection in connections.Values)
            {
                if (connection.IsClosed || (!joinedConnections.Contains(connection.Id) && connection.AgeSeconds >= 10)
                    || connection.SecondsSinceReceive >= 30)
                {
                    connection.Close();
                    Detach(connection, now);
                }
            }
            Room.ExpireReservations(now);
            RoomSnapshot snapshot = Room.Snapshot();
            if (snapshot.Version != lastBroadcastVersion)
            {
                byte[] payload = RoomMessageCodec.EncodeSnapshot(snapshot);
                foreach (Guid id in joinedConnections.ToArray())
                {
                    if (connections.TryGetValue(id, out NetworkConnection connection)) { connection.Send(RoomMessageType.RoomState, payload); }
                }
                lastBroadcastVersion = snapshot.Version;
            }
            SynchronizeMatch(snapshot);
            if (running && now - lastHeartbeat >= 1)
            {
                using RoomPayloadWriter heartbeat = new(); heartbeat.Int64(clock());
                foreach (NetworkConnection connection in connections.Values) { connection.Send(RoomMessageType.Heartbeat, heartbeat.ToArray()); }
                lastHeartbeat = now;
            }
            if (Room.Phase == RoomPhase.Closed && running) { _ = StopAsync(); }
        }

        private void Handle(NetworkConnection connection, NetworkMessage message, double now)
        {
            using RoomPayloadReader reader = new(message.Payload);
            string result;
            switch (message.Type)
            {
                case RoomMessageType.Join:
                    string name = reader.Text(48); Guid resumeToken = reader.Guid(); Guid hostProof = reader.Guid(); reader.Complete();
                    RoomJoinResult joined = Room.Join(connection.Id, name, resumeToken, hostProof);
                    using (RoomPayloadWriter reply = new())
                    {
                        reply.Bool(joined.Accepted); reply.Text(joined.Reason, 96); reply.Guid(Room.RoomId);
                        reply.Guid(joined.PlayerId); reply.Guid(joined.ResumeToken);
                        connection.Send(RoomMessageType.Joined, reply.ToArray());
                    }
                    if (joined.Accepted)
                    {
                        joinedConnections.Add(connection.Id);
                        matchCursors[connection.Id] = new MatchCursor { PlayerId = joined.PlayerId };
                        if (connections.TryGetValue(joined.ReplacedConnectionId, out NetworkConnection previous))
                        {
                            previous.Close();
                            Detach(previous, now);
                        }
                    }
                    return;
                case RoomMessageType.Heartbeat:
                    reader.Complete(); return;
                case RoomMessageType.SetReady:
                    bool ready = reader.Bool(); reader.Complete(); result = Room.SetReady(connection.Id, ready); break;
                case RoomMessageType.StartMatch:
                    reader.Complete(); result = Room.StartMatch(connection.Id);
                    if (result == "Ok")
                    {
                        RoomSnapshot room = Room.Snapshot();
                        MainMatchControl core = new();
                        core.ServerFourMatchInit(new MatchSettingData(room.MatchType), room.Members.OrderBy(member => member.Seat).Select(member => new UserProfile(member.Name)).ToArray());
                        core.DoraPointerList.Add(core.PrimeTileList[0]);
                        core.CurrentStageType = StageType.DiscardStage;
                        Match = new MatchJournal(core, clock);
                        Session = new MatchSession(Match, clock);
                        MatchStarted?.Invoke(room);
                    }
                    break;
                case RoomMessageType.SetMatchType:
                    MatchType type = (MatchType)reader.Byte(); reader.Complete(); result = Room.SetMatchType(connection.Id, type); break;
                case RoomMessageType.Leave:
                    reader.Complete(); result = Room.Leave(connection.Id, now); break;
                case RoomMessageType.RequestState:
                    reader.Complete();
                    if (joinedConnections.Contains(connection.Id)) { connection.Send(RoomMessageType.RoomState, RoomMessageCodec.EncodeSnapshot(Room.Snapshot())); }
                    result = joinedConnections.Contains(connection.Id) ? "Ok" : "NotJoined"; break;
                case RoomMessageType.MatchAck:
                case RoomMessageType.RequestMatchSync:
                    Guid matchId = reader.Guid(); Guid roundId = reader.Guid(); int generation = reader.Int32(); long sequence = reader.Int64();
                    bool force = message.Type == RoomMessageType.RequestMatchSync && reader.Bool(); reader.Complete();
                    if (Match == null || !matchCursors.TryGetValue(connection.Id, out MatchCursor cursor)) { return; }
                    bool sameRound = matchId == Match.MatchId && roundId == Match.RoundId && generation == Match.RoundGeneration;
                    if (message.Type == RoomMessageType.MatchAck)
                    {
                        if (sameRound && sequence >= -1 && sequence <= cursor.LastSent) { cursor.LastAcknowledged = Math.Max(cursor.LastAcknowledged, sequence); }
                        else if (sameRound && sequence > cursor.LastSent) { cursor.ForceSnapshot = true; }
                    }
                    else
                    {
                        cursor.ForceSnapshot = force || !sameRound || sequence < -1 || sequence > Match.LatestSequence;
                        cursor.ReplySync = true;
                        if (!cursor.ForceSnapshot) { cursor.LastSent = sequence; }
                    }
                    return;
                case RoomMessageType.MatchCommand:
                    MatchCommand command = MatchCommandCodec.Decode(message.Payload);
                    if (Machine == null || !matchCursors.TryGetValue(connection.Id, out MatchCursor playerCursor)) { return; }
                    RoomMember actingPlayer = Room.Snapshot().Members.FirstOrDefault(member => member.PlayerId == playerCursor.PlayerId && member.Connected);
                    if (actingPlayer == null) { return; }
                    MatchCommandResult decision = Machine.Submit(actingPlayer.Seat, command);
                    connection.Send(RoomMessageType.MatchCommandResult, MatchCommandCodec.EncodeResult(decision));
                    return;
                default:
                    throw new InvalidDataException("Message is not valid in the client-to-server direction.");
            }
            using RoomPayloadWriter response = new();
            response.UInt32(message.Id); response.Bool(result == "Ok"); response.Text(result, 96);
            connection.Send(RoomMessageType.Result, response.ToArray());
        }

        private void Detach(NetworkConnection connection, double now)
        {
            connections.TryRemove(connection.Id, out _);
            joinedConnections.Remove(connection.Id);
            matchCursors.Remove(connection.Id);
            Room.Disconnect(connection.Id, now);
        }

        public Task StopAsync()
        {
            if (!running && !stopping.IsCompleted) { return stopping; }
            running = false;
            listener?.Stop();
            Room?.Close();
            Match = null;
            Session = null;
            stopping = FinishStoppingAsync();
            return stopping;
        }

        private void SynchronizeMatch(RoomSnapshot room)
        {
            if (Match == null || room.Phase != RoomPhase.Playing) { return; }
            foreach (Guid connectionId in joinedConnections.ToArray())
            {
                if (!connections.TryGetValue(connectionId, out NetworkConnection connection) || !matchCursors.TryGetValue(connectionId, out MatchCursor cursor)) { continue; }
                RoomMember member = room.Members.FirstOrDefault(player => player.PlayerId == cursor.PlayerId);
                if (member == null || !member.Connected) { continue; }
                bool snapshot = cursor.ForceSnapshot || cursor.RoundId != Match.RoundId;
                MatchLogBatch batch = snapshot ? null : Match.GetDelta(member.Seat, cursor.LastSent);
                if (snapshot || batch == null)
                {
                    VisibleMatchState checkpoint = Match.GetSnapshot(member.Seat);
                    if (connection.Send(RoomMessageType.MatchSnapshot, MatchSyncCodec.EncodeSnapshot(checkpoint)) == 0) { continue; }
                    cursor.RoundId = Match.RoundId; cursor.LastSent = checkpoint.Sequence; cursor.LastAcknowledged = -1; cursor.ForceSnapshot = false;
                    cursor.ReplySync = false;
                    MatchSnapshotsSent++;
                }
                else if (batch.Records.Count != 0 || cursor.ReplySync)
                {
                    if (connection.Send(RoomMessageType.MatchLog, MatchSyncCodec.EncodeBatch(batch)) == 0) { continue; }
                    if (batch.Records.Count != 0) { cursor.LastSent = batch.Records[batch.Records.Count - 1].Sequence; }
                    cursor.ReplySync = false;
                    MatchLogBatchesSent++;
                }
            }
        }

        private async Task FinishStoppingAsync()
        {
            foreach (NetworkConnection connection in connections.Values) { connection.Close(); }
            await acceptTask.ConfigureAwait(false);
            NetworkConnection[] remaining = allConnections.Values.ToArray();
            foreach (NetworkConnection connection in remaining) { connection.Close(); }
            await Task.WhenAll(remaining.Select(connection => connection.Completion)).ConfigureAwait(false);
            connections.Clear();
        }
    }

    /// <summary>客户端会话。所有公开事件仅在Pump中触发；断线保留自己的恢复令牌。</summary>
    public sealed class LanRoomClient
    {
        private readonly NetworkInbox inbox = new();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private NetworkConnection connection;
        private double lastHeartbeat;
        private uint leaveRequest;
        private readonly object lifecycleLock = new();
        private readonly TimeSpan serverSilenceTimeout;
        private CancellationTokenSource connectCancellation;
        private Task connectCompletion = Task.CompletedTask;
        private bool matchSyncPending;
        private bool expectMatchCheckpoint;
        private long lastServerMilliseconds;
        private long clockReceiptTimestamp;
        public ClientMatchProjection Match { get; } = new();
        public event Action<VisibleMatchState> MatchChanged;
        public event Action<MatchCommandResult> CommandCompleted;
        public MatchCommandResult LastCommandResult { get; private set; }
        private VisibleMatchState commandView;
        private System.Collections.Generic.IReadOnlyList<MatchCommand> availableCommands = Array.Empty<MatchCommand>();
        public System.Collections.Generic.IReadOnlyList<MatchCommand> AvailableCommands
        {
            get
            {
                VisibleMatchState view = Match.Current;
                if (!IsMatchSynchronized || view == null) { return Array.Empty<MatchCommand>(); }
                if (!ReferenceEquals(commandView, view))
                {
                    commandView = view;
                    availableCommands = view.GetAvailableActions().Select(action => new MatchCommand
                    {
                        MatchId = view.MatchId, RoundId = view.RoundId, RoundGeneration = view.RoundGeneration, PhaseId = view.PhaseId,
                        CommandId = Guid.NewGuid(), Action = action
                    }).ToArray();
                }
                return availableCommands;
            }
        }
        public long RemainingActionMilliseconds => Match.Current?.RemainingMilliseconds((long)((Stopwatch.GetTimestamp() - clockReceiptTimestamp) * 1000.0 / Stopwatch.Frequency)) ?? 0;
        public long RemainingResultMilliseconds => Match.Current?.RemainingResultMilliseconds((long)((Stopwatch.GetTimestamp() - clockReceiptTimestamp) * 1000.0 / Stopwatch.Frequency)) ?? 0;
        public Guid PlayerId { get; private set; }
        public Guid ResumeToken { get; private set; }
        public Guid RoomId { get; private set; }
        public RoomSnapshot Snapshot { get; private set; }
        public bool IsJoined { get; private set; }
        /// <summary>恢复/补齐完成之前暂停新操作，避免把旧可见状态作为当前菜单。</summary>
        public bool IsMatchSynchronized => IsJoined && Match.Current != null && !expectMatchCheckpoint && !matchSyncPending && !Match.NeedsCheckpoint;
        public bool IsConnecting { get; private set; }
        public bool IsConnected => connection != null && !connection.IsClosed;
        public string LastError { get; private set; }
        public event Action<RoomSnapshot> StateChanged;
        public event Action<uint, bool, string> OperationCompleted;

        /// <summary>长时间未收到服务端任何消息则断开并保留恢复令牌；默认30秒。</summary>
        public LanRoomClient(TimeSpan? serverSilenceTimeout = null)
        {
            this.serverSilenceTimeout = serverSilenceTimeout ?? TimeSpan.FromSeconds(30);
            if (this.serverSilenceTimeout <= TimeSpan.Zero) { throw new ArgumentOutOfRangeException(nameof(serverSilenceTimeout)); }
        }

        public Task ConnectAsync(IPAddress address, int port, string name, Guid hostProof = default, CancellationToken cancellationToken = default)
        {
            if (address == null) { throw new ArgumentNullException(nameof(address)); }
            if (port < 1 || port > 65535) { throw new ArgumentOutOfRangeException(nameof(port)); }
            lock (lifecycleLock)
            {
                if (IsConnecting || IsConnected) { throw new InvalidOperationException("Client is already connected or connecting."); }
                IsConnecting = true;
                connectCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCompletion = ConnectCoreAsync(address, port, name, hostProof, connectCancellation);
                return connectCompletion;
            }
        }

        private async Task ConnectCoreAsync(IPAddress address, int port, string name, Guid hostProof, CancellationTokenSource attempt)
        {
            TcpClient tcp = null;
            try
            {
                tcp = new TcpClient(address.AddressFamily);
                using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(attempt.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                using CancellationTokenRegistration registration = deadline.Token.Register(tcp.Close);
                await tcp.ConnectAsync(address, port).ConfigureAwait(false);
                deadline.Token.ThrowIfCancellationRequested();
                lock (lifecycleLock)
                {
                    attempt.Token.ThrowIfCancellationRequested();
                    using RoomPayloadWriter writer = new();
                    writer.Text(name, 48); writer.Guid(ResumeToken); writer.Guid(hostProof);
                    connection = new NetworkConnection(tcp,
                        (source, message) => inbox.Post(source, message),
                        (source, error) => inbox.Post(source, null, error));
                    LastError = null;
                    IsJoined = false;
                    leaveRequest = 0;
                    matchSyncPending = false;
                    expectMatchCheckpoint = true;
                    lastHeartbeat = clock.Elapsed.TotalSeconds;
                    connection.Send(RoomMessageType.Join, writer.ToArray());
                }
            }
            catch
            {
                tcp?.Close();
                connection?.Close();
                connection = null;
                throw;
            }
            finally
            {
                lock (lifecycleLock)
                {
                    IsConnecting = false;
                    connectCancellation = null;
                    attempt.Dispose();
                }
            }
        }

        public void Pump()
        {
            for (int count = 0; count < 256 && inbox.TryRead(out NetworkInbox.Entry entry); count++)
            {
                if (!ReferenceEquals(entry.Connection, connection)) { continue; }
                if (entry.Message == null)
                {
                    IsJoined = false;
                    LastError = entry.Error?.Message ?? "Disconnected";
                    continue;
                }
                try { Handle(entry.Message); }
                catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is ArgumentException)
                {
                    LastError = exception.Message;
                    connection.Close(exception);
                    IsJoined = false;
                }
            }
            if (IsConnected && connection.SecondsSinceReceive >= serverSilenceTimeout.TotalSeconds)
            {
                TimeoutException error = new("Server heartbeat timeout.");
                LastError = error.Message; IsJoined = false; connection.Close(error);
            }
            if (connection != null && connection.IsClosed) { IsJoined = false; }
            if (IsConnected && clock.Elapsed.TotalSeconds - lastHeartbeat >= 1)
            {
                connection.Send(RoomMessageType.Heartbeat, Array.Empty<byte>());
                lastHeartbeat = clock.Elapsed.TotalSeconds;
            }
        }

        private void Handle(NetworkMessage message)
        {
            using RoomPayloadReader reader = new(message.Payload);
            switch (message.Type)
            {
                case RoomMessageType.Joined:
                    bool accepted = reader.Bool(); string reason = reader.Text(96); Guid roomId = reader.Guid();
                    Guid playerId = reader.Guid(); Guid token = reader.Guid(); reader.Complete();
                    if (accepted)
                    {
                        if (roomId == Guid.Empty || playerId == Guid.Empty || token == Guid.Empty) { throw new InvalidDataException("Invalid session identity."); }
                        RoomId = roomId; PlayerId = playerId; ResumeToken = token; IsJoined = true;
                    }
                    else { LastError = reason; IsJoined = false; }
                    OperationCompleted?.Invoke(0, accepted, reason);
                    break;
                case RoomMessageType.RoomState:
                    if (!IsJoined && leaveRequest != 0) { break; }
                    RoomSnapshot snapshot = RoomMessageCodec.DecodeSnapshot(message.Payload);
                    if (!IsJoined || snapshot.RoomId != RoomId) { throw new InvalidDataException("Snapshot belongs to another room."); }
                    if (Snapshot == null || Snapshot.RoomId != RoomId || snapshot.Version > Snapshot.Version)
                    {
                        Snapshot = snapshot;
                        StateChanged?.Invoke(snapshot);
                    }
                    break;
                case RoomMessageType.Result:
                    uint request = reader.UInt32(); bool success = reader.Bool(); string result = reader.Text(96); reader.Complete();
                    if (!success) { LastError = result; }
                    OperationCompleted?.Invoke(request, success, result);
                    if (success && request == leaveRequest)
                    {
                        ForgetSession();
                        connection.Close();
                    }
                    break;
                case RoomMessageType.Heartbeat:
                    long serverMilliseconds = reader.Int64(); reader.Complete();
                    if (serverMilliseconds < 0) { throw new InvalidDataException("Invalid server clock."); }
                    ReanchorClock(serverMilliseconds); break;
                case RoomMessageType.MatchSnapshot:
                    if (!IsJoined && leaveRequest != 0) { break; }
                    VisibleMatchState checkpoint = MatchSyncCodec.DecodeSnapshot(message.Payload);
                    RoomMember self = Snapshot?.Members.FirstOrDefault(member => member.PlayerId == PlayerId);
                    if (!IsJoined || self == null || self.Seat != checkpoint.SelfSeat) { throw new InvalidDataException("Snapshot receiver does not match the session."); }
                    if (Match.Install(checkpoint, matchSyncPending || expectMatchCheckpoint))
                    {
                        matchSyncPending = false; expectMatchCheckpoint = false; ReanchorClock(checkpoint.ServerMilliseconds); SendMatchAck(); MatchChanged?.Invoke(Match.Current);
                    }
                    break;
                case RoomMessageType.MatchLog:
                    if (!IsJoined && leaveRequest != 0) { break; }
                    if (!IsJoined) { throw new InvalidDataException("Match log received before joining."); }
                    MatchLogBatch batch = MatchSyncCodec.DecodeBatch(message.Payload);
                    if (Match.Apply(batch))
                    {
                        matchSyncPending = false; ReanchorClock(Match.Current.ServerMilliseconds); SendMatchAck(); MatchChanged?.Invoke(Match.Current);
                    }
                    else if (!matchSyncPending) { RequestMatchSync(Match.NeedsCheckpoint); }
                    break;
                case RoomMessageType.MatchCommandResult:
                    LastCommandResult = MatchCommandCodec.DecodeResult(message.Payload);
                    if (!LastCommandResult.Accepted) { LastError = LastCommandResult.Reason; }
                    CommandCompleted?.Invoke(LastCommandResult);
                    break;
                default:
                    throw new InvalidDataException("Message is not valid in the server-to-client direction.");
            }
        }

        public uint SetReady(bool ready)
        {
            using RoomPayloadWriter writer = new(); writer.Bool(ready);
            return Send(RoomMessageType.SetReady, writer.ToArray());
        }
        public uint StartMatch() => Send(RoomMessageType.StartMatch, Array.Empty<byte>());
        public uint RequestState() => Send(RoomMessageType.RequestState, Array.Empty<byte>());
        public uint SetMatchType(MatchType type)
        {
            using RoomPayloadWriter writer = new(); writer.Byte((byte)type);
            return Send(RoomMessageType.SetMatchType, writer.ToArray());
        }
        public uint LeaveRoom()
        {
            leaveRequest = Send(RoomMessageType.Leave, Array.Empty<byte>());
            if (leaveRequest != 0) { ForgetSession(); }
            return leaveRequest;
        }
        public uint RequestMatchSync(bool forceCheckpoint = false)
        {
            if (!IsJoined) { return 0; }
            using RoomPayloadWriter writer = new(); WriteMatchPosition(writer); writer.Bool(forceCheckpoint);
            uint request = Send(RoomMessageType.RequestMatchSync, writer.ToArray());
            if (request != 0) { matchSyncPending = true; }
            return request;
        }
        /// <summary>提交菜单绑定的原始局/阶段/命令编号，重发同一对象不会重新执行。</summary>
        public Guid SendCommand(MatchCommand command)
        {
            if (!IsMatchSynchronized || command == null) { return Guid.Empty; }
            return Send(RoomMessageType.MatchCommand, MatchCommandCodec.Encode(command)) != 0 ? command.CommandId : Guid.Empty;
        }
        private void SendMatchAck()
        {
            using RoomPayloadWriter writer = new(); WriteMatchPosition(writer); Send(RoomMessageType.MatchAck, writer.ToArray());
        }
        private void WriteMatchPosition(RoomPayloadWriter writer)
        {
            VisibleMatchState view = Match.Current;
            writer.Guid(view?.MatchId ?? Guid.Empty); writer.Guid(view?.RoundId ?? Guid.Empty);
            writer.Int32(view?.RoundGeneration ?? 0); writer.Int64(view?.Sequence ?? -1);
        }
        private void ReanchorClock(long milliseconds)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = clockReceiptTimestamp == 0 ? 0 : (long)((now - clockReceiptTimestamp) * 1000.0 / Stopwatch.Frequency);
            lastServerMilliseconds = Math.Max(lastServerMilliseconds + elapsed, milliseconds);
            Match.ReanchorClock(lastServerMilliseconds); clockReceiptTimestamp = now;
        }
        private uint Send(RoomMessageType type, byte[] payload) => IsJoined && IsConnected ? connection.Send(type, payload) : 0;

        public async Task CloseAsync(bool forgetSession = false)
        {
            NetworkConnection previous;
            Task attempt;
            lock (lifecycleLock)
            {
                connectCancellation?.Cancel();
                attempt = connectCompletion;
                previous = connection;
                connection = null;
                IsJoined = false;
                if (forgetSession) { ForgetSession(); }
            }
            if (previous != null) { await previous.CloseAsync().ConfigureAwait(false); }
            try { await attempt.ConfigureAwait(false); }
            catch (Exception) { /* ConnectAsync的调用者收到原异常；关闭仍须完成。 */ }
        }
        public void ForgetSession()
        {
            PlayerId = Guid.Empty; ResumeToken = Guid.Empty; RoomId = Guid.Empty; Snapshot = null; IsJoined = false;
            Match.Clear(); matchSyncPending = false; expectMatchCheckpoint = false; lastServerMilliseconds = 0; clockReceiptTimestamp = 0;
            commandView = null; availableCommands = Array.Empty<MatchCommand>(); LastCommandResult = null;
        }
    }
}
