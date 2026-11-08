using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Tests.Editor
{
    public class SegmentPrefabContractTests
    {
        const string Folder = "Assets/SegmentUI/Samples/Generated";
        [Test] public void EditorCaptureCanExplicitlySynchronizeBridgeWithoutPlayModeCallbacks()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SegmentDemo.prefab"));
            try
            {
                var sample = root.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "SegmentDemo");
                sample.GetType().GetMethod("Initialize").Invoke(sample, null);
                sample.GetType().GetMethod("ToggleDirection").Invoke(sample, null);
                foreach (var bridge in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().Name == "SegmentRtlLayoutBridge"))
                {
                    var synchronize = bridge.GetType().GetMethod("Synchronize");
                    Assert.That(synchronize, Is.Not.Null);
                    synchronize.Invoke(bridge, null);
                }
                Assert.That(root.transform.Find("SafeAreaContent/Header/Title").GetComponent<Text>().alignment, Is.EqualTo(TextAnchor.MiddleRight));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void BusinessPrefabsOptIntoIndependentRtlLibraryAndContainersStopTraversal()
        {
            foreach (var path in new[] { Folder + "/PageA.prefab", Folder + "/PageB.prefab", Folder + "/PageNative.prefab", Folder + "/PageNested.prefab", Folder + "/SegmentDemo.prefab", "Assets/SegmentUI/Prefabs/DefaultItem.prefab", "Assets/SegmentUI/Prefabs/BadgeItem.prefab" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var names = asset.GetComponents<MonoBehaviour>().Where(c => c != null).Select(c => c.GetType().Name).ToArray();
                Assert.That(names, Does.Contain("RtlLayoutRoot"), path);
                var rtlRoot = asset.GetComponents<MonoBehaviour>().First(c => c != null && c.GetType().Name == "RtlLayoutRoot");
                Assert.That((int)rtlRoot.GetType().GetProperty("BakedNodeCount").GetValue(rtlRoot), Is.GreaterThan(0), "Prefab must store editor-collected LTR baselines: " + path);
                Assert.That(names, Does.Contain("SegmentRtlLayoutBridge"), path);
                foreach (var view in asset.GetComponentsInChildren<MonoBehaviour>(true).Where(c => c != null && c.GetType().Name == "SegmentView"))
                {
                    var boundary = view.GetComponents<MonoBehaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "RtlLayoutNode");
                    Assert.That(boundary, Is.Not.Null, path);
                    Assert.That(new SerializedObject(boundary).FindProperty("IgnoreSubtree").boolValue, Is.True);
                }
            }
            Assert.That(File.ReadAllText("Assets/RTLLayout/Runtime/RTLLayout.asmdef"), Does.Not.Contain("SegmentUI"));
            Assert.That(File.ReadAllText("Assets/SegmentUI/Runtime/SegmentUI.asmdef"), Does.Not.Contain("RTLLayout"));
        }
        [Test] public void ReimportDemoReferencesPreservesFilesAndResolvesEveryPageSource()
        {
            var paths = new[] { "PageA", "PageB", "PageNative", "PageNested", "SegmentDemo" }
                .Select(n => Folder + "/" + n + ".prefab").ToArray();
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SegmentUI.Editor.SegmentDemoSeeder")).First(t => t != null);
            var method = type.GetMethod("ReimportDemoReferences");
            Assert.That(method, Is.Not.Null);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                method.Invoke(null, null);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SegmentDemo.prefab");
                var component = root.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "SegmentDemo");
                var serialized = new SerializedObject(component);
                foreach (var field in new[] { "PageA", "PageB", "PageNative", "PageNested" })
                {
                    var source = serialized.FindProperty(field).objectReferenceValue;
                    Assert.That(source, Is.Not.Null, field);
                    Assert.That(EditorUtility.IsPersistent(source), Is.True, field);
                }
                var nested = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PageNested.prefab");
                var nestedComponent = nested.GetComponents<MonoBehaviour>().First(c => c.GetType().Name == "SegmentNestedDemoPage");
                Assert.That(new SerializedObject(nestedComponent).FindProperty("NativePagePrefab").objectReferenceValue, Is.Not.Null);
                for (int i = 0; i < paths.Length; i++) Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
            }
        }
        [Test] public void DefaultPrefabOwnsClippedPagerAndTitleRegionsWithoutSafeArea()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SegmentUI/Prefabs/SegmentView.prefab");
            Assert.That(prefab, Is.Not.Null, "Generate the default SegmentView assets.");
            Assert.That(prefab.transform.Find("SafeAreaContent"), Is.Null);
            var bar = prefab.transform.Find("SegmentBar");
            var viewport = prefab.transform.Find("PagerViewport");
            Assert.That(bar, Is.Not.Null); Assert.That(viewport, Is.Not.Null);
            Assert.That(viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(viewport.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(viewport.Find("PageContent"), Is.Not.Null);
            Assert.That(viewport.Find("PageContent").GetComponent<HorizontalLayoutGroup>(),Is.Not.Null);
            var fitter=viewport.Find("PageContent").GetComponent<ContentSizeFitter>();Assert.That(fitter,Is.Not.Null);
            Assert.That(fitter.horizontalFit,Is.EqualTo(ContentSizeFitter.FitMode.PreferredSize));
            Assert.That(fitter.verticalFit,Is.EqualTo(ContentSizeFitter.FitMode.Unconstrained));
            Assert.That(((RectTransform)bar).rect.height, Is.GreaterThanOrEqualTo(96));
            var controlledTitle = bar.GetComponent<ScrollRect>();
            Assert.That(controlledTitle, Is.Not.Null);
            Assert.That(controlledTitle.GetType().Name, Is.EqualTo("SegmentScrollRect"));
            Assert.That(controlledTitle.viewport, Is.SameAs(bar));
            Assert.That(controlledTitle.content.parent, Is.SameAs(bar));
            Assert.That(viewport.GetComponents<MonoBehaviour>().Any(c => c != null && c.GetType().Name == "SegmentPagerInput"), Is.True);
            var serialized = new SerializedObject(prefab.GetComponents<MonoBehaviour>().First(c => c != null && c.GetType().Name == "SegmentView"));
            Assert.That(serialized.FindProperty("pager").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("bar").objectReferenceValue, Is.Not.Null);
        }
        [Test] public void DemoHasSingleSafeAreaAndControlsRespectMinimumHitSize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/SegmentDemo.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Count(t => t.name == "SafeAreaContent"), Is.EqualTo(1));
            var safe = prefab.transform.Find("SafeAreaContent");
            foreach (var zone in new[] { "Header", "Middle", "Footer" }) Assert.That(safe.Find(zone), Is.Not.Null);
            var live = safe.Find("Footer/Controls/Live");
            Assert.That(live, Is.Not.Null);
            Assert.That(live.GetComponentInChildren<Text>().text, Is.EqualTo("LIVE: OFF"));
            var demo = new SerializedObject(prefab.GetComponents<MonoBehaviour>().Single(component => component.GetType().Name == "SegmentDemo"));
            Assert.That(demo.FindProperty("LiveUpdateButton").objectReferenceValue, Is.EqualTo(live.GetComponent<Button>()));
            var nested = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PageNested.prefab");
            var nestedData = new SerializedObject(nested.GetComponents<MonoBehaviour>().Single(component => component.GetType().Name == "SegmentNestedDemoPage"));
            Assert.That(nestedData.FindProperty("BadgeItem").objectReferenceValue, Is.Not.Null);
            foreach (var button in prefab.GetComponentsInChildren<Button>(true))
            {
                var layout = button.GetComponent<LayoutElement>();
                Assert.That(layout, Is.Not.Null, button.name);
                Assert.That(layout.minWidth, Is.GreaterThanOrEqualTo(96), button.name);
                Assert.That(layout.minHeight, Is.GreaterThanOrEqualTo(96), button.name);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PageA.prefab"), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/PageB.prefab"), Is.Not.Null);
            foreach (var name in new[] { "PageA", "PageB" })
            {
                var page = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + name + ".prefab");
                var scroll = page.GetComponent<ScrollRect>();
                Assert.That(scroll.GetType().Name, Is.EqualTo("SegmentScrollRect"));
                Assert.That(scroll.vertical, Is.True); Assert.That(scroll.horizontal, Is.False);
                Assert.That(scroll.content.parent, Is.SameAs(scroll.viewport));
            }
        }
        [Test] public void NativeDemoPageUsesOnlySystemScrollRectAndOrdinaryButton()
        {
            var page=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PageNative.prefab");
            Assert.That(page,Is.Not.Null);
            var scroll=page.GetComponent<ScrollRect>();Assert.That(scroll.GetType(),Is.EqualTo(typeof(ScrollRect)));
            Assert.That(scroll.vertical,Is.True);Assert.That(scroll.horizontal,Is.False);
            Assert.That(page.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c!=null && c.GetType().Name=="SegmentScrollRectAdapter"),Is.False,"Adapter must be installed at runtime, not baked into the example.");
            var button=page.GetComponentInChildren<Button>();Assert.That(button.GetType(),Is.EqualTo(typeof(Button)));
            Assert.That(button.GetComponent<LayoutElement>().minHeight,Is.GreaterThanOrEqualTo(96));
        }
        [Test] public void NestedDemoEmbedsChildViewAndReferencesNativePageWithoutAnotherSafeArea()
        {
            var page=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PageNested.prefab");
            Assert.That(page,Is.Not.Null);
            Assert.That(page.GetComponentsInChildren<MonoBehaviour>(true).Count(c=>c!=null && c.GetType().Name=="SegmentView"),Is.EqualTo(1));
            Assert.That(page.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c!=null && c.GetType().Name=="SampleSafeArea"),Is.False);
            Assert.That(AssetDatabase.GetDependencies(Folder+"/PageNested.prefab",true),Does.Contain(Folder+"/PageNative.prefab"));
        }
        [Test] public void GeneratedDemoAndDefaultPrefabHaveNoExternalListFrameworkDependencies()
        {
            foreach (var path in new[] { "Assets/SegmentUI/Prefabs/SegmentView.prefab", Folder + "/SegmentDemo.prefab" })
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null);
                foreach (var dependency in AssetDatabase.GetDependencies(path,true))
                    Assert.That(dependency, Does.Not.Contain("Assets/CollectionUI"), path + " must be independently reusable.");
            }
        }
        [Test] public void PublicValidatorAcceptsGeneratedContracts()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SegmentUI.Editor.SegmentConfigurationValidator")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Validator menu must be available.");
            var path = Folder + "/SegmentDemo.prefab";
            var before = File.ReadAllBytes(path);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var errors = (System.Collections.ICollection)type.GetMethod("ValidateAssets").Invoke(null, null);
                Assert.That(errors.Count, Is.Zero, string.Join("; ", errors.Cast<object>()));
            }
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path),"Validation must not mutate the source prefab.");
        }
        [Test] public void SeederSecondRunLeavesAssetBytesUnchanged()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("SegmentUI.Editor.SegmentDemoSeeder")).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Seeder must be available.");
            var generate = type.GetMethod("Generate");
            generate.Invoke(null, null);
            var files = Directory.GetFiles("Assets/SegmentUI", "*", SearchOption.AllDirectories)
                .Where(p => p.EndsWith(".prefab") || p.EndsWith(".unity")).OrderBy(p => p).ToArray();
            Assert.That(files.Length, Is.GreaterThanOrEqualTo(8));
            var before = files.Select(File.ReadAllBytes).ToArray();
            generate.Invoke(null, null);
            for (int i = 0; i < files.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(files[i]), files[i]);
        }
    }
}
