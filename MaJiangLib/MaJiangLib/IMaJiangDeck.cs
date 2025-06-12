namespace MaJiangLib
{
    /// <summary>
    /// 麻将牌堆
    /// </summary>
    public interface IMaJiangDeck
    {
        public void InitDesk(long seed);

        public void MoPai();
    }
}