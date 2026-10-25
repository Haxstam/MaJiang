using MaJiangLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MatchType = MaJiangLib.MatchType;

internal static partial class Program
{
    private static async Task<int> Main()
    {
        (string Name, Func<Task> Run)[] checks =
        {
            ("Fixed-frame fragmentation preserves marker bytes in payload", FramesRoundTrip),
            ("Corrupt frames, versions and duplicate messages are rejected", InvalidFrames),
            ("Half frames, concatenated frames and EOF are distinguished", StreamBoundaries),
            ("Payloads reject invalid UTF-8, booleans and trailing bytes", PayloadValidation),
            ("Room identity, reservations and host permissions", RoomRules),
            ("Only explicit host start advances four ready players", FourClientRoom),
            ("Reconnect restores identity and revokes the old connection", ReconnectTakeover),
            ("A departing player frees a waiting-room seat", LeaveAndRejoin),
            ("Concurrent sends use one complete ordered stream", ConcurrentSends),
            ("Partial-frame and partial-message reads expire", PartialReadTimeout),
            ("Malformed peers do not affect an existing room", MalformedPeer),
            ("Invalid message payloads close only the offending peer", MalformedPayload),
            ("Invalid server direction is handled by the client", MalformedServer),
            ("Disconnect cancels an in-flight connect", CancelConnecting),
            ("A bounded send queue closes a stalled connection", BoundedSendQueue),
            ("Shutdown closes pending reads and releases the port", StopAndRestart),
            ("Explicit host departure closes all peers", HostDeparture),
            ("Visible checkpoints contain only the receiver's concealed hand", SyncVisibility),
            ("Forty action records reconstruct the checkpoint exactly", SyncReplay),
            ("Duplicate and missing records cannot partially advance state", SyncSequenceRules),
            ("Private rulings survive replay without leaking to others", SyncPrivateState),
            ("Malformed updates recover through an atomic checkpoint", SyncRecovery),
            ("New round identities reject stale logs and checkpoints", SyncRoundIsolation),
            ("Large rivers and melds have explicit counts", SyncVariableLists),
            ("Old retained positions require a checkpoint", SyncRetention),
            ("Four TCP clients receive their own initial match state", SyncTcpInitialization),
            ("Normal TCP updates use logs and explicit checkpoints recover", SyncTcpUpdates),
            ("TCP reconnect preserves private state and the original deadline", SyncTcpReconnect),
            ("Replaying an old clock anchor never refunds thinking time", SyncClockMonotonic),
            ("A TCP log gap requests missing records without a checkpoint", SyncTcpGap),
            ("An explicit recovery corrects an impossible future sequence", SyncTcpFutureSequence),
            ("Published journal data cannot be rewritten through caller aliases", SyncSourceIsolation),
            ("Invalid action metadata does not enter the journal", SyncInvalidSource),
            ("Machine/client use the same pure action query", MachineSharedQueries),
            ("Hand discard preserves the draw and red identity", MachineTurnAndRedDiscard),
            ("Illegal action, duplicate id and stale phase are deterministic", MachineDefaultAndReplay),
            ("Authenticated seat and round gate every command", MachineAuthentication),
            ("Per-step clocks debit one shared long-think bank", MachineClocks),
            ("Responses wait for higher priority before pon beats chi", MachineCallPriority),
            ("An illegal response is skipped without applying a meld", MachineInvalidResponse),
            ("Chi blocks same-tile and suji kuikae", MachineChiRestriction),
            ("Pon consumes the exact chosen red/normal tiles", MachineRedPon),
            ("Double and triple ron are independent of packet order", MachineMultiRon),
            ("Dora alone is no yaku and missed shape causes furiten", MachineNoYakuAndFuriten),
            ("Temporary furiten clears on draw; riichi furiten remains", MachineFuritenExpiry),
            ("Riichi commits only after ron window and calls cancel ippatsu", MachineRiichiConfirmation),
            ("A ronned riichi discard does not charge a stick", MachineRiichiRonned),
            ("Closed kan draws rinshan and opens its dora", MachineClosedKan),
            ("Added-kan dora waits for discard and stays closed on rinshan win", MachineAddedKanDora),
            ("Robbing an added kan prevents its meld, draw and dora", MachineRobAddedKan),
            ("A new kan opens previous dora even when the new kan is robbed", MachineContinuousKanRob),
            ("Riichi ankan preserves every hand decomposition", MachineRiichiKan),
            ("A disconnected four-player game reaches exhaustive draw", MachineExhaustiveWall),
            ("Open kan takes rinshan and defers dora until discard", MachineOpenKan),
            ("The final rinshan discard can win with houtei alone", MachineLastRinshanDiscard),
            ("Response timeout passes a win and records furiten", MachineResponseTimeout),
            ("Four winds, fourth riichi and nine terminals end explicitly", MachineAbortiveDraws),
            ("Fourth kan allows rinshan and ron before abortive draw", MachineFourthKan),
            ("Open straight yaku ignores called tile storage order", MachineMeldYaku),
            ("Real TCP player commands enter the same authoritative machine", MachineTcpCommands),
            ("Four TCP clients complete and synchronize a full hand", MachineTcpFullHand),
            ("Round payments are applied once across ticks and retries", SettlementOnce),
            ("Next round keeps scores and retires old commands and machine", SettlementNextRound),
            ("Only winners and actual tenpai hands are publicly revealed", SettlementVisibility),
            ("Settlement codec bounds, ledger and alias checks", SettlementCodecChecks),
            ("Ura is revealed only for riichi and actually opened dora", SettlementUra),
            ("Final result persists without creating another round", SettlementFinal),
            ("Default play completes a whole south/west match", SettlementWholeMatch),
            ("Four TCP clients settle, reconnect and enter the next round", SettlementTcpNextRound),
            ("Recovery gates menus until checkpoint installation", RecoveryMenuGate),
            ("Reconnect after own deadline cannot refund or replay turn", RecoveryOwnDeadline),
            ("Answered claim survives reconnect, retry and priority resolution", RecoveryAnsweredClaim),
            ("Reconnect inside rob-kan window preserves pending kan and clock", RecoveryRobKan),
            ("A disconnected player returns directly to the new round", RecoveryAcrossRound),
            ("Client detects silent server while preserving resume identity", RecoveryHeartbeatTimeout),
            ("Solo debug maps shuffled seats to private TCP views and locks pending input", DebugSeatsAndCommands),
            ("Solo debug holds time, tests timeouts, completes a hand and enters next round", DebugFullHand),
            ("Solo debug shutdown cancels startup and allows a fresh session", DebugShutdown)
        };
        int failures = 0;
        foreach (var check in checks)
        {
            try { await check.Run(); Console.WriteLine($"PASS {check.Name}"); }
            catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL {check.Name}: {exception}"); }
        }
        Console.WriteLine($"{checks.Length - failures}/{checks.Length} network checks passed.");
        return failures == 0 ? 0 : 1;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
    private static async Task Until(Func<bool> condition, Action pump = null, int milliseconds = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            pump?.Invoke();
            if (condition()) { return; }
            await Task.Delay(3);
        }
        throw new TimeoutException("Expected state was not reached.");
    }

