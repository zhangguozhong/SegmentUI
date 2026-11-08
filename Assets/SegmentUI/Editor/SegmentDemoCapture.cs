using System.IO;
using SegmentUI.Samples;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI.Editor
{
    /// <summary>用于布局检查的离屏渲染；灰屏输出不能视为视觉验收通过。</summary>
    // 离屏生成演示截图并检查目标竖屏布局，用于人工视觉检查。
    public static class SegmentDemoCapture
    {
        /// <summary>批量实例化演示并渲染目标尺寸，结束后清理临时对象。</summary>
        public static void Capture()
        {
            SegmentDemoSeeder.Generate();
            var directory = Path.GetFullPath("Docs/SegmentUIVerification"); Directory.CreateDirectory(directory);
            foreach (var size in new[] { new Vector2Int(720,1280),new Vector2Int(1080,1920),new Vector2Int(1080,2400) })
                for (int preset = 0; preset < 5; preset++) CaptureOne(directory,size.x,size.y,preset);
        }
        static void CaptureOne(string directory,int width,int height,int preset)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SegmentDemoSeeder.Folder + "/SegmentDemo.prefab"));
            var cameraObject = new GameObject("SegmentCaptureCamera",typeof(Camera)); var camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true; camera.transform.position = new Vector3(0,0,-10); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f,.055f,.085f);
            var target = new RenderTexture(width,height,24); target.Create(); camera.targetTexture = target; var previous = RenderTexture.active;
            try
            {
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                root.GetComponent<CanvasScaler>().enabled = false; canvas.scaleFactor = width / 1080f;
                var safe = root.GetComponentInChildren<SampleSafeArea>(); safe.enabled = false;
                var rect = (RectTransform)safe.transform; rect.anchorMin = new Vector2(0,.035f); rect.anchorMax = new Vector2(1,.94f); rect.offsetMin = rect.offsetMax = Vector2.zero;
                Canvas.ForceUpdateCanvases();
                var demo = root.GetComponent<SegmentDemo>(); demo.Initialize();
                if (preset == 1) { demo.ToggleStyle(); demo.View.Select("activity",false); }
                if (preset == 2) demo.View.Select("native", false);
                if (preset == 3) demo.View.Select("nested", false);
                if (preset == 4) { demo.ToggleDirection(); demo.View.Select("nested", false); }
                foreach (var bridge in root.GetComponentsInChildren<SegmentUI.Integrations.SegmentRtlLayoutBridge>(true)) bridge.Synchronize();
                demo.UpdateDiagnostics();
                Canvas.ForceUpdateCanvases(); demo.View.Pager.RefreshLayout();
                foreach (var page in root.GetComponentsInChildren<SegmentDemoPage>(true)) LayoutRebuilder.ForceRebuildLayoutImmediate(page.Scroll.content);
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(width,height,TextureFormat.RGB24,false);
                try { image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply(); File.WriteAllBytes(Path.Combine(directory,"segment-" + width + "x" + height + (preset == 0 ? "-line" : preset == 1 ? "-pill" : preset == 2 ? "-native" : preset == 3 ? "-nested" : "-nested-rtl") + ".png"),image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            finally { RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); target.Release(); Object.DestroyImmediate(target); }
        }
    }
}
