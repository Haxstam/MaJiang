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
        public Image CardBody;
        public Image CardFace;
        public Image Back;
        public Image BlueMask;
        public Image BlackMask;
        public bool Interactive;
        public int HandIndex;
        public bool Drawn;
        public bool HoverSource;
        public bool FaceVisible { get; private set; }
        public Tile Tile { get; private set; }
        private bool selected;
        private bool discardBlocked;
        private MahjongDoraSweep doraSweep;
        private Vector2 rest;
        private RectTransform Rect => (RectTransform)transform;
        public void CaptureRest() => rest = Rect.anchoredPosition;
        public event Action<MahjongTileWidget> Clicked;
        public event Action<MahjongTileWidget, bool> Hovered;

        public void Bind(MahjongTileArt art, Tile tile, bool hidden, bool dora = false)
        {
            Tile = tile;
            FaceVisible = !hidden && tile != null;
            if (doraSweep != null) { doraSweep.enabled = false; }
            SetDiscardBlocked(false);
            if (CardBody == null) { CardBody = GetComponent<Image>(); }
            if (CardBody != null)
            {
                if (CardFace == null) { CardFace = transform.Find("Image")?.GetComponent<Image>(); }
                if (Back == null) { Back = transform.Find("Back")?.GetComponent<Image>(); }
                if (BlueMask == null) { BlueMask = transform.Find("BlueMask")?.GetComponent<Image>(); }
                CardFace.sprite = hidden ? null : art.SpriteOf(art.Face(tile));
                CardFace.enabled = !hidden && tile != null; CardFace.raycastTarget = false;
                Back.gameObject.SetActive(hidden);
                if (BlueMask != null) { BlueMask.gameObject.SetActive(false); BlueMask.raycastTarget = false; }
                rest = Rect.anchoredPosition;
                return;
            }
            // 兼容尚未重新导入的旧场景；新生成的牌均来自Card.prefab。
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
            selected = value && !discardBlocked;
            if (Selection != null) { Selection.gameObject.SetActive(selected); }
            Rect.anchoredPosition = rest + Vector2.up * (selected ? 15 : 0);
        }

        public bool MatchesDiscard(PlayerActionData action, bool riichi)
        {
            PlayerAction type = riichi ? PlayerAction.Riichi : Drawn ? PlayerAction.TakeCut : PlayerAction.HandCut;
            return action.PlayerAction == type && MaJiangLib.Tile.ExactlyEquals(action.TargetTile, Tile);
        }

        public void SetDiscardBlocked(bool blocked)
        {
            discardBlocked = blocked;
            if (BlackMask == null) { BlackMask = transform.Find("BlackMask")?.GetComponent<Image>(); }
            if (BlackMask != null)
            {
                BlackMask.raycastTarget = false;
                BlackMask.gameObject.SetActive(blocked);
            }
            if (blocked) { Select(false); }
        }

        public void Highlight(bool value) { if (BlueMask != null) { BlueMask.gameObject.SetActive(value && FaceVisible); } }
        private void OnDisable() { if (HoverSource) { Hovered?.Invoke(this, false); } }

        internal void PlayDoraSweep(MahjongDoraSweepSettings settings)
        {
            if (!FaceVisible) { return; }
            if (doraSweep == null)
            {
                doraSweep = AddOverlay<MahjongDoraSweep>("宝牌扫光");
                Graphic face = CardFace != null ? CardFace : Face;
                if (face != null && face.transform.parent == transform)
                {
                    doraSweep.transform.SetSiblingIndex(face.transform.GetSiblingIndex() + 1);
                }
            }
            doraSweep.Play(settings, HandAnalysis.Face(Tile));
        }

        internal Image AddDiscardFrame(Sprite sprite, string name)
        {
            Image frame = AddOverlay<Image>(name);
            frame.sprite = sprite;
            return frame;
        }

        private T AddOverlay<T>(string name) where T : Graphic
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            layer.layer = gameObject.layer;
            var rect = (RectTransform)layer.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            T graphic = layer.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        public void SetBodyTint(Color color)
        {
            if (CardBody != null) { CardBody.color = color; if (Back != null && Back.gameObject.activeSelf) { Back.color = color; } }
            else if (Body != null) { Body.color = color; }
        }

        public void MarkRiichi()
        {
            if (Selection != null) { Selection.color = new Color(0.79f, 0.38f, 0.30f); Selection.gameObject.SetActive(true); }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (discardBlocked) { return; }
            if (HoverSource) { Hovered?.Invoke(this, true); }
            if (Interactive && !selected) { Rect.anchoredPosition = rest + Vector2.up * 7; }
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (discardBlocked) { return; }
            if (HoverSource) { Hovered?.Invoke(this, false); }
            if (Interactive && !selected) { Rect.anchoredPosition = rest; }
        }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Interactive && !discardBlocked && eventData.button == PointerEventData.InputButton.Left) { Clicked?.Invoke(this); }
        }
    }
}
