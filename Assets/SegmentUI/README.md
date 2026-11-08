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

### 在拖拽和动画期间测试 UpdateItem

1. 点击底部 **LIVE: OFF** 切换到 **LIVE: ON**。Activity 标题与角标每 0.1 秒更新一次，图标保持不变；诊断区 `UPDATES` 显示累计次数，角标每十次会隐藏一次。
2. 按住页面横向拖动并停在两页之间：标题继续变化，页面应保持在手指所在的位置，不跳回已选页。
3. 上下拖动页面列表，或横向拖动标题栏：持续更新不应抢走手势。
4. 点击 Activity 切页，或点击 **STRESS** 连续请求切页，观察更新期间动画继续执行。使用 **STYLE** 的内容宽度模式可检查标题变长后的布局和指示器，使用 **DIR** 检查 RTL。
5. 再次点击 **LIVE: ON** 停止更新，保留最后显示的数据。**CANCEL** 只取消切页和压力操作，持续更新需要通过 LIVE 单独关闭。

### 嵌套子分页的持续更新

开启 **LIVE** 后进入 **Nested**，First / Middle / Last 三个子标题也会每 0.1 秒更新序号和数字角标；诊断区第三行显示子分页的 `UPDATES`。角标每十次隐藏一次，再次更新时恢复。

- 在子分页中横拖并保持按下，标题仍会更新；松手后应正常完成子页切换。
- 在子页内上下滚动原生列表，更新不会切换父页或子页。
- 子页处于 First/Last 时向外拖动，手势交给父分页；更新期间也应正常完成交接。
- 切换 **DIR** 后重复上述操作。RTL 下向右进入下一子页，First 向左 / Last 向右交给父分页。
- LIVE 只更新已创建 Nested 的标题，不提前创建未访问的业务子页。离开 Nested 后缓存标题继续更新，返回时保留子页选择与列表位置。关闭 LIVE 同时停止父、子标题更新。

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
    SegmentPageDescriptor[] pages;

    void Start()
    {
        pages = new[]
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
        };
        segments.SetPages(pages, initialPageId: "all");
    }
}
```

也可在 SegmentView Inspector 的 Initial Pages 中配置 Prefab/ExistingInstance 来源。Id 必须稳定、非空、唯一；标题和页面通过同一描述项关联，不依赖两个数组的索引。

`SetPages` 会复制传入数据。运行时只更新标题、图标或角标时使用下方的 `UpdateItem`，避免重复提交页面列表打断交互。业务依赖变化时增加 `PageRevision` 并调用 `SetPages`；修改 Prefab、Factory 实例或来源版本会释放旧页面实例。

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

### 只修改标题或角标

沿用上例保存的 `pages`，修改业务数据后提交对应 Item：

```csharp
// 修改收藏页角标，同时保留原来的标题和图标。
var page = pages[1];
page.Item.BadgeCount = 5;
bool accepted = segments.UpdateItem(page.Id, page.Item);

// 修改标题。
page.Item.Title = "我的收藏";
segments.UpdateItem(page.Id, page.Item);

// 隐藏角标。
page.Item.BadgeCount = 0;
segments.UpdateItem(page.Id, page.Item);
```

同步维护 `pages` 中的数据，可以避免之后调用 `SetPages(pages)` 时旧值覆盖当前显示。仅修改外部数据不会自动刷新，必须调用 `UpdateItem`。

### 替换整份标题数据

```csharp
var page = pages[1];
page.Item = new SegmentItemData
{
    Title = "我的收藏",
    Icon = favoritesIcon, // 业务持有的 Sprite；传 null 清除图标
    BadgeCount = 5
};
segments.UpdateItem(page.Id, page.Item);
```

`UpdateItem` 会替换 Title、Icon、BadgeCount，**不是只更新填写的字段**。例如只传 `new SegmentItemData { BadgeCount = 5 }` 会同时清除标题和图标。仅改一个字段时，使用前面的保留数据写法。

### 调用规则

- 在 Unity 主线程调用；调用后无需再调用 `SetPages`。
- 不重建标题项或 Page，不重新绑定业务页面，不打断拖拽、标题栏惯性或切页动画。
- 标题宽度和指示器随内容刷新；不主动居中，内容缩短时只将滚动位置限制在新的有效范围内。
- 保留当前 `IsEnabled`，传入值不用于改变可选页面序列。
- 输入会被复制，调用后再修改该对象不会自动刷新。
- 返回 true 表示接受更新；未知/null PageId 或正在销毁的组件返回 false。itemData 为 null 时抛出 `ArgumentNullException`。
- 在回调内调用会排队；若前面的排队操作已删除该页，则跳过更新。

### 什么时候使用 SetPages

| 修改内容 | 调用方法 |
| --- | --- |
| 标题、图标、角标 | `UpdateItem(pageId, itemData)` |
| 新增、删除、排序、启用或禁用页面 | `SetPages(pages)` |
| 修改标题模板、页面来源 | `SetPages(pages)` |
| 更新业务绑定数据 | 增加对应 `PageRevision` 后调用 `SetPages(pages)` |

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
