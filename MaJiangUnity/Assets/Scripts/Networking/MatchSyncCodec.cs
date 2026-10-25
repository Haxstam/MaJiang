using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>可见状态的明确编码。避开旧512字节布局，不编码牌山、随机源、别家暗手或未公开里宝牌。</summary>
    public static class MatchSyncCodec
    {
        public static MatchSettingData CopySettings(MatchSettingData settings) => new(settings.MatchType, settings.ThinkingTime, settings.BaseTime,
            settings.BasePoint, settings.FinishPoint, settings.MinimumYakuFan, settings.OpenTanyao, settings.HaveRedDora, settings.HaveBankrupt, settings.HaveLocalYaku);
        public static Group CopyGroup(Group group) => new(group.GroupType, group.Color, group.Tiles) { FuluSource = group.FuluSource, SingleTile = group.SingleTile };
        public static PlayerActionData CopyAction(PlayerActionData action)
        {
            if (action is null) { return null; }
            if (!Enum.IsDefined(typeof(PlayerAction), action.PlayerAction) || action.SelfNumber < 0 || action.SelfNumber > 3
                || action.TargetPlayerNumber < 0 || action.TargetPlayerNumber > 3 || action.Tiles?.Count > 4 || action.Tiles?.Any(tile => tile is null) == true)
            {
                throw new InvalidDataException("Invalid action metadata.");
            }
            return new(action.SelfNumber, action.TargetPlayerNumber, action.Tiles?.ToList() ?? new(), action.TargetTile, action.PlayerAction);
        }

        public static byte[] EncodeSnapshot(VisibleMatchState view)
        {
            Validate(view);
            using RoomPayloadWriter writer = new();
            writer.Guid(view.MatchId); writer.Guid(view.RoundId); writer.Int32(view.RoundGeneration); writer.Int64(view.Sequence); writer.Byte((byte)view.SelfSeat);
            writer.Bytes(view.State.MatchSettingData.GetBytes(), 16);
            foreach (Player player in view.State.PlayerList) { writer.Text(player.PlayerProfile.Name, 48); }
            WriteStatus(writer, view);
            for (int seat = 0; seat < 4; seat++) { WriteTiles(writer, view.State.DiscardTileList[seat], 136); WriteGroups(writer, view.State.PlayerOpenSetList[seat]); }
            WriteTiles(writer, view.State.DoraPointerList, 5);
            WriteHand(writer, view.State.PlayerList[view.SelfSeat].PlayerHandTile);
            WriteSettlement(writer, view.Settlement);
            return writer.ToArray();
        }

        public static VisibleMatchState DecodeSnapshot(byte[] payload)
        {
            using RoomPayloadReader reader = new(payload);
            VisibleMatchState view = new() { MatchId = reader.Guid(), RoundId = reader.Guid(), RoundGeneration = reader.Int32(), Sequence = reader.Int64(), SelfSeat = reader.Byte() };
            if (view.SelfSeat > 3) { throw new InvalidDataException("Invalid snapshot receiver."); }
            byte[] settings = reader.Bytes(16);
            if (settings.Length != 16) { throw new InvalidDataException("Invalid rule settings size."); }
            MainMatchControl state = new()
            {
                MatchSettingData = new MatchSettingData(MatchType.FourMahjongSouth).BytesTo(settings, 0), PlayerList = new(), DiscardTileList = new(),
                PlayerOpenSetList = new(), DoraPointerList = new(), Initialized = true
            };
            view.State = state;
            for (int seat = 0; seat < 4; seat++) { state.PlayerList.Add(new Player(new UserProfile(reader.Text(48)), seat, new HandTile(seat), StageType.InitStage)); }
            ReadStatus(reader, view);
            for (int seat = 0; seat < 4; seat++) { state.DiscardTileList.Add(ReadTiles(reader, 136)); state.PlayerOpenSetList.Add(ReadGroups(reader)); }
            state.DoraPointerList = ReadTiles(reader, 5);
            ReadHand(reader, state.PlayerList[view.SelfSeat].PlayerHandTile);
            view.Settlement = ReadSettlement(reader);
            BindPublicGroups(view);
            reader.Complete(); Validate(view); return view;
        }

        /// <summary>动作日志之外只补充裁定状态及发生变化的区域，不重复整个牌局。</summary>
        public static byte[] EncodeUpdate(VisibleMatchState before, VisibleMatchState after)
        {
            Validate(after);
            using RoomPayloadWriter writer = new();
            WriteStatus(writer, after);
            for (int seat = 0; seat < 4; seat++)
            {
                bool riverChanged = !SameTiles(before.State.DiscardTileList[seat], after.State.DiscardTileList[seat]);
                writer.Bool(riverChanged); if (riverChanged) { WriteTiles(writer, after.State.DiscardTileList[seat], 136); }
                bool meldChanged = !GroupBytes(before.State.PlayerOpenSetList[seat]).SequenceEqual(GroupBytes(after.State.PlayerOpenSetList[seat]));
                writer.Bool(meldChanged); if (meldChanged) { WriteGroups(writer, after.State.PlayerOpenSetList[seat]); }
            }
            bool doraChanged = !SameTiles(before.State.DoraPointerList, after.State.DoraPointerList);
            writer.Bool(doraChanged); if (doraChanged) { WriteTiles(writer, after.State.DoraPointerList, 5); }
            HandTile previous = before.State.PlayerList[before.SelfSeat].PlayerHandTile;
            HandTile next = after.State.PlayerList[after.SelfSeat].PlayerHandTile;
            bool handChanged = !SameTiles(previous.HandTileList, next.HandTileList) || !Tile.ExactlyEquals(previous.SingleTile, next.SingleTile);
            writer.Bool(handChanged); if (handChanged) { WriteHand(writer, next); }
            bool settlementChanged = before.Settlement?.RoundId != after.Settlement?.RoundId;
            writer.Bool(settlementChanged); if (settlementChanged) { WriteSettlement(writer, after.Settlement); }
            return writer.ToArray();
        }

        public static void ApplyUpdate(VisibleMatchState view, byte[] update)
        {
            using RoomPayloadReader reader = new(update);
            ReadStatus(reader, view);
            for (int seat = 0; seat < 4; seat++)
            {
                if (reader.Bool()) { view.State.DiscardTileList[seat] = ReadTiles(reader, 136); }
                if (reader.Bool()) { view.State.PlayerOpenSetList[seat] = ReadGroups(reader); }
            }
            if (reader.Bool()) { view.State.DoraPointerList = ReadTiles(reader, 5); }
            if (reader.Bool()) { ReadHand(reader, view.State.PlayerList[view.SelfSeat].PlayerHandTile); }
            if (reader.Bool()) { view.Settlement = ReadSettlement(reader); }
            BindPublicGroups(view); reader.Complete();
        }

        public static byte[] EncodeBatch(MatchLogBatch batch)
        {
            if (batch.Records.Count > 16) { throw new InvalidDataException("Too many journal records."); }
            using RoomPayloadWriter writer = new();
            writer.Guid(batch.MatchId); writer.Guid(batch.RoundId); writer.Int32(batch.RoundGeneration); writer.Int64(batch.LatestSequence); writer.Byte((byte)batch.Records.Count);
            foreach (VisibleMatchRecord record in batch.Records)
            {
                writer.Int64(record.Sequence); WriteAction(writer, record.Action); writer.Bytes(record.Update, 8192);
            }
            return writer.ToArray();
        }

        public static MatchLogBatch DecodeBatch(byte[] bytes)
        {
            using RoomPayloadReader reader = new(bytes);
            MatchLogBatch batch = new() { MatchId = reader.Guid(), RoundId = reader.Guid(), RoundGeneration = reader.Int32(), LatestSequence = reader.Int64() };
            int count = reader.Byte();
            if (count > 16 || batch.MatchId == Guid.Empty || batch.RoundId == Guid.Empty || batch.RoundGeneration < 1 || batch.LatestSequence < -1)
            {
                throw new InvalidDataException("Invalid journal header.");
            }
            List<VisibleMatchRecord> records = new();
            for (int index = 0; index < count; index++)
            {
                long sequence = reader.Int64(); PlayerActionData action = ReadAction(reader); byte[] update = reader.Bytes(8192);
                if (sequence < 0 || sequence > batch.LatestSequence || (index > 0 && sequence != records[index - 1].Sequence + 1)) { throw new InvalidDataException("Invalid journal sequence."); }
                records.Add(new(sequence, action, update));
            }
            batch.Records = records.AsReadOnly(); reader.Complete(); return batch;
        }

        private static void WriteSettlement(RoomPayloadWriter writer, RoundSettlementResult result)
        {
            writer.Bool(result != null);
            if (result != null) { writer.Bytes(RoundSettlementCodec.Encode(result), 8192); }
        }
        private static RoundSettlementResult ReadSettlement(RoomPayloadReader reader) => reader.Bool() ? RoundSettlementCodec.Decode(reader.Bytes(8192)) : null;

        private static void WriteStatus(RoomPayloadWriter writer, VisibleMatchState view)
        {
            MainMatchControl state = view.State;
            writer.Byte((byte)state.MatchType); writer.Byte((byte)state.Wind); writer.Int32(state.Round); writer.Int32(state.Honba);
            writer.Byte((byte)state.KangCount); writer.Byte((byte)state.KangMark); writer.Byte((byte)state.CurrentPlayerIndex); writer.Byte((byte)state.CurrentStageType);
            writer.Byte((byte)state.CurrentDealerIndex); writer.Int32(state.RemainTileCount); writer.Bool(state.FirstCycleIppatsu); writer.Int32(state.RiichiStickCount);
            for (int seat = 0; seat < 4; seat++)
            {
                writer.Int32(state.PlayerPoint[seat]);
                writer.Bool(state.IsRiichi[seat]); writer.Bool(state.IsDoubleRiichi[seat]); writer.Bool(state.HaveIppatsu[seat]); writer.Bool(state.IsKan[seat]);
                writer.Byte((byte)view.HandCounts[seat]);
            }
            WriteTile(writer, state.CurrentTile);
            writer.Int64(view.PhaseId); writer.Int64(view.DeadlineMilliseconds); writer.Int64(view.ServerMilliseconds);
            writer.Int32(view.ThinkingMilliseconds); writer.Byte(view.FuritenFlags); writer.Bool(view.HasResponded); writer.Byte((byte)view.PendingDoraCount);
            writer.Byte((byte)view.OperationStage); writer.Byte((byte)(view.ResponseSourceSeat + 1)); WriteTile(writer, view.ResponseTile);
            writer.Byte((byte)(view.PendingRiichiSeat + 1)); WriteTiles(writer, view.ForbiddenDiscards, 2);
            writer.Byte((byte)view.Outcome); writer.Byte(view.WinnerMask);
            writer.Int64(view.NextRoundMilliseconds);
        }

        private static void ReadStatus(RoomPayloadReader reader, VisibleMatchState view)
        {
            MainMatchControl state = view.State;
            state.MatchType = (MatchType)reader.Byte(); state.Wind = (WindType)reader.Byte(); state.Round = reader.Int32(); state.Honba = reader.Int32();
            state.KangCount = reader.Byte(); state.KangMark = reader.Byte(); state.CurrentPlayerIndex = reader.Byte(); state.CurrentStageType = (StageType)reader.Byte();
            state.CurrentDealerIndex = reader.Byte(); state.RemainTileCount = reader.Int32(); state.FirstCycleIppatsu = reader.Bool(); state.RiichiStickCount = reader.Int32();
            state.PlayerPoint = new(); state.IsRiichi = new(); state.IsDoubleRiichi = new(); state.HaveIppatsu = new(); state.IsKan = new();
            for (int seat = 0; seat < 4; seat++)
            {
                state.PlayerPoint.Add(reader.Int32()); state.IsRiichi.Add(reader.Bool()); state.IsDoubleRiichi.Add(reader.Bool());
                state.HaveIppatsu.Add(reader.Bool()); state.IsKan.Add(reader.Bool()); view.HandCounts[seat] = reader.Byte();
                state.PlayerList[seat].CurrentStageType = state.CurrentStageType;
            }
            state.CurrentTile = ReadTile(reader);
            view.PhaseId = reader.Int64(); view.DeadlineMilliseconds = reader.Int64(); view.ServerMilliseconds = reader.Int64();
            view.ThinkingMilliseconds = reader.Int32(); view.FuritenFlags = reader.Byte(); view.HasResponded = reader.Bool(); view.PendingDoraCount = reader.Byte();
            view.OperationStage = (MatchOperationStage)reader.Byte(); view.ResponseSourceSeat = reader.Byte() - 1; view.ResponseTile = ReadTile(reader);
            view.PendingRiichiSeat = reader.Byte() - 1; view.ForbiddenDiscards = ReadTiles(reader, 2);
            view.Outcome = (RoundOutcomeKind)reader.Byte(); view.WinnerMask = reader.Byte();
            view.NextRoundMilliseconds = reader.Int64();
        }

        private static void WriteHand(RoomPayloadWriter writer, HandTile hand) { WriteTiles(writer, hand.HandTileList, 13); WriteTile(writer, hand.SingleTile); }
        private static void ReadHand(RoomPayloadReader reader, HandTile hand) { hand.HandTileList = ReadTiles(reader, 13); hand.SingleTile = ReadTile(reader); }
        private static void BindPublicGroups(VisibleMatchState view)
        {
            for (int seat = 0; seat < 4; seat++) { view.State.PlayerList[seat].PlayerHandTile.OpenTileList = view.State.PlayerOpenSetList[seat]; }
        }
        private static bool SameTiles(IReadOnlyList<Tile> left, IReadOnlyList<Tile> right)
            => left.Count == right.Count && left.Zip(right, Tile.ExactlyEquals).All(equal => equal);
        private static byte[] GroupBytes(List<Group> groups) { using RoomPayloadWriter writer = new(); WriteGroups(writer, groups); return writer.ToArray(); }
        internal static void WriteGroups(RoomPayloadWriter writer, List<Group> groups)
        {
            if (groups.Count > 4) { throw new InvalidDataException("Too many melds."); }
            writer.Byte((byte)groups.Count);
            foreach (Group group in groups)
            {
                writer.Byte((byte)group.GroupType); writer.Byte((byte)group.Color); writer.Byte((byte)group.FuluSource);
                WriteTile(writer, group.SingleTile); WriteTiles(writer, group.Tiles, 4);
            }
        }
        internal static List<Group> ReadGroups(RoomPayloadReader reader)
        {
            int count = reader.Byte(); if (count > 4) { throw new InvalidDataException("Too many melds."); }
            List<Group> groups = new();
            for (int index = 0; index < count; index++)
            {
                GroupType type = (GroupType)reader.Byte(); Color color = (Color)reader.Byte(); int source = reader.Byte();
                Tile called = ReadTile(reader); List<Tile> tiles = ReadTiles(reader, 4);
                if (!Enum.IsDefined(typeof(GroupType), type) || color > Color.Honor || source > 3 || tiles.Count < 3) { throw new InvalidDataException("Invalid meld."); }
                groups.Add(new Group(type, color, tiles) { FuluSource = source, SingleTile = called });
            }
            return groups;
        }
        internal static void WriteTiles(RoomPayloadWriter writer, List<Tile> tiles, int maximum)
        {
            if (tiles.Count > maximum || tiles.Any(tile => tile is null)) { throw new InvalidDataException("Invalid tile list."); }
            writer.Byte((byte)tiles.Count); foreach (Tile tile in tiles) { WriteTile(writer, tile); }
        }
        internal static List<Tile> ReadTiles(RoomPayloadReader reader, int maximum)
        {
            int count = reader.Byte(); if (count > maximum) { throw new InvalidDataException("Too many tiles."); }
            List<Tile> tiles = new();
            for (int index = 0; index < count; index++) { tiles.Add(ReadTile(reader) ?? throw new InvalidDataException("Null tile in list.")); }
            return tiles;
        }
        internal static void WriteTile(RoomPayloadWriter writer, Tile tile)
        {
            writer.Bool(tile != null); if (tile == null) { return; }
            if (tile.Color > Color.Honor || tile.Number < 1 || tile.Number > (tile.Color == Color.Honor ? 7 : 9)
                || (tile.IsRedDora && (tile.Number != 5 || tile.Color == Color.Honor))) { throw new InvalidDataException("Invalid physical tile."); }
            writer.Byte((byte)tile.Color); writer.Byte((byte)(tile.IsRedDora ? 0 : tile.Number));
        }
        internal static Tile ReadTile(RoomPayloadReader reader)
        {
            if (!reader.Bool()) { return null; }
            Color color = (Color)reader.Byte(); int number = reader.Byte();
            if (color > Color.Honor || number > (color == Color.Honor ? 7 : 9) || (number == 0 && color == Color.Honor)) { throw new InvalidDataException("Invalid physical tile."); }
            return new Tile(color, number == 0 ? 5 : number, number == 0);
        }
        public static void WriteAction(RoomPayloadWriter writer, PlayerActionData action)
        {
            writer.Bool(action != null); if (action == null) { return; }
            writer.Byte((byte)action.PlayerAction); writer.Byte((byte)action.SelfNumber); writer.Byte((byte)action.TargetPlayerNumber);
            WriteTiles(writer, action.Tiles ?? new(), 4); WriteTile(writer, action.TargetTile);
        }
        public static PlayerActionData ReadAction(RoomPayloadReader reader)
        {
            if (!reader.Bool()) { return null; }
            PlayerAction type = (PlayerAction)reader.Byte(); int actor = reader.Byte(); int target = reader.Byte();
            List<Tile> tiles = ReadTiles(reader, 4); Tile tile = ReadTile(reader);
            if (!Enum.IsDefined(typeof(PlayerAction), type) || actor > 3 || target > 3) { throw new InvalidDataException("Invalid action."); }
            return new PlayerActionData(actor, target, tiles, tile, type);
        }

        public static void Validate(VisibleMatchState view)
        {
            MainMatchControl state = view?.State;
            if (view == null || state == null || view.MatchId == Guid.Empty || view.RoundId == Guid.Empty || view.RoundGeneration < 1
                || view.Sequence < -1 || view.SelfSeat < 0 || view.SelfSeat > 3 || view.PhaseId < 1 || view.DeadlineMilliseconds < 0
                || view.ServerMilliseconds < 0 || view.ThinkingMilliseconds < 0 || view.FuritenFlags > 7 || view.PendingDoraCount < 0 || view.PendingDoraCount > 4
                || !Enum.IsDefined(typeof(MatchOperationStage), view.OperationStage) || !Enum.IsDefined(typeof(RoundOutcomeKind), view.Outcome)
                || view.ResponseSourceSeat < -1 || view.ResponseSourceSeat > 3 || view.PendingRiichiSeat < -1 || view.PendingRiichiSeat > 3 || view.WinnerMask > 15 || view.NextRoundMilliseconds < 0)
            {
                throw new InvalidDataException("Invalid visible match header.");
            }
            if (view.Settlement != null)
            {
                RoundSettlementCodec.Validate(view.Settlement);
                if (view.Settlement.RoundGeneration > view.RoundGeneration || (view.Settlement.RoundGeneration == view.RoundGeneration && view.Settlement.RoundId != view.RoundId))
                {
                    throw new InvalidDataException("Settlement belongs to a future or conflicting round.");
                }
            }
            if (state.PlayerList?.Count != 4 || state.PlayerPoint?.Count != 4 || state.DiscardTileList?.Count != 4 || state.PlayerOpenSetList?.Count != 4
                || state.IsRiichi?.Count != 4 || state.IsDoubleRiichi?.Count != 4 || state.HaveIppatsu?.Count != 4 || state.IsKan?.Count != 4
                || view.HandCounts?.Length != 4 || state.MatchSettingData == null || state.MatchSettingData.MatchType != state.MatchType
                || (state.MatchType != MatchType.FourMahjongEast && state.MatchType != MatchType.FourMahjongSouth)
                || !Enum.IsDefined(typeof(WindType), state.Wind) || !Enum.IsDefined(typeof(StageType), state.CurrentStageType)
                || state.CurrentPlayerIndex < 0 || state.CurrentPlayerIndex > 3 || state.CurrentDealerIndex < 0 || state.CurrentDealerIndex > 3
                || state.Round < 1 || state.Round > 4 || state.Honba < 0 || state.RemainTileCount < 0 || state.RemainTileCount > 70
                || state.RiichiStickCount < 0 || state.KangCount < 0 || state.KangCount > 4 || state.KangMark < 0 || state.KangMark > 6
                || state.MainTileList != null || state.PrimeTileList != null || state.Random != null || state.IsServer)
            {
                throw new InvalidDataException("Invalid or non-visible match state.");
            }
            for (int seat = 0; seat < 4; seat++)
            {
                HandTile hand = state.PlayerList[seat].PlayerHandTile;
                int actual = hand.HandTileList.Count + (hand.SingleTile != null ? 1 : 0);
                if (view.HandCounts[seat] < 0 || view.HandCounts[seat] > 14 || state.PlayerList[seat].PlayerNumber != seat
                    || hand.PlayerNumber != seat || (seat == view.SelfSeat ? actual != view.HandCounts[seat] : actual != 0))
                {
                    throw new InvalidDataException("Private hand or public hand count mismatch.");
                }
            }
        }
    }
}
