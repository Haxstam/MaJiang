#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using MaJiang.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MaJiang.EditorTools
{
    /// <summary>创建独立可编辑uGUI预览场景与牌预制体，并从真实Unity摄像机导出评审图。</summary>
    public static class TablePreviewBuilder
    {
        public const string ScenePath = "Assets/Scenes/TablePreview.unity";
        private const string ArtRoot = "Assets/Images/riichi-mahjong-tiles-master/Export/Regular/";
        public const string DebugScenePath = "Assets/Scenes/TableDebug.unity";
        private static readonly Color Ink = Hex("0E211F");
        private static readonly Color Felt = Hex("19453D");
        private static readonly Color Gold = Hex("CFB77F");
        private static readonly Color Ivory = Hex("F1ECE1");
        private static readonly Color Muted = Hex("9AB2A7");

        [MenuItem("麻将/四座调试/创建独立调试场景")]
        public static void BuildDebugScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("请退出播放模式后再创建调试场景。"); return; }
            if (File.Exists(DebugScenePath))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(DebugScenePath));
                Debug.Log("调试场景已存在，保留你的编辑：" + DebugScenePath); return;
            }
            if (!File.Exists(ScenePath)) { BuildScene(); }
            if (!AssetDatabase.CopyAsset(ScenePath, DebugScenePath)) { throw new IOException("无法复制牌桌预览场景。"); }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(DebugScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                MahjongTableView view = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MahjongTableView>(true)).Single();
                foreach (MahjongTablePreview preview in view.GetComponents<MahjongTablePreview>()) { Object.DestroyImmediate(preview); }
                MahjongTableDebug debug = view.gameObject.AddComponent<MahjongTableDebug>(); debug.View = view;
                view.HeaderState.text = "单人四座调试 · 进入Play后自动开局";
                view.Hint.text = "数字键 1–4 切换座位 · F1 调试面板";
                view.SetActions(new string[6], new bool[6]); view.Modal.SetActive(false);
                foreach (Text text in view.GetComponentsInChildren<Text>(true)) { text.font = null; }
                if (!EditorSceneManager.SaveScene(scene, DebugScenePath)) { throw new IOException("无法保存调试场景。"); }
                Debug.Log("四座调试场景已创建，请打开后进入Play：" + DebugScenePath);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) { SceneManager.SetActiveScene(previous); }
                EditorSceneManager.CloseScene(scene, true);
            }
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(DebugScenePath));
        }

        [MenuItem("麻将/牌桌预览/打开预览场景")]
        public static void OpenPreview()
        {
            if (!File.Exists(ScenePath)) { BuildScene(); }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { EditorSceneManager.OpenScene(ScenePath); }
        }

        [MenuItem("麻将/牌桌预览/仅生成独立场景")]
        public static void BuildScene() => Build(false);

        /// <summary>CLI入口；仅覆盖本工具生成的专用预览资产，不修改SampleScene或构建场景列表。</summary>
        [MenuItem("麻将/牌桌预览/生成场景与评审图")]
        public static void BuildAndCapture() => Build(true);

        private static void Build(bool capture)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("请退出播放模式后再生成牌桌预览场景。");
                return;
            }
            EnsureFolder("Assets/Presentation"); EnsureFolder("Assets/Prefabs"); EnsureFolder("Assets/Prefabs/UI");
            AssetDatabase.Refresh();
            MahjongTileArt art = MakeArt();
            AssetDatabase.SaveAssetIfDirty(art);
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                MahjongTileWidget prefab = MakeTilePrefab();
                Camera camera = new GameObject("Preview Camera").AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 540;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink; camera.nearClipPlane = 0.1f; camera.farClipPlane = 100;
                GameObject events = new("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(null);
                MahjongTableView view = MakeLayout(camera, art, prefab);
                MahjongTablePreview preview = view.gameObject.AddComponent<MahjongTablePreview>(); preview.View = view;
                preview.InitializePreview();
                foreach (Text text in view.GetComponentsInChildren<Text>(true)) { text.font = null; }
                if (!EditorSceneManager.SaveScene(scene, ScenePath))
                {
                    throw new IOException("无法保存牌桌预览场景：" + ScenePath);
                }
                Debug.Log("TABLE_PREVIEW_READY " + ScenePath);
                if (!capture) { return; }
                view.ApplyFonts();
                string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../docs/previews")); Directory.CreateDirectory(output);
                Capture(camera, view, Path.Combine(output, "table-preview.png"));
                // 调用和实际按钮相同的操作路径，结算图来自真实计分结果。
                typeof(MahjongTablePreview).GetMethod("OnAction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(preview, new object[] { 4 });
                Capture(camera, view, Path.Combine(output, "table-result.png"));
                Debug.Log("TABLE_PREVIEW_READY " + ScenePath + " | " + output);
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) { SceneManager.SetActiveScene(previous); }
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static MahjongTileArt MakeArt()
        {
            const string path = "Assets/Presentation/MahjongTileArt.asset";
            MahjongTileArt art = AssetDatabase.LoadAssetAtPath<MahjongTileArt>(path);
            if (art == null) { art = ScriptableObject.CreateInstance<MahjongTileArt>(); AssetDatabase.CreateAsset(art, path); }
            Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + name + ".png") ?? throw new FileNotFoundException("Missing tile art " + name);
            art.Front = Load("Front"); art.Back = Load("Back");
            string[] suits = { "Man", "Pin", "Sou" };
            for (int face = 0; face < 27; face++) { art.Faces[face] = Load(suits[face / 9] + (face % 9 + 1)); }
            string[] honors = { "Ton", "Nan", "Shaa", "Pei", "Haku", "Hatsu", "Chun" };
            for (int index = 0; index < 7; index++) { art.Faces[27 + index] = Load(honors[index]); }
            for (int suit = 0; suit < 3; suit++) { art.RedFives[suit] = Load(suits[suit] + "5-Dora"); }
            EditorUtility.SetDirty(art); return art;
        }

        private static MahjongTileWidget MakeTilePrefab()
        {
            RectTransform root = Rect(null, "Mahjong Tile", 0, 0, 62, 82.667f);
            MahjongTileWidget tile = root.gameObject.AddComponent<MahjongTileWidget>();
            tile.Selection = Shape(root, "Selection Outline", 31, 41.333f, 68, 88.667f, Gold, 10, 1.8f);
            Stretch((RectTransform)tile.Selection.transform, new Vector2(6, 6));
            tile.Body = Raw(root, "Porcelain Body");
            Shadow shadow = tile.Body.gameObject.AddComponent<Shadow>(); shadow.effectDistance = new Vector2(0, -3); shadow.effectColor = new Color(0, 0, 0, 0.28f);
            tile.Face = Raw(root, "Original Tile Artwork"); tile.Face.raycastTarget = false;
            tile.DoraMark = Shape(root, "Dora Dot", 55, 75, 4, 4, Gold, 2);
            RectTransform mark = (RectTransform)tile.DoraMark.transform;
            mark.anchorMin = mark.anchorMax = new Vector2(1, 0); mark.anchoredPosition = new Vector2(-6, 7);
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root.gameObject, "Assets/Prefabs/UI/MahjongTile.prefab");
            Object.DestroyImmediate(root.gameObject); return asset.GetComponent<MahjongTileWidget>();
        }

        private static MahjongTableView MakeLayout(Camera camera, MahjongTileArt art, MahjongTileWidget prefab)
        {
            GameObject canvasObject = new("Table Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            RectTransform root = Rect(canvasObject.transform, "Table Layout · 1920 × 1080", 0, 0, 1920, 1080);
            root.anchorMin = root.anchorMax = Vector2.one / 2; root.anchoredPosition = Vector2.zero;
            MahjongTableView view = root.gameObject.AddComponent<MahjongTableView>(); view.Art = art; view.TilePrefab = prefab;
            Shape(root, "Ink Background", 960, 540, 1920, 1080, Ink, 0);
            Shape(root, "Header Rule", 960, 76, 1792, 1, Alpha(Muted, 0.16f), 0);
            Shape(root, "Brand Seal", 83, 37, 38, 38, Alpha(Gold, 0.75f), 7, 1);
            Label(root, "Brand Glyph", "雀", 83, 36, 38, 36, 24, Gold);
            Label(root, "Brand Title", "立直麻将", 189, 30, 142, 30, 21, Ivory, TextAnchor.MiddleLeft);
            Label(root, "Brand Subtitle", "R I I C H I   M A H J O N G", 200, 53, 164, 16, 8, Alpha(Muted, 0.7f), TextAnchor.MiddleLeft);
            Label(root, "Room Title", "友 人 桌", 387, 38, 130, 30, 14, Muted, TextAnchor.MiddleLeft);
            view.HeaderState = Label(root, "Match Type", "四人东南战  /  01", 960, 37, 360, 30, 14, Muted);
            Shape(root, "Preview Dot", 1461, 37, 5, 5, Hex("91BFA7"), 3);
            Label(root, "Preview Label", "界面预览", 1512, 37, 94, 26, 12, Muted);
            view.RoomButton = Button(root, "Room Button", "房间", 1665, 37, 96, 36, false, out _);
            view.ResetButton = Button(root, "Reset Preview", "重置预览", 1804, 37, 136, 36, false, out _);

            Shape(root, "Table Shadow", 960, 548, 1844, 890, new Color(0, 0, 0, 0.24f), 28);
            Shape(root, "Table Rail", 960, 538, 1840, 884, Hex("112F29"), 26);
            Shape(root, "Table Rail Edge", 960, 537, 1838, 882, Alpha(Gold, 0.25f), 26, 1);
            Shape(root, "Jade Felt", 960, 529, 1806, 846, Felt, 20);
            Shape(root, "Quiet Inset", 960, 529, 1768, 808, Alpha(Muted, 0.075f), 14, 1);
            Shape(root, "Center Circle", 960, 488, 574, 574, Alpha(Muted, 0.05f), 287, 1);
            TableShape diamond = Shape(root, "Center Diamond", 960, 488, 305, 305, Alpha(Gold, 0.065f), 14, 1);
            diamond.transform.localEulerAngles = new Vector3(0, 0, 45);
            Label(root, "Dora Caption", "宝 牌 指 示", 222, 133, 222, 26, 12, Muted, TextAnchor.MiddleLeft);
            view.Indicators = Rect(root, "Dora Indicators", 221, 186, 226, 62);
            Label(root, "Rules Summary", "赤宝牌  ·  食断  ·  一番缚", 220, 241, 224, 22, 11, Alpha(Muted, 0.6f), TextAnchor.MiddleLeft);
            Label(root, "Table Edition", "友人  /  東南戦", 1702, 145, 216, 30, 17, Alpha(Ivory, 0.7f));
            Shape(root, "Edition Rule", 1702, 172, 130, 1, Alpha(Gold, 0.22f), 0);
            Label(root, "Table Edition Small", "一桌四人，一期一会", 1702, 195, 220, 24, 11, Alpha(Muted, 0.6f));
            Label(root, "Felt Watermark", "雀", 1702, 740, 160, 100, 78, Alpha(Ivory, 0.045f));
            Label(root, "Felt Watermark Caption", "R I I C H I", 1702, 806, 150, 24, 10, Alpha(Muted, 0.19f));

            Shape(root, "Center Plate Shadow", 960, 493, 238, 186, Alpha(Ink, 0.4f), 14);
            Shape(root, "Center Plate", 960, 488, 236, 184, Hex("112F2A"), 13);
            Shape(root, "Center Plate Border", 960, 488, 236, 184, Alpha(Gold, 0.31f), 13, 1);
            view.Round = Label(root, "Round", "東 一 局", 960, 442, 220, 48, 30, Ivory);
            view.Counters = Label(root, "Honba and Deposits", "0 本场  ·  1 供托", 960, 485, 220, 28, 13, Muted);
            Shape(root, "Center Divider", 960, 513, 162, 1, Alpha(Muted, 0.15f), 0);
            Label(root, "Tiles Remaining Label", "余牌", 902, 548, 48, 24, 11, Muted);
            view.Remaining = Label(root, "Tiles Remaining", "25", 961, 546, 66, 42, 31, Ivory);
            Label(root, "Tiles Remaining Unit", "枚", 1017, 548, 30, 24, 11, Muted);
            Shape(root, "Active Seat Bar", 960, 580, 56, 3, Gold, 2);
            Label(root, "Center West", "西", 960, 381, 32, 28, 14, Alpha(Muted, 0.56f));
            Label(root, "Center South", "南", 1094, 488, 32, 28, 14, Alpha(Muted, 0.56f));
            Label(root, "Center North", "北", 826, 488, 32, 28, 14, Alpha(Muted, 0.56f));

            view.Rivers[0] = Rect(root, "River · Self", 960, 682, 300, 194);
            view.Rivers[1] = Rotated(root, "River · Right", 1228, 488, 300, 194, 90);
            view.Rivers[2] = Rotated(root, "River · Across", 960, 294, 300, 194, 180);
            view.Rivers[3] = Rotated(root, "River · Left", 692, 488, 300, 194, 270);
            view.Hands[0] = Rect(root, "Hand · Self", 960, 930, 1120, 110);
            view.Hands[1] = Rotated(root, "Concealed · Right", 1478, 488, 560, 50, 90);
            view.Hands[2] = Rotated(root, "Concealed · Across", 960, 176, 560, 50, 180);
            view.Hands[3] = Rotated(root, "Concealed · Left", 442, 488, 560, 50, 270);
            view.Melds[0] = Rect(root, "Melds · Self", 1658, 934, 260, 48);
            view.Melds[1] = Rotated(root, "Melds · Right", 1478, 755, 320, 48, 90);
            view.Melds[2] = Rotated(root, "Melds · Across", 1336, 176, 320, 48, 180);
            view.Melds[3] = Rotated(root, "Melds · Left", 442, 755, 320, 48, 270);
            view.Players[0] = Player(root, "Player · Self", 245, 930, true);
            view.Players[1] = Player(root, "Player · Right", 1667, 488, false);
            view.Players[2] = Player(root, "Player · Across", 960, 112, false);
            view.Players[3] = Player(root, "Player · Left", 253, 488, false);
            Shape(root, "Timer Ring", 374, 928, 44, 44, Alpha(Gold, 0.56f), 22, 1.5f);
            view.Timer = Label(root, "Action Timer", "25", 374, 928, 44, 44, 16, Gold);

            Shape(root, "Turn Dot", 529, 835, 5, 5, Gold, 3);
            view.Status = Label(root, "Turn Status", "你的回合", 581, 835, 100, 28, 14, Gold, TextAnchor.MiddleLeft);
            view.Hint = Label(root, "Action Hint", "选中后再次点击出牌", 928, 835, 560, 28, 12, Muted, TextAnchor.MiddleLeft);
            RectTransform actionRow = Rect(root, "Action Buttons", 1456, 835, 616, 50);
            view.Actions = new Button[6]; view.ActionLabels = new Text[6];
            for (int index = 0; index < 6; index++)
            {
                view.Actions[index] = Button(actionRow, "Action " + index, "操作", 56 + index * 112, 25, 104, 46, index == 4, out Text label);
                view.ActionLabels[index] = label;
            }
            Label(root, "Footer Controls", "操作   /   单击选牌，再次点击出牌", 280, 1032, 440, 26, 11, Alpha(Muted, 0.65f), TextAnchor.MiddleLeft);
            Label(root, "Footer Preview Mode", "本地交互预览   ·   不计时", 960, 1032, 380, 26, 11, Alpha(Muted, 0.5f));
            Label(root, "Footer Edition", "L A Y O U T   S T U D Y   0 1", 1720, 1032, 300, 26, 9, Alpha(Muted, 0.45f), TextAnchor.MiddleRight);
            MakeModal(root, view);
            return view;
        }

        private static MahjongTableView.PlayerPanel Player(RectTransform parent, string name, float x, float y, bool self)
        {
            RectTransform root = Rect(parent, name, x, y, 220, 74);
            MahjongTableView.PlayerPanel panel = new();
            panel.TurnBorder = Shape(root, "Active Player Border", 110, 37, 220, 74, Alpha(Gold, 0.4f), 12, 1);
            Shape(root, "Wind Medallion", 32, 37, 46, 46, self ? Alpha(Gold, 0.13f) : Alpha(Ink, 0.22f), 23);
            Shape(root, "Wind Outline", 32, 37, 46, 46, self ? Alpha(Gold, 0.68f) : Alpha(Muted, 0.2f), 23, 1);
            panel.Wind = Label(root, "Wind", "東", 32, 36, 44, 44, 24, self ? Gold : Muted);
            panel.Name = Label(root, "Player Name", "玩家", 136, 18, 138, 24, 14, Ivory, TextAnchor.MiddleLeft);
            panel.Points = Label(root, "Player Points", "25,000", 137, 48, 140, 38, 28, self ? Gold : Ivory, TextAnchor.MiddleLeft);
            TableShape badge = Shape(root, "Riichi Badge", 191, 16, 42, 20, new Color(0.68f, 0.26f, 0.21f, 0.2f), 4);
            Label((RectTransform)badge.transform, "Riichi Label", "立直", 21, 10, 42, 20, 10, Hex("D6937D"));
            panel.Riichi = badge.gameObject; return panel;
        }

        private static void MakeModal(RectTransform root, MahjongTableView view)
        {
            TableShape overlay = Shape(root, "Result and Room Overlay", 960, 540, 1920, 1080, Alpha(Ink, 0.83f), 0); overlay.raycastTarget = true;
            RectTransform modal = (RectTransform)overlay.transform; view.Modal = overlay.gameObject;
            Shape(modal, "Modal Shadow", 960, 550, 668, 624, new Color(0, 0, 0, 0.3f), 22);
            Shape(modal, "Modal Panel", 960, 540, 660, 620, Hex("14372F"), 20);
            Shape(modal, "Modal Border", 960, 540, 660, 620, Alpha(Gold, 0.45f), 20, 1);
            Shape(modal, "Modal Accent", 960, 280, 56, 2, Gold, 1);
            view.ModalEyebrow = Label(modal, "Result Eyebrow", "ROUND RESULT / 本局结算", 960, 311, 560, 28, 11, Gold);
            view.ModalTitle = Label(modal, "Result Title", "自 摸", 960, 370, 590, 58, 36, Ivory);
            view.ModalScore = Label(modal, "Result Score", "+8,800 点", 960, 445, 590, 70, 49, Gold);
            view.ModalDetail = Label(modal, "Result Details", "4 番 · 20 符", 960, 569, 570, 184, 15, Muted);
            view.ModalDetail.lineSpacing = 1.35f;
            view.ModalHand = Rect(modal, "Revealed Winning Hand", 960, 690, 540, 58);
            view.ModalClose = Button(modal, "Close Modal", "返回牌桌", 841, 780, 162, 44, false, out _);
            view.ModalReset = Button(modal, "Reset from Modal", "再看一局", 1075, 780, 218, 44, true, out _);
            view.Modal.SetActive(false);
        }

        private static void Capture(Camera camera, MahjongTableView view, string path)
        {
            RenderTexture target = new(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
            camera.targetTexture = target;
            view.ApplyFonts();
            foreach (Text text in view.GetComponentsInChildren<Text>(true))
            {
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle); text.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
            for (int pass = 0; pass < 3; pass++)
            {
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            }
            RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
            Texture2D image = new(1920, 1080, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
            Debug.Log("TABLE_PREVIEW_IMAGE " + path);
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject item = new(name, typeof(RectTransform)); RectTransform rect = item.GetComponent<RectTransform>();
            if (parent != null) { rect.SetParent(parent, false); }
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = Vector2.one / 2;
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private static RectTransform Rotated(Transform parent, string name, float x, float y, float width, float height, float angle)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height); rect.localEulerAngles = new Vector3(0, 0, angle); return rect;
        }
        private static TableShape Shape(Transform parent, string name, float x, float y, float width, float height, Color color, float radius, float stroke = 0)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height); TableShape shape = rect.gameObject.AddComponent<TableShape>();
            shape.Radius = radius; shape.Stroke = stroke; shape.color = color; shape.raycastTarget = false; return shape;
        }
        private static Text Label(Transform parent, string name, string value, float x, float y, float width, float height, int size, Color color, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(parent, name, x, y, width, height); Text text = rect.gameObject.AddComponent<Text>();
            text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false; return text;
        }
        private static Button Button(Transform parent, string name, string value, float x, float y, float width, float height, bool primary, out Text label)
        {
            TableShape shape = Shape(parent, name, x, y, width, height, primary ? Gold : Alpha(Ivory, 0.15f), 7, primary ? 0 : 1);
            shape.raycastTarget = true; Button button = shape.gameObject.AddComponent<Button>(); button.targetGraphic = shape;
            ColorBlock colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.15f, 1.15f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.75f); colors.selectedColor = Color.white; colors.fadeDuration = 0.12f; button.colors = colors;
            label = Label(shape.transform, "Label", value, width / 2, height / 2, width, height, 14, primary ? Ink : Ivory);
            return button;
        }
        private static RawImage Raw(RectTransform parent, string name)
        {
            RectTransform rect = Rect(parent, name, 0, 0, 1, 1); Stretch(rect, Vector2.zero); return rect.gameObject.AddComponent<RawImage>();
        }
        private static void Stretch(RectTransform rect, Vector2 extra)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.anchoredPosition = Vector2.zero; rect.sizeDelta = extra;
        }
        private static Color Hex(string color) { ColorUtility.TryParseHtmlString("#" + color, out Color value); return value; }
        private static Color Alpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
