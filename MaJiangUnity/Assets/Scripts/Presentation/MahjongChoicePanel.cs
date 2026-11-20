using System;
using System.Collections.Generic;
using System.Linq;
using MaJiangLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MaJiang.Presentation
{
    /// <summary>复用作者的两格选择组；杠使用文字格加单张牌，选项宽度固定。</summary>
    public sealed class MahjongChoicePanel : MonoBehaviour
    {
        public RectTransform List;
        public RectTransform ChoicePrefab;
        public Button Cancel;
        public MahjongTileArt Art;
        public float GroupSpacing = 10;
        public float HorizontalPadding = 92;
        public Action Cancelled;
        private void Awake() { if (Cancel != null) { Cancel.onClick.AddListener(() => { Hide(); Cancelled?.Invoke(); }); } }
        public void Hide() => gameObject.SetActive(false);
        public void Show(IReadOnlyList<PlayerActionData> choices, Action<PlayerActionData> selected)
        {
            foreach (Transform child in List) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var layout = MahjongTableLayouts.Horizontal(List, GroupSpacing, TextAnchor.MiddleCenter); layout.spacing = GroupSpacing;
            layout.childControlWidth = layout.childControlHeight = false;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            foreach (PlayerActionData choice in choices)
            {
                RectTransform option = Instantiate(ChoicePrefab, List); option.name = "鸣牌选择组";
                MahjongTableLayouts.Size(option, ChoicePrefab.rect.width, ChoicePrefab.rect.height);
                bool kan = choice.PlayerAction == PlayerAction.ClosedKan || choice.PlayerAction == PlayerAction.AddedKan;
                Transform operation = option.Find("操作文字");
                var cards = option.GetComponentsInChildren<MahjongTileWidget>(true).OrderBy(card => card.transform.GetSiblingIndex()).ToArray();
                HorizontalLayoutGroup row = option.GetComponent<HorizontalLayoutGroup>();
                row.childControlWidth = row.childControlHeight = false; row.childForceExpandWidth = row.childForceExpandHeight = false; row.spacing = 0;
                operation.gameObject.SetActive(kan);
                if (kan)
                {
                    operation.GetComponentInChildren<TMP_Text>(true).text = "杠";
                    operation.SetAsFirstSibling(); cards[0].gameObject.SetActive(false);
                    cards[1].gameObject.SetActive(true); cards[1].Bind(Art, choice.TargetTile, false);
                }
                else
                {
                    Tile[] used = choice.Tiles.OrderBy(tile => tile).ToArray();
                    for (int i = 0; i < cards.Length; i++) { cards[i].gameObject.SetActive(i < used.Length); if (i < used.Length) { cards[i].Bind(Art, used[i], false); } }
                }
                // 牌只承担图形显示。禁用其指针处理，避免吞掉父级选择按钮的点击。
                foreach (var card in cards) { card.Interactive = false; card.enabled = false; }
                option.GetComponent<Button>().onClick.AddListener(() => { Hide(); selected(choice); });
            }
            gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(List);
            float width = LayoutUtility.GetPreferredWidth(List);
            List.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            // 外框只随内容变宽；保留作者的外框留白与高度。
            ((RectTransform)transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width + HorizontalPadding);
            RectTransform close = (RectTransform)Cancel.transform;
            close.anchorMin = close.anchorMax = close.pivot = Vector2.one;
            close.anchoredPosition = new Vector2(-6, -6);
        }
    }
}
