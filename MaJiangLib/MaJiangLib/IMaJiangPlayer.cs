namespace MaJiangLib
{
    /// <summary>
    /// 麻将玩家
    /// </summary>
    public interface IMaJiangPlayer
    {
        public void MoPai(string pai);
        public void DaPai(string pai);
        public void Gan(string pai);
        public void Chi(string pai);
        public void Pen(string pai);
        public void He();
    }
}