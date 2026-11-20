using MaJiangLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace MaJiang.Presentation
{
    /// <summary>用作者已有的听牌按钮、行模板和振听面板显示预估结果。</summary>
    public sealed class MahjongTenpaiHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform List;
        public RectTransform EntryPrefab;
        public GameObject FuritenPanel;
        public Color WarningColor = new(0.67f, 0.13f, 0.13f);
        public Color LimitColor = new(0.85f, 0.39f, 0.08f);
        private bool hovering;
        private bool showSelectedDiscard;

        public void Show(MahjongTileArt art, VisibleMatchState visible, PlayerActionData discard)
        {
            if (visible == null || visible.OperationStage == MatchOperationStage.RoundEnded) { Hide(); return; }
            FuritenPanel.SetActive(visible.FuritenFlags != 0);
            var waits = TenpaiForecast.GetWaits(visible, discard);
            showSelectedDiscard = discard != null && waits.Count > 0;
            while (List.childCount < waits.Count) { Instantiate(EntryPrefab, List); }
            Color normalHan = Background(EntryPrefab, "番数").color;
            Color normalRemaining = Background(EntryPrefab, "剩余张数").color;
            for (int i = 0; i < List.childCount; i++)
            {
                Transform entry = List.GetChild(i);
                entry.gameObject.SetActive(i < waits.Count);
                if (i >= waits.Count) { continue; }
                TenpaiWait wait = waits[i];
                entry.Find("振听提示").gameObject.SetActive(wait.Furiten);
                var card = entry.GetComponentInChildren<MahjongTileWidget>(true);
                card.Interactive = false;
                card.Bind(art, wait.Tile, false);

                ScoredHand score = wait.RonScore;
                Graphic han = Background(entry, "番数");
                han.GetComponentInChildren<TMP_Text>(true).text = score == null ? "无役" : score.Yakuman > 0 ? "役满" : $"{score.Han}番";
                han.color = score == null ? WarningColor : score.Yakuman > 0 || score.Han >= 13 ? LimitColor : normalHan;

                Graphic remaining = Background(entry, "剩余张数");
                remaining.GetComponentInChildren<TMP_Text>(true).text = $"{wait.Remaining}张";
                remaining.color = wait.Remaining == 0 ? WarningColor : normalRemaining;
            }
            gameObject.SetActive(waits.Count > 0);
            RefreshVisibility();
            if (List.gameObject.activeInHierarchy) { LayoutRebuilder.ForceRebuildLayoutImmediate(List); }
        }

        public void Hide()
        {
            showSelectedDiscard = false;
            hovering = false;
            List.gameObject.SetActive(false);
            FuritenPanel.SetActive(false);
            gameObject.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData) { hovering = true; RefreshVisibility(); }
        public void OnPointerExit(PointerEventData eventData) { hovering = false; RefreshVisibility(); }
        private void OnDisable()
        {
            hovering = false;
            if (List != null) { RefreshVisibility(); }
        }
        private void RefreshVisibility() => List.gameObject.SetActive(showSelectedDiscard || (hovering && isActiveAndEnabled));
        private static Graphic Background(Transform entry, string name) => entry.Find(name).GetComponent<Graphic>();
    }
}
