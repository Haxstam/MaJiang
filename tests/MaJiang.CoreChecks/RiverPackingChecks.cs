using System;
using System.Linq;
using MaJiang.Presentation;

internal static partial class Program
{
    private static void RiverNear(float actual, float expected)
    {
        if (Math.Abs(actual - expected) > 0.001f) { throw new InvalidOperationException($"River coordinate {actual} != {expected}."); }
    }
    private static void CheckRiverBounds(bool[] sideways)
    {
        const float width = 30, height = 40;
        RiverTilePlacement[] layout = MahjongRiverPacking.Pack(sideways, width, height);
        float previousRight = 0;
        for (int i = 0; i < layout.Length; i++)
        {
            if (i % 6 == 0) { previousRight = 0; }
            // 将原始30x40矩形绕中心旋转后的可见边界还原，检查相邻边贴合与底边齐平。
            float visibleLeft = layout[i].X + width / 2 - (sideways[i] ? height : width) / 2;
            float visibleRight = visibleLeft + (sideways[i] ? height : width);
            float visibleBottom = layout[i].Y + height / 2 + (sideways[i] ? width : height) / 2;
            RiverNear(visibleLeft, previousRight);
            RiverNear(visibleBottom, (i / 6 + 1) * height);
            previousRight = visibleRight;
        }
    }
    private static void RiverNormalPacking()
    {
        bool[] flags = new bool[18]; CheckRiverBounds(flags);
        var positions = MahjongRiverPacking.Pack(flags, 30, 40);
        for (int i = 0; i < flags.Length; i++)
        {
            RiverNear(positions[i].X, i % 6 * 30);
            RiverNear(positions[i].Y, i / 6 * 40);
        }
    }
    private static void RiverRiichiPacking()
    {
        for (int declaration = 0; declaration < 6; declaration++)
        {
            bool[] flags = new bool[12]; flags[declaration] = true;
            CheckRiverBounds(flags);
            var positions = MahjongRiverPacking.Pack(flags, 30, 40);
            float lastRight = positions[5].X + 15 + (flags[5] ? 20 : 15);
            RiverNear(lastRight, 190); // 正常行180，立直行凸出10。
            RiverNear(positions[6].X, 0); RiverNear(positions[6].Y, 40);
        }
    }
    private static void RiverIndependentRows()
    {
        bool[] flags = Enumerable.Range(0, 19).Select(i => i == 2 || i == 6 || i == 17).ToArray();
        CheckRiverBounds(flags);
        var positions = MahjongRiverPacking.Pack(flags, 30, 40);
        RiverNear(positions[18].X, 0); RiverNear(positions[18].Y, 120);
        if (MahjongRiverPacking.Pack(Array.Empty<bool>(), 30, 40).Length != 0) { throw new InvalidOperationException("Empty river layout failed."); }
    }
}
