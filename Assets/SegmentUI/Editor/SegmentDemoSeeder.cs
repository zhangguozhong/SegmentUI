using System.IO;
using RTLLayout;
using SegmentUI.Integrations;
using SegmentUI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SegmentUI.Editor
{
    /// <summary>仅示例宿主管理安全区；头部/底部固定高度，中间区域吸收高度变化。</summary>
    // 生成默认标题/指示器、演示页面和场景；布局完成后预收集 LTR 基准。
    public static class SegmentDemoSeeder
    {
        public const string Folder = "Assets/SegmentUI/Samples/Generated";
        public const string Templates = "Assets/SegmentUI/Prefabs";
        public const string ScenePath = Folder + "/SegmentDemo.unity";
        public const float HeaderHeight = 208, FooterHeight = 224, Gap = 16, Inset = 24, BarHeight = 112;
        static readonly Color CanvasColor = new Color(.035f,.055f,.085f), Panel = new Color(.07f,.105f,.15f), Cyan = new Color(.3f,.86f,.94f);
        /// <summary>创建或更新示例资产，保留稳定路径与引用；仅由编辑器菜单或验证流程调用。</summary>
        [MenuItem("Tools/Segment UI/Generate Demo")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder); Directory.CreateDirectory(Templates); AssetDatabase.Refresh();
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SegmentDemo.prefab");
            if (existing != null && existing.GetComponent<SegmentDemo>()?.Schema == SegmentDemo.CurrentSchema && File.Exists(ScenePath)
                && File.Exists(Templates + "/SegmentView.prefab") && File.Exists(Folder + "/PageA.prefab") && File.Exists(Folder + "/PageB.prefab") && File.Exists(Folder + "/PageNative.prefab") && File.Exists(Folder + "/PageNested.prefab"))
            {
                if (!HasPageReferences(existing.GetComponent<SegmentDemo>())) ReimportDemoReferences();
                return;
            }
            var item = Save(Item("DefaultItem", false), Templates + "/DefaultItem.prefab").GetComponent<SegmentItemView>();
            var badge = Save(Item("BadgeItem", true), Templates + "/BadgeItem.prefab").GetComponent<SegmentItemView>();
            var line = Indicator("Underline", SegmentIndicatorStyle.Underline, SegmentIndicatorLayer.Foreground);
            var background = Indicator("RoundedBackground", SegmentIndicatorStyle.RoundedBackground, SegmentIndicatorLayer.Background);
            var outline = Indicator("Outline", SegmentIndicatorStyle.Outline, SegmentIndicatorLayer.Foreground);
            var root = Rect("SegmentView", null); root.sizeDelta = new Vector2(1032,1400);
            var view = root.gameObject.AddComponent<SegmentView>();
            var barRect = Rect("SegmentBar", root); Top(barRect, BarHeight); Image(barRect, Panel, true); barRect.gameObject.AddComponent<RectMask2D>();
            var titleScroll = barRect.gameObject.AddComponent<SegmentScrollRect>(); titleScroll.IsTitleBar = true; titleScroll.viewport = barRect;
            var items = Rect("Items", barRect); Fill(items);
            var back = Rect("BackgroundIndicators", barRect); Fill(back);
            var front = Rect("ForegroundIndicators", barRect); Fill(front);
            var bar = barRect.gameObject.AddComponent<SegmentBar>(); bar.TitleScroll = titleScroll; bar.Configure(items,item,back,front); bar.DefaultIndicators = new[] { line };
            var viewport = Rect("PagerViewport", root); Fill(viewport); viewport.offsetMax = new Vector2(0,-BarHeight-Gap);
            Image(viewport, Panel, true); viewport.gameObject.AddComponent<RectMask2D>(); viewport.gameObject.AddComponent<SegmentPagerInput>();
            var content = Rect("PageContent", viewport); Fill(content);
            var pager = root.gameObject.AddComponent<SegmentPager>(); pager.Configure(viewport,content); view.Configure(pager,bar);
            var defaultPrefab = Save(root.gameObject, Templates + "/SegmentView.prefab");
            var pageA = Page("PageA", "DISPATCH QUEUE");
            var pageB = Page("PageB", "ACTIVITY FEED");
            var pageNative = Page("PageNative", "NATIVE SCROLLRECT", true);
            var pageNested = NestedPage(defaultPrefab, pageNative);
            var canvas = new GameObject("SegmentDemo",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080,1920); scaler.matchWidthOrHeight = 0;
            var backdrop = Rect("Background",canvas.transform); Fill(backdrop); Image(backdrop,CanvasColor);
            var safe = Rect("SafeAreaContent",canvas.transform); Fill(safe); safe.gameObject.AddComponent<SampleSafeArea>();
            var header = Rect("Header",safe); Top(header,HeaderHeight); header.offsetMin = new Vector2(Inset,header.offsetMin.y); header.offsetMax = new Vector2(-Inset,0);
            var eyebrow = Text("Eyebrow",header,"SEGMENT UI   /   INTERACTION LAB",24); Top(eyebrow.rectTransform,48); eyebrow.color = Cyan;
            var title = Text("Title",header,"One gesture. One owner.",48); Top(title.rectTransform,72,48);
            var help = Text("Help",header,"LIVE updates Activity while dragging. Try LIVE + STRESS.\nSTYLE tests title width; DIR switches layout direction.",26); Top(help.rectTransform,80,120);
            var middle = Rect("Middle",safe); Fill(middle); middle.offsetMin = new Vector2(Inset,Inset + FooterHeight + Gap); middle.offsetMax = new Vector2(-Inset,-HeaderHeight-Gap);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(defaultPrefab); instance.transform.SetParent(middle,false); Fill((RectTransform)instance.transform);
            instance.AddComponent<RtlLayoutNode>().IgnoreSubtree = true;
            var footer = Rect("Footer",safe); footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1,0); footer.pivot = new Vector2(.5f,0); footer.sizeDelta = new Vector2(-Inset*2,FooterHeight); footer.anchoredPosition = new Vector2(0,Inset);
            var diagnostics = Text("Diagnostics",footer,"CURRENT --   TARGET --\nIdle / RESETS 0 / STRESS 0 / UPDATES 0",26); Top(diagnostics.rectTransform,96); diagnostics.color = Cyan;
            var row = Rect("Controls",footer); Top(row,112,112); var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = Gap; layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = layout.childForceExpandHeight = true;
            var demo = canvas.AddComponent<SegmentDemo>(); demo.View = instance.GetComponent<SegmentView>(); demo.PageA = pageA; demo.PageB = pageB; demo.PageNative = pageNative; demo.PageNested = pageNested; demo.BadgeItem = badge; demo.ItemIcon = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); demo.Underline = line; demo.Background = background; demo.Outline = outline; demo.Diagnostics = diagnostics;
            demo.Stress = Button(row,"Stress","STRESS"); demo.Cancel = Button(row,"Cancel","CANCEL"); demo.Reset = Button(row,"Reset","RESET LIST"); demo.Style = Button(row,"Style","STYLE: LINE"); demo.DirectionButton = Button(row,"Direction","DIR: LTR"); demo.LiveUpdateButton = Button(row,"Live","LIVE: OFF");
            var events = new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule)); events.transform.SetParent(canvas.transform,false);
            AddRtlAdapter(canvas).SourceView = demo.View;
            var demoPrefab = Save(canvas,Folder + "/SegmentDemo.prefab");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            PrefabUtility.InstantiatePrefab(demoPrefab,scene); EditorSceneManager.SaveScene(scene,ScenePath);
            if (!Application.isBatchMode) EditorSceneManager.CloseScene(scene,true);
            AssetDatabase.SaveAssets();
        }
        // 外部替换 Prefab 后，编辑器已加载的组件引用可能仍为空，
        // 即使磁盘 GUID/fileID 有效；应先重新导入依赖。
        /// <summary>重新导入页面与演示引用，用于修复编辑器缓存中的空引用，不保存当前场景。</summary>
        [MenuItem("Tools/Segment UI/Reimport Demo References")]
        public static void ReimportDemoReferences()
        {
            foreach (var name in new[] { "PageA", "PageB", "PageNative", "PageNested", "SegmentDemo" })
                AssetDatabase.ImportAsset(Folder + "/" + name + ".prefab", ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SegmentDemo.prefab");
            if (prefab == null || !HasPageReferences(prefab.GetComponent<SegmentDemo>()))
                throw new System.InvalidOperationException("Segment demo page references are missing on disk. Check PageA, PageB, PageNative, PageNested and NativePagePrefab in the generated prefabs.");
        }
        [MenuItem("Tools/Segment UI/Reimport Demo References", true)]
        static bool CanReimportDemoReferences() => !EditorApplication.isPlayingOrWillChangePlaymode;
        static bool HasPageReferences(SegmentDemo demo) => demo != null && demo.PageA != null
            && demo.PageB != null && demo.PageNative != null && demo.PageNested != null
            && demo.PageNested.NativePagePrefab != null;
        /// <summary>先确保示例资产存在，再打开生成的演示场景。</summary>
        [MenuItem("Tools/Segment UI/Open Demo")]
        public static void OpenDemo() { Generate(); EditorSceneManager.OpenScene(ScenePath); }
        static SegmentNestedDemoPage NestedPage(GameObject template, SegmentDemoPage nativePage)
        {
            var root = Rect("PageNested", null); root.sizeDelta = new Vector2(1032,1200);
            var page = root.gameObject.AddComponent<SegmentNestedDemoPage>();
            const float hintHeight = 112;
            var hint = Text("NestedHelp", root, "Swipe inside to change child tabs. Up/down scrolls the list.\nAt Left/Right edge, swipe outward to change parent page.", 26);
            Top(hint.rectTransform, hintHeight); hint.rectTransform.offsetMin = new Vector2(20, hint.rectTransform.offsetMin.y); hint.rectTransform.offsetMax = new Vector2(-20, 0);
            hint.color = Cyan; page.Help = hint;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
            instance.name = "ChildSegmentView"; instance.transform.SetParent(root, false);
            var rect = (RectTransform)instance.transform; Fill(rect); rect.offsetMax = new Vector2(0, -hintHeight-Gap);
            instance.AddComponent<RtlLayoutNode>().IgnoreSubtree = true;
            AddRtlAdapter(root.gameObject);
            page.ChildView = instance.GetComponent<SegmentView>(); page.NativePagePrefab = nativePage;
            return Save(root.gameObject, Folder + "/PageNested.prefab").GetComponent<SegmentNestedDemoPage>();
        }
        static SegmentDemoPage Page(string name,string heading,bool native = false)
        {
            var root = Rect(name,null); root.sizeDelta = new Vector2(1032,1200); root.gameObject.SetActive(false);
            var page = root.gameObject.AddComponent<SegmentDemoPage>();
            ScrollRect scroll = native ? root.gameObject.AddComponent<ScrollRect>() : root.gameObject.AddComponent<SegmentScrollRect>(); scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Elastic;
            var viewport = Rect("Viewport",root); Fill(viewport); Image(viewport,Panel,true); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content",viewport); content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(0,1); content.sizeDelta = Vector2.zero;
            scroll.viewport = viewport; scroll.content = content;
            page.Scroll = scroll;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20,20,20,20); layout.spacing = 16;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>(); fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained; fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            for (int i = 0; i < 24; i++)
            {
                var row = Rect("Row" + i.ToString("00"),content);
                Image(row,i % 2 == 0 ? new Color(.10f,.17f,.23f) : new Color(.08f,.14f,.20f),true);
                var element = row.gameObject.AddComponent<LayoutElement>(); element.minHeight = element.preferredHeight = 160; element.minWidth = 96;
                var label = Text("Label",row,heading + "  /  " + (i+1).ToString("00") + "\nVertical scroll  -  Swipe sideways to switch",30);
                Fill(label.rectTransform); label.rectTransform.offsetMin = new Vector2(24,12); label.rectTransform.offsetMax = new Vector2(-24,-12);
                if (native && i == 0)
                {
                    page.TestButton = row.gameObject.AddComponent<Button>(); page.TestButton.targetGraphic = row.GetComponent<Image>();
                    row.GetComponent<Image>().color = new Color(.12f,.27f,.34f);
                    page.TestLabel = label;
                    label.text = "TAP OR DRAG THIS BUTTON  /  TAPS 0\nUp/down: scroll   -   Sideways: switch page";
                }
            }
            AddRtlAdapter(root.gameObject);
            root.gameObject.SetActive(true);
            return Save(root.gameObject,Folder + "/" + name + ".prefab").GetComponent<SegmentDemoPage>();
        }
        static GameObject Item(string name,bool badge)
        {
            var root = Rect(name,null); root.sizeDelta = new Vector2(320,BarHeight);
            var button = root.gameObject.AddComponent<SegmentButton>(); button.targetGraphic = Image(root,new Color(0,0,0,0),true);
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.minWidth = layout.minHeight = 96;
            var item = root.gameObject.AddComponent<SegmentItemView>(); item.Button = button; item.NormalColor = new Color(.65f,.72f,.8f); item.SelectedColor = Cyan;
            item.Label = Text("Label",root,"Segment",32); Fill(item.Label.rectTransform); item.Label.alignment = TextAnchor.MiddleCenter;
            item.Label.rectTransform.offsetMin = new Vector2(64,8); item.Label.rectTransform.offsetMax = new Vector2(badge ? -72 : -24,-8);
            var iconRect = Rect("Icon",root); iconRect.anchorMin = iconRect.anchorMax = new Vector2(0,.5f); iconRect.pivot = new Vector2(0,.5f); iconRect.sizeDelta = new Vector2(28,28); iconRect.anchoredPosition = new Vector2(24,0); item.Icon = Image(iconRect,Cyan);
            if (badge) { item.Badge = Text("Badge",root,"8",22); var r = item.Badge.rectTransform; r.anchorMin = r.anchorMax = new Vector2(1,.5f); r.pivot = new Vector2(1,.5f); r.sizeDelta = new Vector2(48,48); r.anchoredPosition = new Vector2(-16,0); item.Badge.alignment = TextAnchor.MiddleCenter; item.Badge.color = Cyan; }
            AddRtlAdapter(root.gameObject);
            return root.gameObject;
        }
        static SegmentRtlLayoutBridge AddRtlAdapter(GameObject root)
        {
            root.AddComponent<RtlLayoutRoot>();
            return root.AddComponent<SegmentRtlLayoutBridge>();
        }
        static SegmentIndicator Indicator(string name,SegmentIndicatorStyle style,SegmentIndicatorLayer layer)
        {
            var root = Rect(name,null); var indicator = root.gameObject.AddComponent<SegmentBuiltInIndicator>(); indicator.Style = style; indicator.Layer = layer; indicator.Motion = SegmentIndicatorMotion.Stretch;
            indicator.WidthMode = SegmentIndicatorWidth.MatchItem; indicator.Height = 6; indicator.Radius = 20; indicator.Inset = 10; indicator.Position = style == SegmentIndicatorStyle.Underline ? SegmentIndicatorPosition.Bottom : SegmentIndicatorPosition.Center;
            indicator.Color = layer == SegmentIndicatorLayer.Background ? new Color(.12f,.27f,.34f) : Cyan;
            return Save(root.gameObject,Templates + "/" + name + ".prefab").GetComponent<SegmentIndicator>();
        }
        static GameObject Save(GameObject root,string path)
        {
            // 预制体预存 LTR 基准，运行时收集只补充动态缺失内容。
            foreach (var adapter in root.GetComponentsInChildren<RtlLayoutRoot>(true)) adapter.CaptureLayout();
            var asset = PrefabUtility.SaveAsPrefabAsset(root,path);
            Object.DestroyImmediate(root); return asset;
        }
        internal static RectTransform Rect(string name,Transform parent) { var r = (RectTransform)new GameObject(name,typeof(RectTransform)).transform; r.SetParent(parent,false); return r; }
        internal static void Fill(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
        static void Top(RectTransform r,float height,float inset=0) { r.anchorMin = new Vector2(0,1); r.anchorMax = Vector2.one; r.pivot = new Vector2(.5f,1); r.sizeDelta = new Vector2(0,height); r.anchoredPosition = new Vector2(0,-inset); }
        static Image Image(RectTransform r,Color color,bool hit=false) { var image = r.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = hit; return image; }
        static Text Text(string name,Transform parent,string value,int size) { var r = Rect(name,parent); var text = r.gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = size; text.resizeTextForBestFit = true; text.resizeTextMinSize = 20; text.resizeTextMaxSize = size; text.color = Color.white; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text; }
        static Button Button(Transform parent,string name,string value) { var root = Rect(name,parent); root.sizeDelta = new Vector2(96,112); var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = Image(root,new Color(.12f,.23f,.31f),true); var layout = root.gameObject.AddComponent<LayoutElement>(); layout.minWidth = layout.minHeight = 96; layout.flexibleWidth = 1; var text = Text("Label",root,value,25); Fill(text.rectTransform); text.alignment = TextAnchor.MiddleCenter; return button; }
    }
}
