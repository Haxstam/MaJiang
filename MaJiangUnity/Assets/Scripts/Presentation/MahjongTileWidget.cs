using System;
using MaJiangLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace MaJiang.Presentation
{
    /// <summary>单张UI牌的表现和指针交互，不修改牌局。手切/摸切由控制器提交给现用操作机。</summary>
    public sealed class MahjongTileWidget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public RawImage Body;
        public RawImage Face;
        public TableShape Selection;
        public TableShape DoraMark;
        public bool Interactive;
        public int HandIndex;
        public bool Drawn;
        public Tile Tile { get; private set; }
        private bool selected;
        private Vector2 rest;
        private RectTransform Rect => (RectTransform)transform;
        public event Action<MahjongTileWidget> Clicked;

        public void Bind(MahjongTileArt art, Tile tile, bool hidden, bool dora = false)
        {
            Tile = tile;
            Body.texture = hidden ? art.Back : art.Front;
            Body.color = hidden ? new Color(0.48f, 0.56f, 0.53f) : Color.white;
            Face.texture = hidden ? null : art.Face(tile);
            Face.enabled = !hidden && tile != null;
            Face.raycastTarget = false;
            Selection.gameObject.SetActive(false);
            DoraMark.gameObject.SetActive(dora);
            rest = Rect.anchoredPosition;
        }

        public void Select(bool value)
        {
            selected = value;
            Selection.gameObject.SetActive(value);
            Rect.anchoredPosition = rest + Vector2.up * (selected ? 15 : 0);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Interactive && !selected) { Rect.anchoredPosition = rest + Vector2.up * 7; }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (Interactive && !selected) { Rect.anchoredPosition = rest; }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactive && eventData.button == PointerEventData.InputButton.Left) { Clicked?.Invoke(this); }
        }
    }
}
