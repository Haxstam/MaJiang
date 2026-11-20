using System;
using System.Collections.Generic;

namespace MaJiang.Presentation
{
    public readonly struct RiverTilePlacement
    {
        public readonly float X, Y;
        public RiverTilePlacement(float x, float y) { X = x; Y = y; }
    }

    /// <summary>六张一行，横牌占实际宽度并推开同行后续牌；位置供原始竖牌Rect旋转后使用。</summary>
    public static class MahjongRiverPacking
    {
        public const int Columns = 6;
        public static RiverTilePlacement[] Pack(IReadOnlyList<bool> sideways, float width, float height)
        {
            if (width <= 0 || height <= 0) { throw new ArgumentOutOfRangeException(nameof(width)); }
            RiverTilePlacement[] result = new RiverTilePlacement[sideways.Count];
            float x = 0;
            for (int i = 0; i < sideways.Count; i++)
            {
                if (i % Columns == 0) { x = 0; }
                // 横牌与竖牌底边对齐，保留牌的原始长方形宽高和中心旋转。
                float offset = sideways[i] ? (height - width) / 2 : 0;
                result[i] = new RiverTilePlacement(x + offset, i / Columns * height + offset);
                x += sideways[i] ? height : width;
            }
            return result;
        }
    }
}
