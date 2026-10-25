using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MaJiangLib
{
    public enum RoomPhase : byte { Waiting, Playing, Closed }

    /// <summary>房间公开玩家信息。不包含恢复令牌或连接对象。</summary>
    public sealed class RoomMember
    {
        public Guid PlayerId { get; }
        public string Name { get; }
        public int Seat { get; }
        public bool Connected { get; }
        public bool Ready { get; }
        public bool IsHost { get; }
        public RoomMember(Guid playerId, string name, int seat, bool connected, bool ready, bool isHost)
        {
            PlayerId = playerId; Name = name; Seat = seat; Connected = connected; Ready = ready; IsHost = isHost;
        }
    }

    public sealed class RoomSnapshot
    {
        public Guid RoomId { get; }
        public long Version { get; }
        public RoomPhase Phase { get; }
        public MatchType MatchType { get; }
        public IReadOnlyList<RoomMember> Members { get; }
        public bool CanStart => Phase == RoomPhase.Waiting && Members.Count == 4
            && Members.All(member => member.Connected && member.Ready) && Members.Any(member => member.IsHost);
        public RoomSnapshot(Guid roomId, long version, RoomPhase phase, MatchType matchType, IEnumerable<RoomMember> members)
        {
            RoomId = roomId; Version = version; Phase = phase; MatchType = matchType;
            Members = Array.AsReadOnly(members.OrderBy(member => member.Seat).ToArray());
        }
    }

    public sealed class RoomJoinResult
    {
        public bool Accepted { get; }
        public string Reason { get; }
        public Guid PlayerId { get; }
        public Guid ResumeToken { get; }
        public Guid ReplacedConnectionId { get; }
        public RoomJoinResult(bool accepted, string reason, Guid playerId = default, Guid resumeToken = default, Guid replacedConnectionId = default)
        {
            Accepted = accepted; Reason = reason; PlayerId = playerId; ResumeToken = resumeToken; ReplacedConnectionId = replacedConnectionId;
        }
    }

    /// <summary>由一个执行上下文调用的房间状态机，无网络、Unity或系统时钟依赖。</summary>
    public sealed class RoomService
    {
        private sealed class Session
        {
            public Guid PlayerId = Guid.NewGuid();
            public Guid ResumeToken = Guid.NewGuid();
            public Guid ConnectionId;
            public string Name;
            public int Seat;
            public bool Ready;
            public bool IsHost;
            public double DisconnectedAt;
        }
        private readonly List<Session> sessions = new();
        private readonly Guid hostJoinToken;
        private long version;
        private MatchType matchType = MatchType.FourMahjongSouth;
        public Guid RoomId { get; } = Guid.NewGuid();
        public RoomPhase Phase { get; private set; }

        public RoomService(Guid hostJoinToken)
        {
            if (hostJoinToken == Guid.Empty) { throw new ArgumentException("Host proof is required.", nameof(hostJoinToken)); }
            this.hostJoinToken = hostJoinToken;
        }

        public RoomSnapshot Snapshot() => new(RoomId, version, Phase, matchType,
            sessions.Select(session => new RoomMember(session.PlayerId, session.Name, session.Seat,
                session.ConnectionId != Guid.Empty, session.Ready, session.IsHost)));

        public RoomJoinResult Join(Guid connectionId, string name, Guid resumeToken, Guid hostProof)
        {
            if (Phase == RoomPhase.Closed) { return new(false, "RoomClosed"); }
            if (connectionId == Guid.Empty || sessions.Any(session => session.ConnectionId == connectionId)) { return new(false, "AlreadyJoined"); }
            if (resumeToken != Guid.Empty)
            {
                Session existing = sessions.FirstOrDefault(session => session.ResumeToken == resumeToken);
                if (existing == null) { return new(false, "InvalidResumeToken"); }
                Guid replaced = existing.ConnectionId;
                existing.ConnectionId = connectionId;
                version++;
                return new(true, "Resumed", existing.PlayerId, existing.ResumeToken, replaced);
            }
            if (Phase != RoomPhase.Waiting) { return new(false, "MatchAlreadyStarted"); }
            if (sessions.Count >= 4) { return new(false, "RoomFull"); }
            if (string.IsNullOrWhiteSpace(name) || Encoding.UTF8.GetByteCount(name) > 48) { return new(false, "InvalidName"); }
            bool isHost = hostProof == hostJoinToken;
            if (hostProof != Guid.Empty && !isHost) { return new(false, "InvalidHostProof"); }
            if (isHost && sessions.Any(session => session.IsHost)) { return new(false, "HostMustResume"); }
            int seat = Enumerable.Range(0, 4).First(candidate => sessions.All(session => session.Seat != candidate));
            Session added = new() { ConnectionId = connectionId, Name = name, Seat = seat, IsHost = isHost };
            sessions.Add(added);
            version++;
            return new(true, "Joined", added.PlayerId, added.ResumeToken);
        }

        public string SetReady(Guid connectionId, bool ready)
        {
            Session player = Find(connectionId);
            if (player == null) { return "NotJoined"; }
            if (Phase != RoomPhase.Waiting) { return "RoomLocked"; }
            if (player.Ready != ready) { player.Ready = ready; version++; }
            return "Ok";
        }

        public string SetMatchType(Guid connectionId, MatchType type)
        {
            Session player = Find(connectionId);
            if (player == null || !player.IsHost) { return "HostOnly"; }
            if (Phase != RoomPhase.Waiting) { return "RoomLocked"; }
            if (type != MatchType.FourMahjongEast && type != MatchType.FourMahjongSouth) { return "InvalidMatchType"; }
            if (matchType != type)
            {
                matchType = type;
                foreach (Session session in sessions) { session.Ready = false; }
                version++;
            }
            return "Ok";
        }

        public string StartMatch(Guid connectionId)
        {
            Session player = Find(connectionId);
            if (player == null || !player.IsHost) { return "HostOnly"; }
            if (!Snapshot().CanStart) { return "PlayersNotReady"; }
            int[] order = new[] { 0, 1, 2, 3 };
            GlobalFunction.Shuffle(order, new Xoshiro256StarStar().NextInt32());
            for (int index = 0; index < sessions.Count; index++) { sessions[index].Seat = order[index]; }
            Phase = RoomPhase.Playing;
            version++;
            return "Ok";
        }

        public void Disconnect(Guid connectionId, double now)
        {
            Session player = Find(connectionId);
            if (player == null) { return; }
            player.ConnectionId = Guid.Empty;
            player.DisconnectedAt = now;
            if (Phase == RoomPhase.Waiting) { player.Ready = false; }
            version++;
        }

        public string Leave(Guid connectionId, double now)
        {
            Session player = Find(connectionId);
            if (player == null) { return "NotJoined"; }
            if (player.IsHost) { Close(); return "Ok"; }
            if (Phase == RoomPhase.Playing) { Disconnect(connectionId, now); }
            else { sessions.Remove(player); version++; }
            return "Ok";
        }

        /// <summary>等待房间保留断线非房主座位30秒；开局后不清退、不允许顶替。</summary>
        public void ExpireReservations(double now)
        {
            if (Phase != RoomPhase.Waiting) { return; }
            if (sessions.RemoveAll(session => !session.IsHost && session.ConnectionId == Guid.Empty && now - session.DisconnectedAt >= 30) > 0)
            {
                version++;
            }
        }

        public void Close()
        {
            if (Phase == RoomPhase.Closed) { return; }
            Phase = RoomPhase.Closed;
            version++;
        }

        private Session Find(Guid connectionId) => connectionId == Guid.Empty ? null : sessions.FirstOrDefault(session => session.ConnectionId == connectionId);
    }

    /// <summary>公开房间快照不包含恢复令牌和房主接入凭据。</summary>
    public static class RoomMessageCodec
    {
        public static byte[] EncodeSnapshot(RoomSnapshot snapshot)
        {
            using RoomPayloadWriter writer = new();
            writer.Guid(snapshot.RoomId); writer.Int64(snapshot.Version); writer.Byte((byte)snapshot.Phase);
            writer.Byte((byte)snapshot.MatchType); writer.Byte((byte)snapshot.Members.Count);
            foreach (RoomMember member in snapshot.Members)
            {
                writer.Guid(member.PlayerId); writer.Text(member.Name, 48); writer.Byte((byte)member.Seat);
                writer.Bool(member.Connected); writer.Bool(member.Ready); writer.Bool(member.IsHost);
            }
            return writer.ToArray();
        }

        public static RoomSnapshot DecodeSnapshot(byte[] bytes)
        {
            using RoomPayloadReader reader = new(bytes);
            Guid roomId = reader.Guid(); long version = reader.Int64(); RoomPhase phase = (RoomPhase)reader.Byte();
            MatchType type = (MatchType)reader.Byte(); int count = reader.Byte();
            if (roomId == Guid.Empty || version < 0 || phase > RoomPhase.Closed || count > 4
                || (type != MatchType.FourMahjongEast && type != MatchType.FourMahjongSouth))
            {
                throw new System.IO.InvalidDataException("Invalid room snapshot header.");
            }
            List<RoomMember> members = new();
            for (int index = 0; index < count; index++)
            {
                RoomMember member = new(reader.Guid(), reader.Text(48), reader.Byte(), reader.Bool(), reader.Bool(), reader.Bool());
                if (member.PlayerId == Guid.Empty || member.Seat > 3 || members.Any(existing => existing.Seat == member.Seat || existing.PlayerId == member.PlayerId)
                    || (member.IsHost && members.Any(existing => existing.IsHost)))
                {
                    throw new System.IO.InvalidDataException("Invalid room member.");
                }
                members.Add(member);
            }
            reader.Complete();
            return new RoomSnapshot(roomId, version, phase, type, members);
        }
    }
}
