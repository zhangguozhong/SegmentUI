using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    /// <summary>标题预制体的业务扩展点：绑定显示数据并接收分页插值；外部几何由标题栏管理。</summary>
    public class SegmentItemView : MonoBehaviour
    {
        /// <summary>标题点击入口，交互矩形由模板提供并由标题栏校验。</summary>
        public Button Button;
        /// <summary>默认标题文字引用，自定义 Bind 可扩展其他文本方案。</summary>
        public Text Label;
        /// <summary>默认图标引用，Bind 根据 Sprite 是否为空切换显隐。</summary>
        public Image Icon;
        /// <summary>默认数字角标引用，非正数隐藏。</summary>
        public Text Badge;
        /// <summary>视觉内容子节点，可用于选中缩放与指示器内容区域。</summary>
        public RectTransform Content;
        public Color NormalColor = new Color(.38f, .42f, .48f);
        public Color SelectedColor = new Color(.15f, .38f, .85f);
        public Color DisabledColor = new Color(.65f, .65f, .65f);
        [Range(1, 1.3f)] public float SelectedScale = 1;
        /// <summary>指示器匹配内容宽度时使用的区域；优先 Content，其次 Label，最后标题根。</summary>
        public virtual RectTransform ContentRect => Content != null ? Content : Label != null ? Label.rectTransform : (RectTransform)transform;
        /// <summary>所属页面 ID，由标题栏在绑定时设置。</summary>
        public string PageId { get; internal set; }
        /// <summary>当前插值选中权重，不等同于页面已经提交。</summary>
        public float SelectedWeight { get; private set; }
        /// <summary>是否对应已提交页，用于与拖拽中的渐变视觉区分。</summary>
        public bool IsCommitted { get; private set; }
        /// <summary>本次绑定的可用状态，与标题栏整体 Interactable 共同决定点击资格。</summary>
        public bool IsEnabled { get; private set; }
        [SerializeField, HideInInspector] bool rightToLeft;
        public bool RightToLeft => rightToLeft;
        /// <summary>仅通知方向，业务内部布局由可选适配器负责。</summary>
        public event System.Action<bool> DirectionChanged;
        public virtual void ApplyDirection(bool value)
        {
            rightToLeft = value;
            // 同方向重新 Bind 后也通知，让适配器补收集新增内容。
            DirectionChanged?.Invoke(value);
        }
        /// <summary>刷新标题、图标、角标和启用状态；覆盖时调用基类，并注意此方法会重置选中视觉。</summary>
        public virtual void Bind(SegmentItemData data)
        {
            IsEnabled = data != null && data.IsEnabled;
            if (Button != null) Button.interactable = IsEnabled;
            if (Label != null) { Label.text = data?.Title ?? ""; Label.raycastTarget = false; if (Label.resizeTextForBestFit) Label.resizeTextMaxSize = Mathf.Min(Label.resizeTextMaxSize, Label.fontSize); }
            if (Icon != null) { Icon.sprite = data?.Icon; Icon.gameObject.SetActive(Icon.sprite != null); Icon.raycastTarget = false; }
            if (Badge != null) { Badge.text = data != null && data.BadgeCount > 0 ? data.BadgeCount.ToString() : ""; Badge.gameObject.SetActive(data != null && data.BadgeCount > 0); Badge.raycastTarget = false; }
            ApplyVisualState(0, false);
        }
        /// <summary>根据分页权重更新视觉，只缩放内容子节点，不改变按钮热区和布局矩形。</summary>
        public virtual void ApplyVisualState(float selectedWeight, bool committed)
        {
            SelectedWeight = IsEnabled ? Mathf.Clamp01(selectedWeight) : 0;
            IsCommitted = committed && IsEnabled;
            if (Label != null) Label.color = IsEnabled ? Color.Lerp(NormalColor, SelectedColor, SelectedWeight) : DisabledColor;
            if (Icon != null) Icon.color = IsEnabled ? Color.Lerp(NormalColor, SelectedColor, SelectedWeight) : DisabledColor;
            // 仅缩放视觉子节点，保持交互热区和外部布局不变。
            if (Content != null && Content != transform) Content.localScale = Vector3.one * Mathf.Lerp(1, SelectedScale, SelectedWeight);
        }
        /// <summary>解除标题数据与交互状态；复用的标题实例仍可重新 Bind。</summary>
        public virtual void Unbind() { PageId = null; IsEnabled = false; ApplyVisualState(0, false); if (Button != null) Button.interactable = false; }
    }
}
