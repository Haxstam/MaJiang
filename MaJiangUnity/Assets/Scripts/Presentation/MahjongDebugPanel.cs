using System;
using System.Linq;
using MaJiangLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Color = UnityEngine.Color;

namespace MaJiang.Presentation
{
    /// <summary>使用TMP和uGUI的四座调试工具，与牌桌共享字体设置。</summary>
    public sealed class MahjongDebugPanel : MonoBehaviour
    {
        public bool HideCollapsedButton;
        private GameObject canvasRoot, panel, collapsed;
        private RectTransform choicesRoot;
        private TMP_Text status, details, followLabel, pauseLabel;
        private readonly TMP_Text[] seats = new TMP_Text[4];
        private MahjongTileArt art;
        private string choicesKey;

        public void Initialize(MahjongTileArt tileArt, Action<int> select, Action follow, Action pause,
            Action<long> advance, Action next, Action restart, Action toggle)
        {
            art = tileArt;
            canvasRoot = new GameObject("四座调试画布", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasRoot, gameObject.scene);
            Canvas canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
            CanvasScaler scaler = canvasRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(HideCollapsedButton ? 1080 : 1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            RectTransform body = Rect(canvasRoot.transform, "四座调试面板"); panel = body.gameObject;
            body.anchorMin = body.anchorMax = body.pivot = new Vector2(0, 1); body.anchoredPosition = new Vector2(12, -92); body.sizeDelta = new Vector2(410, 730);
            Image background = panel.AddComponent<Image>(); background.color = new Color(0.035f, 0.09f, 0.08f, 0.98f);
            VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(12,12,12,12);
            layout.spacing = 6; layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            Label(body, "调试标题", "单人四座调试 · 真实 TCP 回环", 30);
            status = Label(body, "连接状态", "连接中", 36);
            RectTransform row = Row(body, "座位切换", 50);
            for (int i = 0; i < 4; i++) { int seat = i; seats[i] = Button(row, "座位" + (i + 1), "", () => select(seat)); }
            followLabel = Button(body, "自动切换开关", "", follow);
            pauseLabel = Button(body, "暂停计时开关", "", pause);
            row = Row(body, "时间推进操作", 34);
            Button(row, "推进一秒", "时间 +1秒", () => advance(1000)); Button(row, "推进三十秒", "时间 +30秒", () => advance(30000)); Button(row, "进入下一局", "下一局", next);
            row = Row(body, "调试管理操作", 34);
            Button(row, "重开整场", "重开整场", restart); Button(row, "收起面板", "收起 (F1)", toggle);
            details = Label(body, "阶段与操作说明", "", 128);
            Label(body, "合法操作标题", "合法操作与具体组合", 26);
            RectTransform viewport = Rect(body, "操作列表视口"); viewport.gameObject.AddComponent<Image>().color = new Color(0,0,0,0.2f);
            viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.viewport = viewport; scroll.movementType = ScrollRect.MovementType.Clamped;
            choicesRoot = Rect(viewport, "合法操作列表"); choicesRoot.anchorMin = new Vector2(0,1); choicesRoot.anchorMax = Vector2.one; choicesRoot.pivot = new Vector2(0.5f,1); choicesRoot.sizeDelta = Vector2.zero;
            VerticalLayoutGroup list = choicesRoot.gameObject.AddComponent<VerticalLayoutGroup>(); list.spacing = 4; list.childControlWidth = list.childControlHeight = true; list.childForceExpandWidth = true; list.childForceExpandHeight = false;
            choicesRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; scroll.content = choicesRoot;
            TMP_Text open = Button(canvasRoot.transform, "展开调试面板", "四座调试 (F1)", toggle);
            collapsed = open.transform.parent.gameObject; RectTransform openRect = (RectTransform)collapsed.transform;
            openRect.anchorMin = openRect.anchorMax = openRect.pivot = new Vector2(0,1); openRect.anchoredPosition = new Vector2(12,-92); openRect.sizeDelta = new Vector2(170,38);
        }

        public void Refresh(bool opened, string state, string[] seatLabels, string information, bool follow, bool pause,
            MatchCommand[] choices, Func<MatchCommand,string> describe, Action<MatchCommand> submit)
        {
            if (canvasRoot == null) { return; }
            panel.SetActive(opened); collapsed.SetActive(!opened && !HideCollapsedButton);
            status.text = state; details.text = information;
            for (int i=0;i<4;i++) { seats[i].text = seatLabels[i]; }
            followLabel.text = "自动切换待操作座位：" + (follow ? "已启用" : "已关闭");
            pauseLabel.text = "对局计时：" + (pause ? "已暂停" : "运行中");
            string key = string.Join("|", choices.Select(command => command.CommandId));
            if (key == choicesKey) { return; } choicesKey = key;
            foreach (Transform child in choicesRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            foreach (MatchCommand command in choices) { MatchCommand selected = command; Button(choicesRoot, "操作选项", describe(command), () => submit(selected)); }
        }

        private TMP_Text Label(Transform parent, string name, string value, float height)
        {
            RectTransform rect = Rect(parent, name); TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = art.GetFont(); text.text = value; text.fontSize = 15; text.color = new Color(0.94f,0.93f,0.88f); text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height; return text;
        }
        private TMP_Text Button(Transform parent, string name, string value, Action action)
        {
            RectTransform rect = Rect(parent, name); Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.14f,0.25f,0.22f);
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 36;
            TMP_Text label = Label(rect, "按钮文字", value, 32); label.alignment = TextAlignmentOptions.Center;
            RectTransform lr = label.rectTransform; lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(5,0); lr.offsetMax = new Vector2(-5,0);
            return label;
        }
        private static RectTransform Row(Transform parent, string name, float height)
        {
            RectTransform rect = Rect(parent, name); rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            HorizontalLayoutGroup row = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); row.spacing = 4; row.childForceExpandWidth = true; row.childForceExpandHeight = true; return rect;
        }
        private static RectTransform Rect(Transform parent, string name)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); return rect;
        }
        private void OnDisable() { if (canvasRoot != null) { canvasRoot.SetActive(false); } }
        private void OnEnable() { if (canvasRoot != null) { canvasRoot.SetActive(true); } }
        private void OnDestroy() { if (canvasRoot != null) { Destroy(canvasRoot); } }
    }
}
