using UnityEngine;
using UnityEngine.UI;

namespace SegmentUI
{
    public class SegmentItemView : MonoBehaviour
    {
        public Button Button;
        public Text Label;
        public Image Icon;
        public Text Badge;
        public RectTransform Content;
        public Color NormalColor = new Color(.38f, .42f, .48f);
        public Color SelectedColor = new Color(.15f, .38f, .85f);
        public Color DisabledColor = new Color(.65f, .65f, .65f);
        [Range(1, 1.3f)] public float SelectedScale = 1;
        public virtual RectTransform ContentRect => Content != null ? Content : Label != null ? Label.rectTransform : (RectTransform)transform;
        public string PageId { get; internal set; }
        public float SelectedWeight { get; private set; }
        public bool IsCommitted { get; private set; }
        public bool IsEnabled { get; private set; }
        [SerializeField, HideInInspector] bool rightToLeft;
        public bool RightToLeft => rightToLeft;
        /// <summary>Direction notification only; business layout belongs to an optional adapter.</summary>
        public event System.Action<bool> DirectionChanged;
        public virtual void ApplyDirection(bool value)
        {
            rightToLeft = value;
            // Also notify after same-direction Bind, so adapters collect newly added content.
            DirectionChanged?.Invoke(value);
        }
        public virtual void Bind(SegmentItemData data)
        {
            IsEnabled = data != null && data.IsEnabled;
            if (Button != null) Button.interactable = IsEnabled;
            if (Label != null) { Label.text = data?.Title ?? ""; Label.raycastTarget = false; if (Label.resizeTextForBestFit) Label.resizeTextMaxSize = Mathf.Min(Label.resizeTextMaxSize, Label.fontSize); }
            if (Icon != null) { Icon.sprite = data?.Icon; Icon.gameObject.SetActive(Icon.sprite != null); Icon.raycastTarget = false; }
            if (Badge != null) { Badge.text = data != null && data.BadgeCount > 0 ? data.BadgeCount.ToString() : ""; Badge.gameObject.SetActive(data != null && data.BadgeCount > 0); Badge.raycastTarget = false; }
            ApplyVisualState(0, false);
        }
        public virtual void ApplyVisualState(float selectedWeight, bool committed)
        {
            SelectedWeight = IsEnabled ? Mathf.Clamp01(selectedWeight) : 0;
            IsCommitted = committed && IsEnabled;
            if (Label != null) Label.color = IsEnabled ? Color.Lerp(NormalColor, SelectedColor, SelectedWeight) : DisabledColor;
            if (Icon != null) Icon.color = IsEnabled ? Color.Lerp(NormalColor, SelectedColor, SelectedWeight) : DisabledColor;
            // Scale only the visual child. The interaction rectangle and layout never change.
            if (Content != null && Content != transform) Content.localScale = Vector3.one * Mathf.Lerp(1, SelectedScale, SelectedWeight);
        }
        public virtual void Unbind() { PageId = null; IsEnabled = false; ApplyVisualState(0, false); if (Button != null) Button.interactable = false; }
    }
}
