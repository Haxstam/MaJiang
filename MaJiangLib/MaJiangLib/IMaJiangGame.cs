namespace MaJiangLib
{
    /// <summary>
    /// 麻将游戏控制
    /// 也就是麻将在进行中的一个接口
    /// </summary>
    public interface IMaJiangGame
    {
        /// <summary>
        /// 牌山
        /// </summary>
        public IMaJiangDeck Deck { get; }
    }
}