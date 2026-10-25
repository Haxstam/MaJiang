using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>已结束本场的公开结果编码。只包含和牌者/应公开听牌者的手牌和已允许公开的里宝牌。</summary>
    public static class RoundSettlementCodec
    {
        public static RoundSettlementResult Copy(RoundSettlementResult result) => Decode(Encode(result));

        public static byte[] Encode(RoundSettlementResult result)
        {
            Validate(result);
            using RoomPayloadWriter writer = new();
            writer.Guid(result.RoundId); writer.Int32(result.RoundGeneration);
            writer.Byte((byte)result.Outcome); writer.Byte(result.WinnerMask); writer.Byte(result.TenpaiMask); writer.Byte(result.NagashiMask);
            WritePoints(writer, result.PointsBefore); WritePoints(writer, result.PointsAfter); WritePoints(writer, result.Deltas);
            writer.Int32(result.SticksBefore); writer.Int32(result.SticksAfter); writer.Bool(result.DealerRepeats); writer.Bool(result.MatchEnded);
            writer.Byte((byte)result.NextWind); writer.Byte((byte)result.NextRound); writer.Byte((byte)result.NextDealer); writer.Int32(result.NextHonba);
            foreach (int seat in result.Ranking) { writer.Byte((byte)seat); }
            writer.Byte((byte)result.Winners.Count);
            foreach (WinnerSettlement winner in result.Winners)
            {
                writer.Byte((byte)winner.Seat); writer.Byte((byte)winner.SourceSeat); writer.Byte((byte)(winner.PaoSeat + 1));
                MatchSyncCodec.WriteTile(writer, winner.WinningTile); WriteScore(writer, winner.Score); WritePoints(writer, winner.Payments);
            }
            writer.Byte((byte)result.Revealed.Count);
            foreach (RevealedHand hand in result.Revealed)
            {
                writer.Byte((byte)hand.Seat); MatchSyncCodec.WriteTiles(writer, hand.Concealed, 13); MatchSyncCodec.WriteTile(writer, hand.Drawn);
                MatchSyncCodec.WriteGroups(writer, hand.Groups);
            }
            MatchSyncCodec.WriteTiles(writer, result.UraIndicators, 5);
            return writer.ToArray();
        }

        public static RoundSettlementResult Decode(byte[] bytes)
        {
            using RoomPayloadReader reader = new(bytes);
            RoundSettlementResult result = new()
            {
                RoundId = reader.Guid(), RoundGeneration = reader.Int32(), Outcome = (RoundOutcomeKind)reader.Byte(),
                WinnerMask = reader.Byte(), TenpaiMask = reader.Byte(), NagashiMask = reader.Byte(),
                PointsBefore = ReadPoints(reader), PointsAfter = ReadPoints(reader), Deltas = ReadPoints(reader),
                SticksBefore = reader.Int32(), SticksAfter = reader.Int32(), DealerRepeats = reader.Bool(), MatchEnded = reader.Bool(),
                NextWind = (WindType)reader.Byte(), NextRound = reader.Byte(), NextDealer = reader.Byte(), NextHonba = reader.Int32(),
                Ranking = Enumerable.Range(0, 4).Select(_ => (int)reader.Byte()).ToArray()
            };
            int winners = Count(reader, 4);
            for (int index = 0; index < winners; index++)
            {
                result.Winners.Add(new WinnerSettlement { Seat = reader.Byte(), SourceSeat = reader.Byte(), PaoSeat = reader.Byte() - 1,
                    WinningTile = MatchSyncCodec.ReadTile(reader), Score = ReadScore(reader), Payments = ReadPoints(reader) });
            }
            int hands = Count(reader, 4);
            for (int index = 0; index < hands; index++)
            {
                result.Revealed.Add(new RevealedHand { Seat = reader.Byte(), Concealed = MatchSyncCodec.ReadTiles(reader, 13),
                    Drawn = MatchSyncCodec.ReadTile(reader), Groups = MatchSyncCodec.ReadGroups(reader) });
            }
            result.UraIndicators = MatchSyncCodec.ReadTiles(reader, 5);
            reader.Complete(); Validate(result); return result;
        }

        private static void WriteScore(RoomPayloadWriter writer, ScoredHand score)
        {
            ValidateScore(score);
            writer.Int32(score.Han); writer.Int32(score.Fu); writer.Int32(score.BasePoints); writer.Int32(score.Yakuman);
            writer.Byte((byte)score.Yaku.Count);
            foreach (ScoredYaku yaku in score.Yaku) { writer.Byte((byte)yaku.Yaku); writer.Int32(yaku.Han); writer.Int32(yaku.Yakuman); }
        }
        private static ScoredHand ReadScore(RoomPayloadReader reader)
        {
            int han = reader.Int32(), fu = reader.Int32(), basic = reader.Int32(), yakuman = reader.Int32();
            int count = Count(reader, 48); List<ScoredYaku> yaku = new();
            for (int index = 0; index < count; index++) { yaku.Add(new((YakuType)reader.Byte(), reader.Int32(), reader.Int32())); }
            ScoredHand result = new(han, fu, basic, yakuman, yaku); ValidateScore(result); return result;
        }
        private static void ValidateScore(ScoredHand score)
        {
            if (score == null || score.Han < 0 || score.Han > 128 || score.Fu < 0 || score.Fu > 200 || score.BasePoints < 1 || score.BasePoints > 104000
                || score.Yakuman < 0 || score.Yakuman > 13 || score.Yaku.Count < 1 || score.Yaku.Count > 48
                || score.Yaku.Any(yaku => !Enum.IsDefined(typeof(YakuType), yaku.Yaku) || yaku.Yaku == YakuType.Empty || yaku.Han < 0 || yaku.Han > 128 || yaku.Yakuman < 0 || yaku.Yakuman > 13))
            {
                throw new InvalidDataException("Invalid scored hand.");
            }
        }
        private static void WritePoints(RoomPayloadWriter writer, int[] points) { foreach (int point in points) { writer.Int32(point); } }
        private static int[] ReadPoints(RoomPayloadReader reader) => Enumerable.Range(0, 4).Select(_ => reader.Int32()).ToArray();
        private static int Count(RoomPayloadReader reader, int maximum)
        {
            int count = reader.Byte(); if (count > maximum) { throw new InvalidDataException("Settlement list exceeds limit."); } return count;
        }
        public static void Validate(RoundSettlementResult result)
        {
            bool Points(int[] points) => points?.Length == 4;
            if (result == null || result.RoundId == Guid.Empty || result.RoundGeneration < 1 || !Enum.IsDefined(typeof(RoundOutcomeKind), result.Outcome)
                || result.Outcome == RoundOutcomeKind.None || result.WinnerMask > 15 || result.TenpaiMask > 15 || result.NagashiMask > 15
                || !Points(result.PointsBefore) || !Points(result.PointsAfter) || !Points(result.Deltas) || result.SticksBefore < 0 || result.SticksAfter < 0
                || result.NextWind < WindType.East || result.NextWind > WindType.West || result.NextRound < 1 || result.NextRound > 4
                || result.NextDealer < 0 || result.NextDealer > 3 || result.NextHonba < 0 || result.Winners == null || result.Winners.Count > 4
                || result.Revealed == null || result.Revealed.Count > 4 || result.UraIndicators == null || result.UraIndicators.Count > 5
                || result.Ranking?.Length != 4 || !result.Ranking.OrderBy(seat => seat).SequenceEqual(new[] { 0, 1, 2, 3 }))
            {
                throw new InvalidDataException("Invalid round settlement.");
            }
            if (result.PointsBefore.Sum(point => (long)point) + result.SticksBefore * 1000L != result.PointsAfter.Sum(point => (long)point) + result.SticksAfter * 1000L
                || Enumerable.Range(0, 4).Any(seat => (long)result.PointsBefore[seat] + result.Deltas[seat] != result.PointsAfter[seat]))
            {
                throw new InvalidDataException("Settlement ledger mismatch.");
            }
            if (result.Winners.Select(winner => winner.Seat).Distinct().Count() != result.Winners.Count
                || result.Revealed.Select(hand => hand.Seat).Distinct().Count() != result.Revealed.Count) { throw new InvalidDataException("Duplicate settlement seat."); }
            foreach (WinnerSettlement winner in result.Winners)
            {
                if (winner.Seat < 0 || winner.Seat > 3 || winner.SourceSeat < 0 || winner.SourceSeat > 3 || winner.PaoSeat < -1 || winner.PaoSeat > 3 || !Points(winner.Payments))
                {
                    throw new InvalidDataException("Invalid winner settlement.");
                }
                ValidateScore(winner.Score);
            }
            foreach (RevealedHand hand in result.Revealed)
            {
                if (hand.Seat < 0 || hand.Seat > 3 || hand.Concealed == null || hand.Concealed.Count > 13 || hand.Groups == null || hand.Groups.Count > 4)
                {
                    throw new InvalidDataException("Invalid revealed hand.");
                }
            }
        }
    }
}
