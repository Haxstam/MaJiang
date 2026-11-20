namespace MaJiang.Presentation
{
    /// <summary>画面依次为下方自家、右侧下家、上方对家、左侧上家；绝对座位按此方向递增。</summary>
    public static class MahjongSeatOrder
    {
        public static int AbsoluteSeat(int self, int relative) => (self + relative) % 4;
        public static int WindIndex(int seat, int dealer) => (seat - dealer + 4) % 4;
        public static int PublicSeat(int viewedSeat, int relative, int fixedSeat) => AbsoluteSeat(fixedSeat >= 0 ? fixedSeat : viewedSeat, relative);
    }
}
