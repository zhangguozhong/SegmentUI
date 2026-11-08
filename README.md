# SegmentUI

## 安装

使用 Unity 2022.3 和 UGUI 1.0。

1. 将本仓库 `Assets/SegmentUI` 及 `.meta` 文件复制到工程的 `Assets` 目录。
2. 使用默认 Prefab、示例或 Editor 工具时，同时从 [RTLLayout](https://github.com/zhangguozhong/RTLLayout) 导入 `Assets/RTLLayout` 及 `.meta` 文件。
3. 如果保留 Tests 目录，安装 Unity Test Framework；不需要测试时可移除两个组件的 Tests 目录。

请使用目录复制方式安装。仅需分页核心时，可只导入 `Runtime` 并自行制作容器和标题 Prefab。

## 打开示例

选择 **Tools → Segment UI → Open Demo**，将 Game View 设为竖屏（如 720×1280），进入 Play。

- 点击标题或横向拖动切换页面，纵向拖动滚动当前页。
- **Native** 页使用原生 ScrollRect；首行按钮显示点击次数。
- **Nested** 页包含子分页，子页内部同样使用原生 ScrollRect。横拖优先切子页，到达边缘后，下一次向外拖动交给父分页。
- **DIR** 切换 LTR/RTL，**STYLE** 切换指示器，**STRESS** 连续切页，**CANCEL** 取消过渡，**RESET LIST** 重置列表位置。
- 需要重新生成示例时，选择 **Tools → Segment UI → Generate Demo**。

## 配置页面与标题

把 `Prefabs/SegmentView.prefab` 放到宿主 Canvas 的可用区域内，对其 RectTransform 设置拉伸。页面根节点使用 `SegmentPage` 或其派生类。

```csharp
using SegmentUI;
using UnityEngine;

public sealed class MyScreen : MonoBehaviour
{
    [SerializeField] SegmentView segments;
    [SerializeField] SegmentPage allPagePrefab, favoritesPagePrefab;
    [SerializeField] SegmentItemView badgeItemPrefab;

    void Start()
    {
        segments.SetPages(new[]
        {
            new SegmentPageDescriptor
            {
                Id = "all",
                Item = new SegmentItemData { Title = "全部" },
                Source = PageSource.FromPrefab(allPagePrefab)
            },
            new SegmentPageDescriptor
            {
                Id = "favorites",
                Item = new SegmentItemData { Title = "收藏", BadgeCount = 3 },
                ItemTemplate = badgeItemPrefab,
                Source = PageSource.FromPrefab(favoritesPagePrefab)
            }
        }, initialPageId: "all");
    }
}
```

也可在 SegmentView Inspector 的 Initial Pages 中配置 Prefab/ExistingInstance 来源。Id 必须稳定、非空、唯一；标题和页面通过同一描述项关联，不依赖两个数组的索引。

`SetPages` 会复制结构数据。纯标题/角标变化不重绑页面；业务依赖变化时增加 `PageRevision`，然后重新提交描述。修改 Prefab、Factory 实例或来源版本会释放旧页面实例。

`Item.IsEnabled=false` 保留禁用标题，但该页不进入可滑动序列。选择禁用页返回 Unavailable；选择未知 Id 返回 Invalid。

### 页面来源与所有权

- `PageSource.FromPrefab(asset)`：必须是 Prefab 资产，组件实例化并释放。
- `PageSource.FromFactory(factory, version)`：工厂实现 `ISegmentPageFactory.Create(parent)` 和 `Release(page)`。Create 必须将页面置于给定槽位；释放前组件会将页面从自己的槽位脱离，再交还工厂，允许对象池保留实例。
- `PageSource.FromExisting(instance)`：场景实例预放在 `Pager.Content/PageSlot/Page`；组件管理展示与绑定，不销毁宿主实例。未选中已有页会先隐藏，但仍懒绑定。

`BindPage` 可在正式进入前注入业务依赖：

```csharp
descriptor.BindPage = page => ((MyPage)page).SetRepository(repository);
descriptor.PageRevision++;
```

页面默认懒创建，已创建实例缓存到宿主释放，保留纵向位置；适合少量分类页，不提供自动 LRU 淘汰。

## 运行时更新标题、图标和角标

```csharp
bool accepted = segments.UpdateItem("activity", new SegmentItemData
{
    Title = "最新动态",
    Icon = activityIcon, // Sprite；传 null 清除图标
    BadgeCount = 5      // 小于等于 0 时隐藏角标
});
```

`UpdateItem` 替换该项的 Title、Icon、BadgeCount，并保留原来的 IsEnabled。调用后无需再调用 SetPages；它不会重建标题项或 Page，也不会重新绑定业务页面、打断当前拖拽或切页动画。标题宽度和指示器会按新内容刷新，标题栏不会因此自动居中或停止惯性。

传入数据会被复制，之后修改该对象不会自动刷新。Id 不存在时返回 false；itemData 为 null 时抛出 ArgumentNullException。回调内调用会排队，返回 true 表示已接受；如果前面的排队操作已删除该页，更新会跳过。

新增、删除、排序、启用/禁用页面、替换模板或页面来源仍使用 SetPages。如果业务还保留页面描述列表，请同步修改列表中的 Item，避免后续 SetPages 用旧数据覆盖显示。

## 选择页面与切换方向

```csharp
segments.Select("favorites");
segments.SetDirection(SegmentLayoutDirection.RightToLeft);
segments.SetDirection(SegmentLayoutDirection.LeftToRight);
childSegments.SetDirection(SegmentLayoutDirection.Inherit);
```

PageId 和数据顺序保持不变。RTL 下逻辑第一项位于右侧，向右拖动进入下一项。嵌套 SegmentView 默认继承父级方向，也可单独指定。

页面内部需要镜像时，在 Page/Item 根添加 `RtlLayoutRoot` 与 `SegmentRtlLayoutBridge`，按 [RTLLayout 使用说明](https://github.com/zhangguozhong/RTLLayout) 收集 LTR 布局。嵌入的 SegmentView 容器可用 `RtlLayoutNode.IgnoreSubtree` 排除外层收集，由其自身及内部 Page/Item 桥接处理方向。

阿拉伯文字形连接、双向文本排版和 TMP 适配需由业务文本组件处理，可通过 `DirectionChanged` 接入。

## 接入纵向滚动

新列表可使用 `SegmentScrollRect`，设置 `horizontal=false`、`vertical=true`，绑定 viewport 和 content。

已有页面可保留原生 ScrollRect。页面绑定完成后会自动为纵向列表安装 `SegmentScrollRectAdapter`。也可手动将适配器挂在 ScrollRect 所在节点。

运行时新增列表或按钮后调用：

```csharp
SegmentScrollRectAdapter.Install(newContentRoot); // Transform
```

同次拖动锁定方向，纵拖途中转向不会切页。横向或双向 ScrollRect 不自动适配。

自定义刷新行为可订阅 `OwnedBegin`、`OwnedDrag`、`OwnedEnd`、`OwnedCancel`；取消时不要提交刷新。

## 页面生命周期

派生 SegmentPage 覆盖以下 protected virtual 方法：

| 方法 | 时机 |
| --- | --- |
| OnInitialize | 实例初始化一次 |
| OnBind | 每个业务绑定版本 |
| OnEnter | 正式成为有效展示页 |
| OnExit(reason) | 结束对应展示周期 |
| OnUnbind | 解除当前绑定 |
| OnRelease | 结束组件管理会话，释放自持有资源 |

滑到一半又返回不会触发目标页 Enter。A→B 尚未完成又请求 C，只有最终提交页进入。Unity OnEnable/OnDisable 不等价于业务 Enter/Exit。

页面可使用 `PageId`、`BindingVersion`、`PresentationVersion`，以及 `BindingCancellation`、`PresentationCancellation` 保护异步结果。`IsCurrent(id, bindingVersion)` 和 `IsCurrentPresentation(version)` 用于检查请求是否仍有效。所有 UI 更新必须回到主线程。

宿主通过 CanvasGroup 隐藏或自己的导航系统离开时调用 `SetPresented(false)`，返回时调用 `SetPresented(true)`。父页面退出会向嵌套分页传播，保留子页选择。失焦只取消交互，不默认把业务页面当作退出。

数据/选择回调内可以发起后续操作；组件在当前提交完成后执行，避免重入修改一半的状态。

## 自定义标题与指示器

`SegmentBar.DefaultTemplate` 是默认 Item Prefab；描述中的 ItemTemplate 可单项覆盖。模板需挂 SegmentItemView，引用覆盖至少 96×96 UI 单位的 Button；自定义内容不要改变 Item 的外部几何所有权。

自定义 Item 可覆盖 Bind、ApplyVisualState、Unbind；应调用基类以保留可用和选中状态。ContentRect 为“匹配标题内容”的指示器提供矩形。

`SegmentBar.DefaultIndicators` 在第一次成功 Bind 时安装。也可整体替换：

```csharp
segments.SetIndicator(underlinePrefab);
segments.SetIndicators(new[] { backgroundPrefab, outlinePrefab });
segments.SetIndicators(System.Array.Empty<SegmentIndicator>()); // 明确关闭
```

`SegmentBuiltInIndicator` 支持 None/Underline/RoundedBackground/Outline/Image；固定宽度/匹配 Item/匹配内容；Top/Bottom/Center；Discrete/Slide/Stretch。

自定义指示器继承 SegmentIndicator，实现 ApplyFrame；收到真实左右 Item/内容矩形、百分比、Current/Target、布局与过渡版本。矩形已经转换到自己的父层局部坐标。不要再独立缓动分页位置；附加动画应根据版本取消。所有指示器关闭射线。

标题栏支持 Equal/ContentWidth。窄屏无法等分最小热区时保留最小宽度并横向滚动；标题栏本身的横拖不移动页面。LayoutError 表示当前布局不能满足热区，交互会暂时关闭。

## 获取状态与处理失败

- CurrentPageId 是已提交页，TargetPageId 是过渡目标。
- Pager.Position 是连续视觉位置；IsTransitioning 表示横向吸附。
- SelectionChanged 仅在正式 Id 改变时触发；ItemReselected 用于重复点击。
- SelectionFailed 与 LastError 报告创建/绑定失败。
- CancelInteraction(reason) 释放手势并恢复已提交页。
- 可打开 `view.Diagnostics.Enabled`，通过 `Snapshot()` 获取固定容量事件记录；默认关闭，不记录每帧位移。

结构更新移除了旧当前页，而新页创建失败时，保留新数据并进入无当前页错误状态；不恢复已经失效的旧页。后续显式 Select 或 SetPages 可恢复。

## 修复示例页面引用

若打开示例时报 `Page source must be exactly one valid asset, factory or instance`，先退出 Play，选择 **Tools → Segment UI → Reimport Demo References**，再检查 Demo 的页面 Prefab 引用是否完整。
