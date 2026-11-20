using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    /// <summary>紧密六列牌河。Card为直接子节点；立直牌横置只增加本行长度，不预留空格。</summary>
    [AddComponentMenu("Mahjong/UI/紧密牌河布局")]
    public sealed class MahjongRiverLayout : GridLayoutGroup
    {
        public override void SetLayoutHorizontal()
        {
            foreach (RectTransform card in rectChildren)
            {
                // Card自己的比例和旋转保持不变，布局不使用正方形容器。
                SetChildAlongAxis(card, 0, 0, cellSize.x);
                SetChildAlongAxis(card, 1, 0, cellSize.y);
            }
        }
        public override void SetLayoutVertical()
        {
            bool[] rotated = rectChildren.Select(card => Mathf.Abs(Mathf.Sin(card.localEulerAngles.z * Mathf.Deg2Rad)) > 0.5f).ToArray();
            RiverTilePlacement[] packed = MahjongRiverPacking.Pack(rotated, cellSize.x, cellSize.y);
            for (int i = 0; i < rectChildren.Count; i++)
            {
                RectTransform card = rectChildren[i];
                SetChildAlongAxis(card, 0, padding.left + packed[i].X, cellSize.x);
                SetChildAlongAxis(card, 1, padding.top + packed[i].Y, cellSize.y);
            }
        }
    }
}
