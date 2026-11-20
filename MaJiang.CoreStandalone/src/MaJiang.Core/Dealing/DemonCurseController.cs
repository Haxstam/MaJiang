using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>有状态配牌策略，只改变尚未公开/取走的牌。所有调用均发生在显式操作的同步推进内。</summary>
    internal sealed class DemonCurseController
    {
        private readonly DemonCurseOptions options;
        private readonly List<CurseAuditRecord> audit = new();
        private readonly CurseStatistics[] statistics;
        private readonly HashSet<int> frozenDeadPositions = new();
        private int round;
        // 第五对表/里优先，逐对向前；初始公开表位会被排除。
        private static readonly int[] IndicatorPriority = { 8, 9, 6, 7, 4, 5, 2, 3, 0, 1 };

        public DemonCurseController(DemonCurseOptions options)
        {
            this.options = options;
            statistics = Enumerable.Range(0, 4).Select(seat => new CurseStatistics { Seat = seat, Curse = options.At(seat) }).ToArray();
        }

        public IReadOnlyList<CurseAuditRecord> Audit => Array.AsReadOnly(audit.ToArray());
        public IReadOnlyList<CurseAuditRecord> GetAudit(int startIndex)
        {
            if (startIndex < 0 || startIndex > audit.Count) { throw new ArgumentOutOfRangeException(nameof(startIndex)); }
            return Array.AsReadOnly(audit.Skip(startIndex).ToArray());
        }
        public IReadOnlyList<CurseStatistics> Statistics => Array.AsReadOnly(statistics.Select(item => item.Copy()).ToArray());

        /// <summary>先条件均匀抽出诅咒1的13张，再按绝对座位给其他人配牌。</summary>
        public List<List<Tile>> Deal(List<Tile> fullSet, Xoshiro256StarStar random, out List<Tile> live, out List<Tile> dead)
        {
            if (fullSet.Count != 136 || Enumerable.Range(0, 34).Any(face => fullSet.Count(tile => HandAnalysis.Face(tile) == face) != 4))
            {
                throw new ArgumentException("A complete 136-tile four-player set is required.", nameof(fullSet));
            }
            round++; frozenDeadPositions.Clear();
            List<Tile> pool = fullSet.ToList();
            List<List<Tile>> hands = Enumerable.Range(0, 4).Select(_ => new List<Tile>()).ToList();
            int scatteredSeat = options.SeatOf(DemonCurse.ScatteredInitialHand);
            hands[scatteredSeat] = Scattered(pool, random);
            Record(new(round, scatteredSeat, DemonCurse.ScatteredInitialHand, CurseTrigger.InitialHand, CurseAdjustment.Prepared));
            for (int seat = 0; seat < 4; seat++)
            {
                if (seat == scatteredSeat) { continue; }
                if (options.RulesVersion == DemonCurseRulesVersion.V2 && options.At(seat) == DemonCurse.NoDuplicateDraw)
                {
                    hands[seat] = DistinctInitialHand(pool, random);
                    Record(new(round, seat, DemonCurse.NoDuplicateDraw, CurseTrigger.InitialHand, CurseAdjustment.Prepared));
                    continue;
                }
                for (int count = 0; count < 13; count++)
                {
                    int[] eligible = Enumerable.Range(0, pool.Count).Where(index => options.At(seat) != DemonCurse.NoBonusTiles || !pool[index].IsRedDora).ToArray();
                    int chosen = eligible[(int)Below(random, (ulong)eligible.Length)];
                    hands[seat].Add(pool[chosen]); pool.RemoveAt(chosen);
                }
                if (options.At(seat) == DemonCurse.NoBonusTiles)
                {
                    Record(new(round, seat, DemonCurse.NoBonusTiles, CurseTrigger.InitialHand, CurseAdjustment.Prepared));
                }
            }
            Shuffle(pool, random);
            live = pool.Take(70).ToList(); dead = pool.Skip(70).ToList();
            return hands;
        }

        /// <summary>精确按合法实体牌子集加权采样，不用低效的反复重洗，也不以向听数代替十三不搭。</summary>
        private static List<Tile> Scattered(List<Tile> pool, Xoshiro256StarStar random)
        {
            int[] counts = Enumerable.Range(0, 34).Select(face => pool.Count(tile => HandAnalysis.Face(tile) == face)).ToArray();
            ulong?[,] memo = new ulong?[35, 14];
            int Next(int face) => face < 27 ? Math.Min((face / 9 + 1) * 9, face + 3) : face + 1;
            ulong Ways(int face, int needed)
            {
                if (needed == 0) { return 1; }
                if (face >= 34) { return 0; }
                if (memo[face, needed].HasValue) { return memo[face, needed].Value; }
                ulong total = checked(Ways(face + 1, needed) + (ulong)counts[face] * Ways(Next(face), needed - 1));
                memo[face, needed] = total; return total;
            }
            List<Tile> result = new();
            for (int face = 0, needed = 13; needed > 0;)
            {
                ulong total = Ways(face, needed);
                if (total == 0) { throw new InvalidOperationException("No legal scattered initial hand exists."); }
                if (Below(random, total) < Ways(face + 1, needed)) { face++; continue; }
                int[] candidates = Enumerable.Range(0, pool.Count).Where(index => HandAnalysis.Face(pool[index]) == face).ToArray();
                int chosen = candidates[(int)Below(random, (ulong)candidates.Length)];
                result.Add(pool[chosen]); pool.RemoveAt(chosen);
                face = Next(face); needed--;
            }
            Shuffle(result, random); return result;
        }

        /// <summary>从剩余实体牌中按条件组合数抽13个不同牌种；赤五和普通五合并判断。</summary>
        private static List<Tile> DistinctInitialHand(List<Tile> pool, Xoshiro256StarStar random)
        {
            int[] counts = Enumerable.Range(0, 34).Select(face => pool.Count(tile => HandAnalysis.Face(tile) == face)).ToArray();
            ulong?[,] memo = new ulong?[35, 14];
            ulong Ways(int face, int needed)
            {
                if (needed == 0) { return 1; }
                if (face >= 34) { return 0; }
                if (memo[face, needed].HasValue) { return memo[face, needed].Value; }
                ulong total = checked(Ways(face + 1, needed) + (ulong)counts[face] * Ways(face + 1, needed - 1));
                memo[face, needed] = total; return total;
            }
            List<Tile> result = new();
            for (int face = 0, needed = 13; needed > 0;)
            {
                ulong total = Ways(face, needed);
                if (total == 0) { throw new InvalidOperationException("No distinct-face initial hand exists."); }
                if (Below(random, total) < Ways(face + 1, needed)) { face++; continue; }
                int[] candidates = Enumerable.Range(0, pool.Count).Where(index => HandAnalysis.Face(pool[index]) == face).ToArray();
                int chosen = candidates[(int)Below(random, (ulong)candidates.Length)];
                result.Add(pool[chosen]); pool.RemoveAt(chosen);
                face++; needed--;
            }
            Shuffle(result, random); return result;
        }

        public void BeforeDraw(MainMatchControl core, bool rinshan)
        {
            int seat = core.CurrentPlayerIndex;
            DemonCurse curse = options.At(seat);
            HandTile hand = core.PlayerList[seat].PlayerHandTile;
            HashSet<int> forbidden;
            switch (curse)
            {
                case DemonCurse.NoWinningDraw:
                    forbidden = new HashSet<int>(HandAnalysis.Waits(hand).Select(HandAnalysis.Face));
                    if (forbidden.Count == 0) { return; }
                    break;
                case DemonCurse.NoDuplicateDraw:
                    forbidden = new HashSet<int>(hand.HandTileList.Select(HandAnalysis.Face)); break;
                case DemonCurse.NoBonusTiles:
                    forbidden = new HashSet<int>(core.DoraPointerList.Select(marker => RiichiScoring.NextDora(HandAnalysis.Face(marker)))); break;
                default: return;
            }
            bool Accept(Tile tile) => !forbidden.Contains(HandAnalysis.Face(tile)) && (curse != DemonCurse.NoBonusTiles || !tile.IsRedDora);
            Adjust(core, seat, rinshan ? CurseTrigger.RinshanDraw : CurseTrigger.NormalDraw,
                rinshan ? WallZone.DeadWall : WallZone.LiveWall, rinshan ? 10 + core.PrimeTileIndex : core.CurrentTileIndex, Accept);
        }

        public void BeforeIndicator(MainMatchControl core, int index, bool ura, byte winners = 0, Tile winningTile = null)
        {
            if (frozenDeadPositions.Contains(index)) { return; }
            int seat = options.SeatOf(DemonCurse.NoBonusTiles);
            HandTile hand = core.PlayerList[seat].PlayerHandTile;
            HashSet<int> owned = new(hand.HandTileList.Concat(hand.SingleTile == null ? Array.Empty<Tile>() : new[] { hand.SingleTile })
                .Concat(core.PlayerOpenSetList[seat].SelectMany(group => group.Tiles)).Select(HandAnalysis.Face));
            // 荣和张没有写进获胜者暗手，但在翻里牌时也属于其和牌手牌。
            if ((winners & (1 << seat)) != 0 && winningTile != null) { owned.Add(HandAnalysis.Face(winningTile)); }
            Adjust(core, seat, ura ? CurseTrigger.UraIndicator : CurseTrigger.DoraIndicator, WallZone.DeadWall, index,
                tile => !owned.Contains(RiichiScoring.NextDora(HandAnalysis.Face(tile))));
            frozenDeadPositions.Add(index);
        }

        private void Adjust(MainMatchControl core, int seat, CurseTrigger trigger, WallZone sourceZone, int sourceIndex, Func<Tile, bool> accept)
        {
            List<Tile> source = sourceZone == WallZone.LiveWall ? core.MainTileList : core.PrimeTileList;
            Tile original = source[sourceIndex];
            if (accept(original))
            {
                Record(new(round, seat, options.At(seat), trigger, CurseAdjustment.AlreadyValid, sourceZone, sourceIndex, original, result: original)); return;
            }
            int[] live = Enumerable.Range(core.CurrentTileIndex, core.MainTileList.Count - core.CurrentTileIndex)
                .Where(index => !(sourceZone == WallZone.LiveWall && index == sourceIndex) && accept(core.MainTileList[index])).ToArray();
            if (live.Length > 0)
            {
                int replacement = live[(int)Below(core.Random, (ulong)live.Length)];
                Tile result = core.MainTileList[replacement];
                source[sourceIndex] = result; core.MainTileList[replacement] = original;
                Record(new(round, seat, options.At(seat), trigger, CurseAdjustment.SwappedLiveWall,
                    sourceZone, sourceIndex, original, WallZone.LiveWall, replacement, result)); return;
            }
            foreach (int index in DeadPositions(core))
            {
                if ((sourceZone == WallZone.DeadWall && index == sourceIndex) || !accept(core.PrimeTileList[index])) { continue; }
                Tile result = core.PrimeTileList[index];
                source[sourceIndex] = result; core.PrimeTileList[index] = original;
                Record(new(round, seat, options.At(seat), trigger, CurseAdjustment.SwappedDeadWall,
                    sourceZone, sourceIndex, original, WallZone.DeadWall, index, result)); return;
            }
            Record(new(round, seat, options.At(seat), trigger, CurseAdjustment.Unavoidable, sourceZone, sourceIndex, original, result: original));
        }

        private IEnumerable<int> DeadPositions(MainMatchControl core)
        {
            bool Available(int index) => index < core.PrimeTileList.Count && !frozenDeadPositions.Contains(index) &&
                !(index < 10 && index % 2 == 0 && index / 2 < core.DoraPointerList.Count);
            foreach (int index in IndicatorPriority) { if (Available(index)) { yield return index; } }
            for (int index = core.PrimeTileList.Count - 1; index >= 14; index--) { if (Available(index)) { yield return index; } }
            for (int index = 13; index >= 10 + core.PrimeTileIndex; index--) { if (Available(index)) { yield return index; } }
        }

        private void Record(CurseAuditRecord record)
        {
            CurseStatistics item = statistics[record.Seat]; item.Attempts++;
            switch (record.Adjustment)
            {
                case CurseAdjustment.Prepared: item.PreparedHands++; break;
                case CurseAdjustment.AlreadyValid: item.AlreadyValid++; break;
                case CurseAdjustment.SwappedLiveWall: item.LiveWallSwaps++; break;
                case CurseAdjustment.SwappedDeadWall: item.DeadWallSwaps++; break;
                case CurseAdjustment.Unavoidable: item.Unavoidable++; break;
            }
            if (options.RecordAudit) { audit.Add(record); }
        }

        private static void Shuffle<T>(IList<T> list, Xoshiro256StarStar random)
        {
            for (int index = list.Count - 1; index > 0; index--)
            {
                int other = (int)Below(random, (ulong)(index + 1));
                (list[index], list[other]) = (list[other], list[index]);
            }
        }

        private static ulong Below(Xoshiro256StarStar random, ulong bound)
        {
            if (bound == 0) { throw new ArgumentOutOfRangeException(nameof(bound)); }
            ulong threshold = unchecked(0UL - bound) % bound, value;
            do { value = random.Next(); } while (value < threshold);
            return value % bound;
        }
    }
}
