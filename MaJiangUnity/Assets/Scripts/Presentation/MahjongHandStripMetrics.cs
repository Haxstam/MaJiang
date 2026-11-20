using System;

namespace MaJiang.Presentation
{
    /// <summary>在固定牌条中保留摸牌间隔，让过宽的牌体统一缩放。</summary>
    public readonly struct MahjongHandStripMetrics
    {
        public readonly float Scale;
        public readonly float DrawX;
        public readonly float MeldX;

        public MahjongHandStripMetrics(float width, float handWidth, float drawnWidth, float meldWidth, float drawSpacing)
        {
            float gap = drawnWidth > 0 ? drawSpacing : 0;
            float natural = handWidth + drawnWidth + meldWidth;
            Scale = natural > 0 ? Math.Min(1, Math.Max(0, width - gap) / natural) : 1;
            DrawX = handWidth * Scale + gap;
            MeldX = Math.Max(0, width - meldWidth * Scale);
        }
    }
}