    private static Task FramesRoundTrip()
    {
        foreach (int length in new[] { 0, 1, 984, 985, 65536 })
        {
            byte[] payload = Enumerable.Range(0, length).Select(index => (byte)(index % 251)).ToArray();
            if (length > 1200)
            {
                Encoding.ASCII.GetBytes("\u0003EndPack").CopyTo(payload, 100);
                Encoding.ASCII.GetBytes("\u0002HaxMajP").CopyTo(payload, 1100);
            }
            byte[][] frames = NetworkFrameCodec.Encode(new(RoomMessageType.RoomState, 1, payload)).ToArray();
            NetworkMessageAssembler assembler = new();
            NetworkMessage decoded = null;
            for (int index = 0; index < frames.Length; index++)
            {
                Assert(frames[index].Length == 1024, "Non-fixed frame length.");
                decoded = assembler.AddFrame(frames[index]);
                Assert((decoded != null) == (index == frames.Length - 1), "Partial message was exposed.");
            }
            Assert(payload.SequenceEqual(decoded.Payload) && !assembler.HasPartialMessage, "Payload changed.");
        }
        return Task.CompletedTask;
    }

    private static Task InvalidFrames()
    {
        byte[] frame = NetworkFrameCodec.Encode(new(RoomMessageType.Heartbeat, 1, Array.Empty<byte>())).Single();
        byte[] damaged = (byte[])frame.Clone(); damaged[100] ^= 1;
        Throws<InvalidDataException>(() => NetworkFrameCodec.Validate(damaged));
        damaged = (byte[])frame.Clone(); damaged[8] = 1;
        Throws<InvalidDataException>(() => NetworkFrameCodec.Validate(damaged));
        NetworkMessageAssembler assembler = new(); assembler.AddFrame(frame);
        Throws<InvalidDataException>(() => assembler.AddFrame(frame));
        byte[][] fragments = NetworkFrameCodec.Encode(new(RoomMessageType.RoomState, 1, new byte[1200])).ToArray();
        Throws<InvalidDataException>(() => new NetworkMessageAssembler().AddFrame(fragments[1]));
        Throws<InvalidDataException>(() => NetworkFrameCodec.Encode(new(RoomMessageType.RoomState, 1, new byte[65537])).ToArray());
        return Task.CompletedTask;
    }

