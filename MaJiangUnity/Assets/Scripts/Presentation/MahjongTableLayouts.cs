using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    /// <summary>牌桌区域使用标准uGUI布局；已有组件的Inspector设置优先保留。</summary>
    public static class MahjongTableLayouts
    {
        public static MahjongRiverLayout RiverGrid(RectTransform rect, float width)
        {
            MahjongRiverLayout grid = rect.GetComponent<MahjongRiverLayout>();
            if (grid == null)
            {
                LayoutGroup previous = rect.GetComponent<LayoutGroup>();
                if (previous != null) { Object.DestroyImmediate(previous); }
                grid = rect.gameObject.AddComponent<MahjongRiverLayout>();
            }
            grid.cellSize = new Vector2(width, width * 4 / 3);
            grid.spacing = Vector2.zero;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft; grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 6;
            return grid;
        }
        public static HorizontalLayoutGroup Horizontal(RectTransform rect, float gap, TextAnchor align)
        {
            HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();
            if (layout != null) { return layout; }
            layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = gap; layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return layout;
        }
        public static VerticalLayoutGroup Vertical(RectTransform rect, float gap, TextAnchor align)
        {
            VerticalLayoutGroup layout = rect.GetComponent<VerticalLayoutGroup>();
            if (layout != null) { return layout; }
            layout = rect.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = gap; layout.childAlignment = align;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return layout;
        }
        public static LayoutElement Size(RectTransform rect, float width, float height)
        {
            LayoutElement size = rect.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width; size.minHeight = size.preferredHeight = height;
            size.flexibleWidth = size.flexibleHeight = 0; return size;
        }
        public static RectTransform Slot(Transform parent, string name, float width, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = new Vector2(width, height); Size(rect,width,height); return rect;
        }
    }
}
