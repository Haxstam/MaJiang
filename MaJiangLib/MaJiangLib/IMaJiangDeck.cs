namespace MaJiangLib
{
    /// <summary>
    /// 麻将牌堆
    /// </summary>
    public interface IMaJiangDeck
    {
        
        
        /// <summary>
        /// 牌山貌似不会在玩家手里也生成
        /// 所以不需要写在lib里
        /// 等更改.....
        /// </summary>
        /// <param name="seed"></param>
        public void InitDesk(long seed);

        public void MoPai();
    }
}