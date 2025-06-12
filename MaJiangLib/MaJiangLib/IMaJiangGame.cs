namespace MaJiangLib
{
    /// <summary>
    /// 麻将游戏控制
    /// </summary>
    public interface IMaJiangGame
    {
        /// <summary>
        /// 牌山
        /// </summary>
        public IMaJiangDeck Deck { get; }
    }
}