using System;
using System.Collections.Generic;
using System.Linq;

namespace MaJiangLib
{
    /// <summary>整场固定的四种配牌诅咒；不属于玩家可见规则或模型输入。</summary>
    public enum DemonCurse
    {
        ScatteredInitialHand = 1,
        NoWinningDraw = 2,
        NoDuplicateDraw = 3,
        NoBonusTiles = 4
    }

    /// <summary>V1保留原始起手规则；V2额外要求诅咒3的起手13张牌种互不重复。</summary>
    public enum DemonCurseRulesVersion { V1 = 1, V2 = 2 }

    /// <summary>四个绝对座位各一种诅咒。构造时复制配置，之后不能更改。</summary>
    public sealed class DemonCurseOptions
    {
        private readonly DemonCurse[] curses;
        /// <summary>详细审计可关闭以减少大批量实验的内存开销；统计仍保留。</summary>
        public bool RecordAudit { get; }
        public DemonCurseRulesVersion RulesVersion { get; }
        public IReadOnlyList<DemonCurse> CursesBySeat => Array.AsReadOnly((DemonCurse[])curses.Clone());

        public DemonCurseOptions(IReadOnlyList<DemonCurse> cursesBySeat, bool recordAudit = true)
            : this(cursesBySeat, DemonCurseRulesVersion.V1, recordAudit) { }

        public DemonCurseOptions(IReadOnlyList<DemonCurse> cursesBySeat, DemonCurseRulesVersion rulesVersion, bool recordAudit = true)
        {
            if (rulesVersion != DemonCurseRulesVersion.V1 && rulesVersion != DemonCurseRulesVersion.V2)
            {
                throw new ArgumentOutOfRangeException(nameof(rulesVersion));
            }
            if (cursesBySeat == null || cursesBySeat.Count != 4 ||
                !cursesBySeat.Select(curse => (int)curse).OrderBy(value => value).SequenceEqual(new[] { 1, 2, 3, 4 }))
            {
                throw new ArgumentException("Each absolute seat must have one distinct curse from 1 to 4.", nameof(cursesBySeat));
            }
            curses = cursesBySeat.ToArray(); RecordAudit = recordAudit; RulesVersion = rulesVersion;
        }

        internal DemonCurse At(int seat) => curses[seat];
        internal int SeatOf(DemonCurse curse) => Array.IndexOf(curses, curse);
    }

    public enum CurseTrigger { InitialHand, NormalDraw, RinshanDraw, DoraIndicator, UraIndicator }
    public enum CurseAdjustment { Prepared, AlreadyValid, SwappedLiveWall, SwappedDeadWall, Unavoidable }
    public enum WallZone { None, LiveWall, DeadWall }

    /// <summary>仅供可信宿主查看的隐藏调牌审计，不写入成功动作日志或 mjai 事件。</summary>
    public sealed class CurseAuditRecord
    {
        public int RoundGeneration { get; }
        public int Seat { get; }
        public DemonCurse Curse { get; }
        public CurseTrigger Trigger { get; }
        public CurseAdjustment Adjustment { get; }
        public WallZone SourceZone { get; }
        public int SourceIndex { get; }
        public Tile OriginalTile { get; }
        public WallZone ReplacementZone { get; }
        public int ReplacementIndex { get; }
        public Tile ResultTile { get; }

        internal CurseAuditRecord(int round, int seat, DemonCurse curse, CurseTrigger trigger, CurseAdjustment adjustment,
            WallZone sourceZone = WallZone.None, int sourceIndex = -1, Tile original = null,
            WallZone replacementZone = WallZone.None, int replacementIndex = -1, Tile result = null)
        {
            RoundGeneration = round; Seat = seat; Curse = curse; Trigger = trigger; Adjustment = adjustment;
            SourceZone = sourceZone; SourceIndex = sourceIndex; OriginalTile = original;
            ReplacementZone = replacementZone; ReplacementIndex = replacementIndex; ResultTile = result;
        }
    }

    /// <summary>宿主统计副本。尝试数包含起手准备；听牌诅咒在未听牌时不计触发。</summary>
    public sealed class CurseStatistics
    {
        public int Seat { get; internal set; }
        public DemonCurse Curse { get; internal set; }
        public long Attempts { get; internal set; }
        public long PreparedHands { get; internal set; }
        public long AlreadyValid { get; internal set; }
        public long LiveWallSwaps { get; internal set; }
        public long DeadWallSwaps { get; internal set; }
        public long Unavoidable { get; internal set; }

        internal CurseStatistics Copy() => (CurseStatistics)MemberwiseClone();
    }
}
