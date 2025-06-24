namespace MaJiangLib
{
    /// <summary>
    /// 手牌
    /// </summary>
    public interface IMaJiangHandel
    {
        public void MoPai(string pai);
        public void DaPai(string pai);
        /// <summary>
        /// 可以吃某个牌
        /// </summary>
        /// <param name="pai">要被吃的牌</param>
        public bool CouldChi(string pai);
        public bool CouldPen(string pai);
        public bool CouldGan(string pai);
        public bool CouldHe();
        // /// <summary>
        // /// 计算番数
        // /// </summary>
        // public int CalculateYi();
    }
}