using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    /// <summary>固定长度牌条：暗手靠左、副露靠右，摸牌距暗手10像素。</summary>
    [AddComponentMenu("Mahjong/UI/固定长度手牌条")]
    public sealed class MahjongHandStripLayout : LayoutGroup
    {
        public RectTransform Concealed;
        public RectTransform Drawn;
        public RectTransform Melds;
        public float DrawSpacing = 10;
        [Min(1)] public float TileWidth = 40;

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            SetLayoutInputForAxis(0, rectTransform.rect.width, -1, 0);
        }
        public override void CalculateLayoutInputVertical() => SetLayoutInputForAxis(0, rectTransform.rect.height, -1, 1);
        public override void SetLayoutHorizontal() => Arrange();
        public override void SetLayoutVertical() => Arrange();
        private void Arrange()
        {
            if (Concealed == null || Melds == null) { return; }
            float handWidth = LayoutUtility.GetPreferredWidth(Concealed);
            float meldWidth = LayoutUtility.GetPreferredWidth(Melds);
            // Unity可能把受布局驱动的Rect尺寸保存为0；牌宽用独立字段持有。
            float drawnWidth = Drawn != null && Drawn.gameObject.activeSelf ? TileWidth : 0;
            var metrics = new MahjongHandStripMetrics(rectTransform.rect.width, handWidth, drawnWidth, meldWidth, DrawSpacing);
            // 副露面子的上层杠牌允许向上外溢，普通三张牌与暗手底边对齐。
            float cardHeight = Mathf.Max(LayoutUtility.GetPreferredHeight(Concealed), TileWidth * 4 / 3);
            m_Tracker.Add(this, Melds, DrivenTransformProperties.SizeDeltaY);
            Melds.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cardHeight);
            Place(Concealed, 0, handWidth, metrics.Scale);
            if (Drawn != null) { Place(Drawn, metrics.DrawX, TileWidth, metrics.Scale); }
            Place(Melds, Drawn != null ? metrics.MeldX : handWidth * metrics.Scale, meldWidth, metrics.Scale);
        }
        private void Place(RectTransform child, float x, float width, float scale)
        {
            m_Tracker.Add(this, child, DrivenTransformProperties.Anchors | DrivenTransformProperties.Pivot |
                DrivenTransformProperties.AnchoredPosition | DrivenTransformProperties.SizeDeltaX | DrivenTransformProperties.Scale);
            child.anchorMin = child.anchorMax = child.pivot = new Vector2(0, 1);
            child.localScale = Vector3.one * scale;
            child.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            child.anchoredPosition = new Vector2(x, 0);
        }
    }
}
