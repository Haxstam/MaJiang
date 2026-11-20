using System;
using System.IO;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>领域对象复制与命令比较，不经过传输编码。</summary>
    internal static class StateCopy
    {
        public static MatchSettingData Settings(MatchSettingData source) => new(source.MatchType)
        {
            BasePoint = source.BasePoint, FinishPoint = source.FinishPoint, MinimumYakuFan = source.MinimumYakuFan,
            OpenTanyao = source.OpenTanyao, HaveRedDora = source.HaveRedDora,
            HaveBankrupt = source.HaveBankrupt, HaveLocalYaku = source.HaveLocalYaku
        };

        public static Group Group(Group source) => new(source.GroupType, source.Color, source.Tiles)
        {
            FuluSource = source.FuluSource, SingleTile = source.SingleTile
        };

        private static bool ValidTile(Tile tile) => tile is null ||
            (tile.Color >= Color.Wans && tile.Color <= Color.Honor && tile.Number >= 1 &&
            tile.Number <= (tile.Color == Color.Honor ? 7 : 9) &&
            (!tile.IsRedDora || (tile.Color != Color.Honor && tile.Number == 5)));

        public static PlayerActionData Action(PlayerActionData source)
        {
            if (source is null) { return null; }
            if (!Enum.IsDefined(typeof(PlayerAction), source.PlayerAction) || source.SelfNumber < 0 || source.SelfNumber > 3 ||
                source.TargetPlayerNumber < 0 || source.TargetPlayerNumber > 3 || source.Tiles?.Count > 4 ||
                source.Tiles?.Any(tile => tile is null || !ValidTile(tile)) == true || !ValidTile(source.TargetTile))
            {
                throw new InvalidDataException("Invalid action metadata.");
            }
            return new(source.SelfNumber, source.TargetPlayerNumber, source.Tiles?.ToList() ?? new(), source.TargetTile, source.PlayerAction);
        }

        public static MatchCommand Command(MatchCommand source) => new()
        {
            MatchId = source.MatchId, RoundId = source.RoundId, RoundGeneration = source.RoundGeneration,
            PhaseId = source.PhaseId, CommandId = source.CommandId, Action = Action(source.Action)
        };

        public static bool SameCommand(MatchCommand left, MatchCommand right)
        {
            if (left.MatchId != right.MatchId || left.RoundId != right.RoundId || left.RoundGeneration != right.RoundGeneration ||
                left.PhaseId != right.PhaseId || left.CommandId != right.CommandId) { return false; }
            PlayerActionData a = left.Action, b = right.Action;
            if (a is null || b is null) { return a is null && b is null; }
            return a.SelfNumber == b.SelfNumber && a.TargetPlayerNumber == b.TargetPlayerNumber && a.PlayerAction == b.PlayerAction &&
                Tile.ExactlyEquals(a.TargetTile, b.TargetTile) && a.Tiles.Count == b.Tiles.Count &&
                a.Tiles.Zip(b.Tiles, Tile.ExactlyEquals).All(equal => equal);
        }

        /// <summary>复制完整领域状态；Random不外传，也不用于查询。</summary>
        public static MainMatchControl Core(MainMatchControl source)
        {
            MainMatchControl copy = new()
            {
                MatchType = source.MatchType, MatchSettingData = Settings(source.MatchSettingData),
                DiscardTileList = source.DiscardTileList.Select(list => list.ToList()).ToList(),
                PlayerOpenSetList = source.PlayerOpenSetList.Select(list => list.Select(Group).ToList()).ToList(),
                DoraPointerList = source.DoraPointerList.ToList(), Wind = source.Wind, Round = source.Round,
                Honba = source.Honba, KangCount = source.KangCount, KangMark = source.KangMark,
                PlayerPoint = source.PlayerPoint.ToList(), RemainTileCount = source.RemainTileCount,
                CurrentPlayerIndex = source.CurrentPlayerIndex, CurrentDealerIndex = source.CurrentDealerIndex,
                CurrentStageType = source.CurrentStageType, RiichiStickCount = source.RiichiStickCount,
                FirstCycleIppatsu = source.FirstCycleIppatsu, IsRiichi = source.IsRiichi.ToList(),
                IsDoubleRiichi = source.IsDoubleRiichi.ToList(), HaveIppatsu = source.HaveIppatsu.ToList(), IsKan = source.IsKan.ToList(),
                CurrentTile = source.CurrentTile, CurrentTileIndex = source.CurrentTileIndex,
                MainTileList = source.MainTileList?.ToList(), PrimeTileList = source.PrimeTileList?.ToList(),
                PrimeTileIndex = source.PrimeTileIndex, IsServer = source.IsServer, Initialized = source.Initialized,
                PlayerList = new()
            };
            foreach (Player player in source.PlayerList)
            {
                HandTile hand = (HandTile)player.PlayerHandTile.Clone();
                hand.OpenTileList = copy.PlayerOpenSetList[player.PlayerNumber];
                copy.PlayerList.Add(new Player(new UserProfile(player.PlayerProfile.Name), player.PlayerNumber, hand, source.CurrentStageType));
            }
            return copy;
        }

        public static RoundSettlementResult Settlement(RoundSettlementResult source)
        {
            if (source is null) { return null; }
            return new()
            {
                RoundId = source.RoundId, RoundGeneration = source.RoundGeneration, Outcome = source.Outcome,
                WinnerMask = source.WinnerMask, TenpaiMask = source.TenpaiMask, NagashiMask = source.NagashiMask,
                PointsBefore = source.PointsBefore.ToArray(), PointsAfter = source.PointsAfter.ToArray(), Deltas = source.Deltas.ToArray(),
                SticksBefore = source.SticksBefore, SticksAfter = source.SticksAfter, DealerRepeats = source.DealerRepeats,
                MatchEnded = source.MatchEnded, NextWind = source.NextWind, NextRound = source.NextRound,
                NextDealer = source.NextDealer, NextHonba = source.NextHonba, Ranking = source.Ranking.ToArray(),
                UraIndicators = source.UraIndicators.ToList(),
                Winners = source.Winners.Select(winner => new WinnerSettlement
                {
                    Seat = winner.Seat, SourceSeat = winner.SourceSeat, WinningTile = winner.WinningTile,
                    PaoSeat = winner.PaoSeat, Payments = winner.Payments.ToArray(), Score = winner.Score
                }).ToList(),
                Revealed = source.Revealed.Select(hand => new RevealedHand
                {
                    Seat = hand.Seat, Concealed = hand.Concealed.ToList(), Drawn = hand.Drawn,
                    Groups = hand.Groups.Select(Group).ToList()
                }).ToList()
            };
        }
    }
}
