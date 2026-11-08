using System;
using System.Collections.Generic;
using System.Linq;
using SegmentUI.Samples;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Editor
{
    /// <summary>编辑器资产契约检查：验证 Prefab 引用、布局与示例可用区域。</summary>
    public static class SegmentConfigurationValidator
    {
        [MenuItem("Tools/Segment UI/Validate")]
        public static void Validate()
        {
            var errors = ValidateAssets();
            if (errors.Count != 0) throw new InvalidOperationException("Segment UI configuration invalid:\n" + string.Join("\n", errors));
            Debug.Log("Segment UI configuration valid: default references, controlled scrolls, hit sizes and single sample safe area. Rendered and device validation remain separate.");
        }
        /// <summary>返回发现的问题列表供菜单或测试断言使用，不作为运行时修复入口。</summary>
        public static List<string> ValidateAssets()
        {
            var errors = new List<string>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentDemoSeeder.Templates + "/SegmentView.prefab");
            if (prefab == null) errors.Add("Generate SegmentView.prefab first.");
            else
            {
                var view = prefab.GetComponent<SegmentView>();
                if (view == null || view.Pager == null || view.Bar == null) errors.Add("SegmentView requires configured Pager and Bar.");
                else
                {
                    var pager = view.Pager; var bar = view.Bar;
                    if (pager.Viewport == null || pager.Content == null || pager.Content.parent != pager.Viewport) errors.Add("Pager content must be directly beneath its viewport.");
                    if (pager.Content != null && (pager.Content.GetComponent<HorizontalLayoutGroup>() == null || pager.Content.GetComponent<ContentSizeFitter>()?.horizontalFit != ContentSizeFitter.FitMode.PreferredSize)) errors.Add("PageContent requires horizontal layout and PreferredSize fitter.");
                    if (pager.Viewport != null && (pager.Viewport.GetComponent<RectMask2D>() == null || pager.Viewport.GetComponent<SegmentPagerInput>() == null || pager.Viewport.GetComponent<Image>()?.raycastTarget != true)) errors.Add("Pager viewport requires a raycast Image, RectMask2D and SegmentPagerInput.");
                    if (!(bar.TitleScroll is SegmentScrollRect title) || !title.IsTitleBar || title.viewport != bar.transform || title.content != bar.ItemsRoot) errors.Add("Title scroll must be a controlled title SegmentScrollRect with matching viewport/content.");
                    if (((RectTransform)bar.transform).rect.height < 96) errors.Add("Title bar height is below 96.");
                    if (bar.ItemsRoot == null || bar.BackgroundLayer == null || bar.ForegroundLayer == null || bar.DefaultTemplate == null) errors.Add("Bar item root, template and both indicator layers are required.");
                    if (bar.DefaultIndicators == null || bar.DefaultIndicators.Length == 0 || bar.DefaultIndicators.Any(i => i == null)) errors.Add("Default indicator templates must be configured.");
                }
                if (prefab.GetComponentsInChildren<SampleSafeArea>(true).Length != 0) errors.Add("Reusable view must inherit its host safe area.");
            }
            foreach (var name in new[] { "DefaultItem", "BadgeItem" })
            {
                var item = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentDemoSeeder.Templates + "/" + name + ".prefab")?.GetComponent<SegmentItemView>();
                if (item == null || item.Button == null || !(item.Button is SegmentButton)) errors.Add(name + " requires a controlled SegmentButton.");
                else CheckHit(item.Button,errors,true);
            }
            foreach (var name in new[] { "PageA", "PageB", "PageNative" })
            {
                var page = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentDemoSeeder.Folder + "/" + name + ".prefab");
                if (page == null) { errors.Add(name + " prefab missing."); continue; }
                var scroll = page.GetComponent<ScrollRect>();
                if (scroll == null || (scroll is SegmentScrollRect controlled && controlled.IsTitleBar) || !scroll.vertical || scroll.horizontal || scroll.viewport == null || scroll.content == null || scroll.content.parent != scroll.viewport) errors.Add(name + " requires a vertical scroll and owned viewport/content.");
                if (name == "PageNative")
                {
                    if (scroll == null || scroll.GetType() != typeof(ScrollRect) || page.GetComponent<SegmentScrollRectAdapter>() != null) errors.Add("Native page must demonstrate an unadapted system ScrollRect asset.");
                    var button = page.GetComponentInChildren<Button>(true);
                    if (button == null || button.GetType() != typeof(Button)) errors.Add("Native page requires an ordinary test Button.");
                    else CheckHit(button,errors,false);
                }
                if (page.GetComponentsInChildren<SampleSafeArea>(true).Length != 0) errors.Add(name + " must inherit host safe area.");
            }
            var nested = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentDemoSeeder.Folder + "/PageNested.prefab")?.GetComponent<SegmentNestedDemoPage>();
            if (nested == null || nested.ChildView == null || nested.NativePagePrefab == null) errors.Add("Nested page needs child SegmentView and native page source.");
            else
            {
                if (nested.GetComponentsInChildren<SampleSafeArea>(true).Length != 0) errors.Add("Nested page must inherit the host safe area.");
                if (nested.NativePagePrefab.Scroll == null || nested.NativePagePrefab.Scroll.GetType() != typeof(ScrollRect)) errors.Add("Nested child source must use system ScrollRect.");
            }
            var demo = AssetDatabase.LoadAssetAtPath<GameObject>(SegmentDemoSeeder.Folder + "/SegmentDemo.prefab");
            if (demo == null) errors.Add("Demo prefab missing.");
            else
            {
                var safe = demo.GetComponentsInChildren<SampleSafeArea>(true);
                if (safe.Length != 1 || safe[0].transform.parent != demo.transform) errors.Add("Demo requires exactly one direct child safe area.");
                else foreach (var name in new[] { "Header", "Middle", "Footer" }) if (safe[0].transform.Find(name) == null) errors.Add("Safe area is missing " + name + ".");
                foreach (var button in demo.GetComponentsInChildren<Button>(true)) CheckHit(button,errors,false);
                ValidateDemoGeometry(demo,errors);
                var sample = demo.GetComponent<SegmentDemo>();
                if (sample == null || sample.View == null || sample.PageA == null || sample.PageB == null || sample.PageNative == null || sample.PageNested == null || sample.DirectionButton == null || sample.Diagnostics == null) errors.Add("Demo page sources/view/diagnostics references are required.");
            }
            return errors;
        }
        // 受 LayoutGroup 驱动的资产 RectTransform 尚未解析出可靠几何。
        // 使用明确设计尺寸的隔离实例测量，不修改原 Prefab 资产。
        static void ValidateDemoGeometry(GameObject prefab,List<string> errors)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var size in new[] { new Vector2Int(720,1280),new Vector2Int(1080,1920),new Vector2Int(1080,2400) })
                {
                    var root = UnityEngine.Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(root,scene);
                    try
                    {
                        root.GetComponent<CanvasScaler>().enabled = false;
                        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.scaleFactor = 1;
                        var rootRect = (RectTransform)root.transform;
                        rootRect.sizeDelta = new Vector2(1080,size.y * 1080f / size.x);
                        var safe = root.GetComponentInChildren<SampleSafeArea>(true);
                        if (safe == null) continue;
                        safe.enabled = false;
                        var safeRect = (RectTransform)safe.transform;
                        safeRect.anchorMin = new Vector2(0,.035f); safeRect.anchorMax = new Vector2(1,.94f); safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;
                        Canvas.ForceUpdateCanvases();
                        LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                        var sample = root.GetComponent<SegmentDemo>();
                        sample.Initialize(); sample.View.Select("nested", false);
                        foreach (bool rtl in new[] { false, true })
                        {
                            sample.View.SetDirection(rtl ? SegmentLayoutDirection.RightToLeft : SegmentLayoutDirection.LeftToRight);
                            Canvas.ForceUpdateCanvases();
                            foreach (var pager in root.GetComponentsInChildren<SegmentPager>(true))
                            {
                                pager.RefreshLayout();
                                if (!pager.HasLayout) errors.Add(size + ": nested demo pager must have positive viewport dimensions.");
                            }
                            foreach (var layout in root.GetComponentsInChildren<LayoutGroup>(true)) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
                            Canvas.ForceUpdateCanvases();
                            foreach (var button in root.GetComponentsInChildren<Button>(true))
                            {
                                CheckHit(button,errors,true);
                                var hit = (RectTransform)button.transform;
                                CheckContained(hit,(RectTransform)hit.parent,size,errors);
                                CheckContained(hit,safeRect,size,errors);
                            }
                        }
                        var middle = safe.transform.Find("Middle") as RectTransform;
                        if (middle == null || middle.rect.width <= 0 || middle.rect.height <= 0) errors.Add(size + ": demo Middle must have positive remaining space.");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void CheckContained(RectTransform child,RectTransform region,Vector2Int size,List<string> errors)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                Vector2 local = region.InverseTransformPoint(corner);
                var bounds = region.rect;
                if (local.x < bounds.xMin - .1f || local.x > bounds.xMax + .1f || local.y < bounds.yMin - .1f || local.y > bounds.yMax + .1f)
                { errors.Add(size + ": " + child.name + " exceeds " + region.name + "."); break; }
            }
        }
        static void CheckHit(Button button,List<string> errors,bool measure)
        {
            var layout = button.GetComponent<LayoutElement>(); var rect = button.transform as RectTransform;
            if (layout == null || layout.minWidth < 96 || layout.minHeight < 96 || rect == null || (measure && (rect.rect.width < 95.99f || rect.rect.height < 95.99f))) errors.Add(button.name + " has a hit rectangle or minimum layout size below 96.");
        }
    }
}