    private sealed class ChunkStream : MemoryStream
    {
        private readonly int chunk;
        public ChunkStream(byte[] data, int chunk) : base(data) { this.chunk = chunk; }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => base.ReadAsync(buffer, offset, Math.Min(count, chunk), token);
    }

    private static async Task StreamBoundaries()
    {
        byte[] first = NetworkFrameCodec.Encode(new(RoomMessageType.Heartbeat, 1, Array.Empty<byte>())).Single();
        byte[] second = NetworkFrameCodec.Encode(new(RoomMessageType.Heartbeat, 2, Array.Empty<byte>())).Single();
        foreach (int chunk in new[] { 1, 7, 1024, 2048 })
        {
            using ChunkStream stream = new(first.Concat(second).ToArray(), chunk);
            byte[] buffer = new byte[1024];
            Assert(await NetworkConnection.ReadFrameAsync(stream, buffer, default) && buffer.SequenceEqual(first), "First frame misaligned.");
            Assert(await NetworkConnection.ReadFrameAsync(stream, buffer, default) && buffer.SequenceEqual(second), "Second frame misaligned.");
            Assert(!await NetworkConnection.ReadFrameAsync(stream, buffer, default), "Boundary EOF not reported.");
        }
        using ChunkStream partial = new(first.Take(1023).ToArray(), 11);
        try { await NetworkConnection.ReadFrameAsync(partial, new byte[1024], default); }
        catch (EndOfStreamException) { return; }
        throw new InvalidOperationException("Truncated frame accepted.");
    }

    private static Task PayloadValidation()
    {
        using (RoomPayloadReader reader = new(new byte[] { 2 })) { Throws<InvalidDataException>(() => reader.Bool()); }
        using (RoomPayloadReader reader = new(new byte[] { 2, 0, 0xC3, 0x28 })) { Throws<DecoderFallbackException>(() => reader.Text(48)); }
        using (RoomPayloadReader reader = new(new byte[] { 1 })) { Throws<InvalidDataException>(() => reader.Complete()); }
        using (RoomPayloadReader reader = new(new byte[] { 5, 0, 1 })) { Throws<EndOfStreamException>(() => reader.Text(48)); }
        using (RoomPayloadWriter writer = new()) { Throws<InvalidDataException>(() => writer.Text(new string('界', 17), 48)); }
        return Task.CompletedTask;
    }

    private static Task RoomRules()
    {
        Guid hostProof = Guid.NewGuid(); RoomService room = new(hostProof);
        Guid[] sockets = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        RoomJoinResult[] players = new RoomJoinResult[4];
        for (int index = 0; index < 4; index++) { players[index] = room.Join(sockets[index], "同名", Guid.Empty, index == 2 ? hostProof : Guid.Empty); }
        Assert(players.All(player => player.Accepted) && players.Select(player => player.PlayerId).Distinct().Count() == 4, "Names were used as identity.");
        Assert(room.Snapshot().Members.Single(member => member.IsHost).PlayerId == players[2].PlayerId, "First remote visitor became host.");
        byte[] snapshot = RoomMessageCodec.EncodeSnapshot(room.Snapshot());
        Assert(players.All(player => snapshot.AsSpan().IndexOf(player.ResumeToken.ToByteArray()) < 0), "Resume secret leaked into room broadcast.");
        Assert(RoomMessageCodec.DecodeSnapshot(snapshot).Members.Count == 4, "Snapshot lost members.");
        Assert(!room.Join(Guid.NewGuid(), "Fifth", Guid.Empty, Guid.Empty).Accepted, "Fifth player entered.");
        Assert(room.StartMatch(sockets[0]) == "HostOnly" && room.StartMatch(sockets[2]) == "PlayersNotReady", "Start permissions bypassed.");
        foreach (Guid socket in sockets) { room.SetReady(socket, true); }
        Assert(room.Phase == RoomPhase.Waiting && room.Snapshot().CanStart, "Readiness auto-started match.");
        Assert(room.SetMatchType(sockets[2], MatchType.FourMahjongEast) == "Ok" && room.Snapshot().Members.All(member => !member.Ready), "Rule change kept old readiness.");
        room.Disconnect(sockets[0], 10);
        Guid restored = Guid.NewGuid();
        Assert(room.Join(restored, "ignored", players[0].ResumeToken, Guid.Empty).PlayerId == players[0].PlayerId, "Waiting reconnect changed identity.");
        room.Disconnect(restored, 11); room.ExpireReservations(40);
        Assert(room.Snapshot().Members.Count == 4, "Reservation expired too early.");
        room.ExpireReservations(41);
        Assert(room.Snapshot().Members.Count == 3 && !room.Join(Guid.NewGuid(), "bad", players[0].ResumeToken, Guid.Empty).Accepted, "Expired token restored an abandoned waiting seat.");
        return Task.CompletedTask;
    }

    private sealed class Fixture
    {
        public LanRoomServer Server;
        public List<LanRoomClient> Clients = new();
        public Fixture(LanRoomServer server = null) { Server = server ?? new(); Server.Start(IPAddress.Loopback, 0); }
        public void Pump() { Server.Pump(); foreach (LanRoomClient client in Clients) { client.Pump(); } }
        public async Task<LanRoomClient> Join(bool host = false)
        {
            LanRoomClient client = new(); Clients.Add(client);
            await client.ConnectAsync(IPAddress.Loopback, Server.Port, "同名", host ? Server.HostJoinToken : Guid.Empty);
            await Until(() => client.IsJoined && client.Snapshot != null, Pump);
            return client;
        }
        public async Task Cleanup()
        {
            await Task.WhenAll(Clients.Select(client => client.CloseAsync()));
            await Server.StopAsync();
        }
    }

    private static async Task FourClientRoom()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await fixture.Join(true);
            for (int index = 0; index < 3; index++) { await fixture.Join(); }
            await Until(() => fixture.Clients.All(client => client.Snapshot.Members.Count == 4), fixture.Pump);
            LanRoomClient fifth = new(); fixture.Clients.Add(fifth);
            await fifth.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "fifth");
            await Until(() => fifth.LastError == "RoomFull", fixture.Pump);
            Assert(!fifth.IsJoined, "Fifth client joined.");
            await fifth.CloseAsync();
            List<LanRoomClient> joined = fixture.Clients.Take(4).ToList();
            foreach (LanRoomClient client in joined) { client.SetReady(true); }
            await Until(() => joined.All(client => client.Snapshot.CanStart), fixture.Pump);
            Assert(fixture.Server.Room.Phase == RoomPhase.Waiting, "Four ready auto-started.");
            joined[1].StartMatch();
            await Until(() => joined[1].LastError == "HostOnly", fixture.Pump);
            joined[2].SetReady(false);
            await Until(() => !host.Snapshot.CanStart, fixture.Pump);
            host.StartMatch();
            await Until(() => host.LastError == "PlayersNotReady", fixture.Pump);
            joined[2].SetReady(true);
            await Until(() => host.Snapshot.CanStart, fixture.Pump);
            int starts = 0; fixture.Server.MatchStarted += _ => starts++;
            Dictionary<uint, bool> results = new();
            host.OperationCompleted += (id, success, _) => results[id] = success;
            host.StartMatch();
            await Until(() => joined.All(client => client.Snapshot.Phase == RoomPhase.Playing), fixture.Pump);
            uint duplicateStart = host.StartMatch();
            await Until(() => results.TryGetValue(duplicateStart, out bool success) && !success, fixture.Pump);
            Assert(starts == 1 && joined.All(client => client.Snapshot.Members.Select(member => member.Seat).Distinct().Count() == 4), "Duplicate start or invalid seats.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task ReconnectTakeover()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await fixture.Join(true);
            LanRoomClient old = await fixture.Join();
            await fixture.Join(); await fixture.Join();
            foreach (LanRoomClient client in fixture.Clients) { client.SetReady(true); }
            await Until(() => host.Snapshot.CanStart, fixture.Pump); host.StartMatch();
            await Until(() => old.Snapshot.Phase == RoomPhase.Playing, fixture.Pump);
            Guid playerId = old.PlayerId; Guid token = old.ResumeToken;
            int seat = old.Snapshot.Members.Single(member => member.PlayerId == playerId).Seat;
            await old.CloseAsync();
            await Until(() => host.Snapshot.Members.Any(member => member.PlayerId == playerId && !member.Connected), fixture.Pump);
            await old.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "changed name");
            await Until(() => old.IsJoined && old.Snapshot.Members.All(member => member.Connected), fixture.Pump);
            Assert(old.PlayerId == playerId && old.ResumeToken == token && old.Snapshot.Members.Single(member => member.PlayerId == playerId).Seat == seat, "Reconnect changed player identity/seat.");

            // 同一令牌的新连接接管；直接使用协议验证旧连接不能继续控制座位。
            using TcpClient replacement = new(); await replacement.ConnectAsync(IPAddress.Loopback, fixture.Server.Port);
            using RoomPayloadWriter writer = new(); writer.Text("same", 48); writer.Guid(token); writer.Guid(Guid.Empty);
            foreach (byte[] frame in NetworkFrameCodec.Encode(new(RoomMessageType.Join, 1, writer.ToArray()))) { await replacement.GetStream().WriteAsync(frame); }
            await Until(() => !old.IsConnected && fixture.Server.Room.Snapshot().Members.All(member => member.Connected), fixture.Pump);
            replacement.Close();
            await Until(() => fixture.Server.Room.Snapshot().Members.Any(member => member.PlayerId == playerId && !member.Connected), fixture.Pump);
            LanRoomClient intruder = new(); fixture.Clients.Add(intruder);
            await intruder.ConnectAsync(IPAddress.Loopback, fixture.Server.Port, "new");
            await Until(() => intruder.LastError == "MatchAlreadyStarted", fixture.Pump);
            Assert(fixture.Server.Room.Snapshot().Members.Count == 4, "Disconnected playing seat was replaced.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task LeaveAndRejoin()
    {
        Fixture fixture = new();
        try
        {
            await fixture.Join(true); LanRoomClient guest = await fixture.Join();
            guest.LeaveRoom();
            await Until(() => fixture.Server.Room.Snapshot().Members.Count == 1 && !guest.IsConnected, fixture.Pump);
            Assert(guest.ResumeToken == Guid.Empty, "Explicit leave retained abandoned identity.");
            await fixture.Join();
            Assert(fixture.Server.Room.Snapshot().Members.Count == 2, "Freed seat was not reusable.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task ConcurrentSends()
    {
        TcpListener listener = new(IPAddress.Loopback, 0); listener.Start();
        using TcpClient senderSocket = new();
        Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
        await senderSocket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        TcpClient receiverSocket = await accepted;
        ConcurrentQueue<NetworkMessage> received = new();
        NetworkConnection receiver = new(receiverSocket, (_, message) => { received.Enqueue(message); return true; }, null, 128);
        NetworkConnection sender = new(senderSocket, (_, message) => true, null, 128);
        try
        {
            await Task.WhenAll(Enumerable.Range(0, 50).Select(index => Task.Run(() =>
                sender.Send(RoomMessageType.RoomState, Enumerable.Repeat((byte)index, 2400).ToArray()))));
            await Until(() => received.Count == 50);
            NetworkMessage[] messages = received.ToArray();
            Assert(messages.Select(message => message.Id).SequenceEqual(Enumerable.Range(1, 50).Select(index => (uint)index)), "Concurrent writes interleaved messages.");
            Assert(messages.Select(message => message.Payload[0]).Distinct().Count() == 50
                && messages.All(message => message.Payload.All(value => value == message.Payload[0])), "Concurrent payloads were mixed.");
        }
        finally { await sender.CloseAsync(); await receiver.CloseAsync(); listener.Stop(); }
    }

    private static async Task PartialReadTimeout()
    {
        foreach (bool fragmented in new[] { false, true })
        {
            TcpListener listener = new(IPAddress.Loopback, 0); listener.Start();
            using TcpClient socket = new(); Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
            await socket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
            NetworkConnection reader = new(await accepted, (_, message) => true, null, operationTimeout: TimeSpan.FromMilliseconds(150));
            byte[] bytes = NetworkFrameCodec.Encode(new(RoomMessageType.RoomState, 1, new byte[2000])).First();
            try
            {
                await socket.GetStream().WriteAsync(bytes.AsMemory(0, fragmented ? 1024 : 15));
                await Until(() => reader.IsClosed, milliseconds: 2000);
                await reader.CloseAsync();
            }
            finally { socket.Close(); listener.Stop(); }
        }
    }

    private static async Task MalformedPeer()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await fixture.Join(true);
            using TcpClient malformed = new(); await malformed.ConnectAsync(IPAddress.Loopback, fixture.Server.Port);
            byte[] frame = NetworkFrameCodec.Encode(new(RoomMessageType.Heartbeat, 1, Array.Empty<byte>())).Single(); frame[100] = 99;
            await malformed.GetStream().WriteAsync(frame);
            await Until(() => fixture.Server.ConnectionCount == 1, fixture.Pump);
            Assert(host.IsJoined && fixture.Server.Room.Snapshot().Members.Count == 1, "Malformed peer changed another player's state.");
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task StopAndRestart()
    {
        LanRoomServer server = new(); server.Start(IPAddress.Loopback, 0); int port = server.Port;
        using TcpClient pending = new(); await pending.ConnectAsync(IPAddress.Loopback, port);
        await pending.GetStream().WriteAsync(new byte[] { 1, 2, 3 });
        await Until(() => server.ConnectionCount == 1, server.Pump);
        await server.StopAsync();
        Assert(!server.IsRunning && server.ConnectionCount == 0, "Server left live connections.");
        server.Start(IPAddress.Loopback, port);
        await server.StopAsync();
    }

    private static async Task MalformedPayload()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await fixture.Join(true);
            foreach (RoomMessageType type in new[] { RoomMessageType.SetReady, RoomMessageType.RoomState })
            {
                using TcpClient peer = new(); await peer.ConnectAsync(IPAddress.Loopback, fixture.Server.Port);
                await Until(() => fixture.Server.ConnectionCount == 2, fixture.Pump);
                byte[] frame = NetworkFrameCodec.Encode(new(type, 1, new byte[] { 2 })).Single();
                await peer.GetStream().WriteAsync(frame);
                await Until(() => fixture.Server.ConnectionCount == 1, fixture.Pump);
                Assert(host.IsJoined && fixture.Server.Room.Snapshot().Members.Count == 1, "Malformed payload damaged another session.");
            }
        }
        finally { await fixture.Cleanup(); }
    }

    private static async Task MalformedServer()
    {
        TcpListener listener = new(IPAddress.Loopback, 0); listener.Start();
        LanRoomClient client = new();
        try
        {
            Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
            await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, "test");
            using TcpClient peer = await accepted;
            byte[] frame = NetworkFrameCodec.Encode(new(RoomMessageType.Join, 1, Array.Empty<byte>())).Single();
            await peer.GetStream().WriteAsync(frame);
            await Until(() => !client.IsConnected && client.LastError != null, client.Pump);
        }
        finally { await client.CloseAsync(); listener.Stop(); }
    }

    private static async Task CancelConnecting()
    {
        LanRoomServer server = new(); server.Start(IPAddress.Loopback, 0);
        LanRoomClient client = new();
        try
        {
            Task attempt = client.ConnectAsync(IPAddress.Loopback, server.Port, "cancelled");
            await client.CloseAsync();
            try { await attempt; } catch (Exception) { }
            Assert(!client.IsConnecting && !client.IsConnected && !client.IsJoined, "Closed client published a late successful connection.");
        }
        finally { await client.CloseAsync(); await server.StopAsync(); }
    }

    private static async Task BoundedSendQueue()
    {
        TcpListener listener = new(IPAddress.Loopback, 0); listener.Start();
        using TcpClient socket = new(); Task<TcpClient> accepted = listener.AcceptTcpClientAsync();
        await socket.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port);
        using TcpClient stalled = await accepted;
        int closes = 0;
        NetworkConnection sender = new(socket, (_, message) => true, (_, error) => Interlocked.Increment(ref closes), queueCapacity: 1);
        try
        {
            byte[] payload = new byte[65536];
            for (int index = 0; index < 100 && !sender.IsClosed; index++) { sender.Send(RoomMessageType.RoomState, payload); }
            await Until(() => sender.IsClosed);
            await sender.CloseAsync();
            Assert(closes == 1, "Backpressure did not close exactly once.");
        }
        finally { await sender.CloseAsync(); listener.Stop(); }
    }

    private static async Task HostDeparture()
    {
        Fixture fixture = new();
        try
        {
            LanRoomClient host = await fixture.Join(true); LanRoomClient guest = await fixture.Join();
            host.LeaveRoom();
            await Until(() => !fixture.Server.IsRunning && !guest.IsConnected, fixture.Pump);
            Assert(host.ResumeToken == Guid.Empty && fixture.Server.Room.Phase == RoomPhase.Closed, "Host leave did not end room.");
        }
        finally { await fixture.Cleanup(); }
    }
}
