# SegmentUI — 独立 Unity UGUI 分段分页组件

适用于 Unity 2022.3 / UGUI 1.0。核心运行时不依赖业务工程或其他列表框架。完整示例、默认标题 Prefab、Editor 工具及测试需要同时导入 `Assets/SegmentUI` 和 `Assets/RTLLayout`；两个核心程序集保持独立，由可选桥接连接。

## 打开示例

- 菜单 **Tools → Segment UI → Open Demo**。
- 或打开 `Samples/Generated/SegmentDemo.unity`，进入 Play。
- 标题点击切页；内容左右拖动翻页、上下拖动滚动当前页。
- **Native** 页签使用系统 ScrollRect，Prefab 未预挂适配器；页面绑定后自动安装。首行是普通 Button，点击会增加 TAPS，拖动不应增加次数。
- 在 Native 页上下拖动，再横向转向，当前手势应继续纵向滚动；LTR 下重新按下后向右拖动可切回 Nested。切页后返回应保留列表位置，RESET LIST 重置已创建列表，STRESS 轮流请求全部四个父页签。
- **Nested** 位于 Activity 与 Native 之间，内部 First / Middle / Last 三页均使用原生 ScrollRect。LTR 下向左拖先从 First 切到 Middle/Last；在 First 向右拖交给父级返回 Activity，在 Last 向左拖交给父级进入 Native。交接仅发生在手势开始时，纵拖途中转向不会触发分页。
- Nested 子页也保留按钮 TAPS 计数。底部显示 `CHILD` 当前页和阶段；父级 CANCEL 可取消子页拖动，RESET LIST 包括已创建的子页列表；离开再返回 Nested 保留子页选择。
- 演示按竖屏设计，请将 Game View 设为 720×1280、1080×1920 或其他 9:16 附近的竖屏比例。过矮横屏不足以容纳提示区和两层标题栏时，分页会因视口空间不足暂停输入。
- **STRESS** 连续请求切页，**CANCEL** 取消过渡，**RESET LIST** 重置已创建页的纵向位置，**STYLE** 切换下划线与胶囊/描边组合。
- **DIR: LTR / RTL** 镜像标题顺序、图标/角标、页面滑动、嵌套边缘交接和演示文字对齐。数据顺序与 PageId 不变；RTL 向右滑进入逻辑下一页。演示仅使用英文验证方向，不演示阿拉伯文字形连接。
- **Tools → Segment UI → Validate** 检查引用、布局和三种视口比例；**Generate Demo** 生成预制体和场景。

演示只有一处 `SampleSafeArea`。生产组件继承宿主安全区，不重复计算安全区。

## 交付目录

| 目录 | 内容 |
| --- | --- |
| Runtime | 独立程序集 SegmentUI，仅引用 Unity.ugui |
| Prefabs | SegmentView、DefaultItem、BadgeItem、Underline、RoundedBackground、Outline |
| Samples | 自包含示例脚本、四个父页 Prefab（Nested 内复用原生子页）、演示 Canvas 和场景 |
| Editor | Seeder、配置校验器与截图工具，可选 |
| Tests | EditMode 与 PlayMode，需要 Unity Test Framework，可选 |

复制组件目录时保留全部 `.meta`。使用默认 Prefab 时同时保留 RTLLayout 与 Integrations/RTLLayout；生产工程可移除两库的 Tests 以及 SegmentUI 的 Samples、Editor。仅需 SegmentUI 核心时可只导入 Runtime，并自行创建标题和容器 Prefab。默认 SegmentView.prefab 不引用样例页面，业务自行提供。

## 传入标题项与页面

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

## PageContent 布局与懒加载

```text
PageContent (HorizontalLayoutGroup + ContentSizeFitter)
├─ PageSlot-a (LayoutElement + CanvasGroup)
│  └─ PageA（已创建）
├─ PageSlot-b（空槽位）
└─ PageSlot-c（空槽位）
```

SetPages 提前建立所有逻辑页的轻量槽位；业务 Page 仍在首次显示/预览时创建。跳过 B 直接选择 C 不会压缩 B 的占位。禁用页的槽位保留，但 ignoreLayout=true，不占分页宽度。

- HorizontalLayoutGroup 排列槽位、控制槽位宽高；Spacing/Padding 为 0，关闭强制扩展。
- 槽位 LayoutElement 提供视口宽高，视口变化时更新；RTL 使用 reverseArrangement。
- ContentSizeFitter 的 Horizontal Fit=Preferred Size，Vertical Fit=Unconstrained；Content 高度跟随视口拉伸。
- SegmentPager 只写分页横向位移，不再手工写 Content 总宽度或槽位坐标。

PageContent 是组件专用布局区域。ExistingInstance 的槽位在这里期间也由组件管理 LayoutElement 和几何；解绑不销毁外部页面，但仍留在 PageContent 的已释放槽位会 ignoreLayout=true，避免影响其他页的总宽。业务接回已有页面后如需独立布局，应迁出该区域并设置自己的布局参数，不承诺恢复槽位原始布局配置。

## RTL 布局与交互

```csharp
segments.SetDirection(SegmentLayoutDirection.RightToLeft);
segments.SetDirection(SegmentLayoutDirection.LeftToRight);
childSegments.SetDirection(SegmentLayoutDirection.Inherit); // 默认
```

根节点 Inherit 默认 LTR；子组件继承最近的父 SegmentView，也可显式覆盖。Inspector 的 Direction 同样可配置。IsRightToLeft 返回实际方向。

逻辑索引、PageId、SetPages 顺序、Select 和回调语义保持不变。RTL 将逻辑第一项放在右侧，向右拖进入下一项；标题与指示器一起镜像，原生列表的上下方向不变。嵌套交接在子页的逻辑起点/终点判定，然后将同一物理手势交给父级，因此父子也可以使用不同方向。

运行时切换方向会取消手势及未完成吸附，回到已提交页面；保持页面实例和子页选择，不重新绑定业务数据。自动键盘导航按镜像后的 Item 几何寻找相邻按钮。

容器与业务布局分离：SegmentUI 核心只管理 Items/PageContent 排列、分页位置与交互，Item 内部布局不再自动镜像。`SegmentItemView`、`SegmentPage` 和 `SegmentView` 均提供 `DirectionChanged` 通知；Item 每次 Bind 后即使方向相同也通知，Page 每次绑定后、展示前通知。原有虚方法保留供业务方向语义扩展。

业务布局由独立库 [`RTLLayout`](https://github.com/zhangguozhong/RTLLayout) 负责。可选桥接程序集 `SegmentUI.RTLLayout` 引用两者，核心程序集互不依赖。在 Page/Item 根挂 `RtlLayoutRoot` 与 `SegmentRtlLayoutBridge` 即可。默认 Item 和演示 Page 已接入；使用这些预制体需一并保留桥接及 RTLLayout，纯 SegmentUI 核心可以独立使用。Demo 根桥接通过 SourceView 获取方向。

演示中嵌入 SegmentView 的节点挂 `RtlLayoutNode.IgnoreSubtree`，阻止外层通用收集及方向传播。内层 Page/Item 桥接只应用所属 SegmentView 的有效方向，避免外层 RTL 覆盖显式 LTR 子分页。业务树内部嵌套适配根则正常继承方向，按属性独占管理；首次应用发生在 Bind 后，禁用再启用桥接会同步当前状态。

本次按约定仅处理布局和交互；UGUI Text 的阿拉伯文字形连接、双向混排和字体回退需要业务接入相应文本渲染方案，可通过上述接口实现。

## 页面内部纵向滚动

```text
Page (SegmentPage)
└─ Scroll (SegmentScrollRect)
   └─ Viewport (RectMask2D + raycastable Image)
      └─ Content (普通 UGUI 布局/内容)
```

`SegmentScrollRect` 继承 UGUI ScrollRect，纵向页设 `horizontal=false`、`vertical=true`，正常绑定 viewport/content。它保留原生纵向惯性和回弹，通过 SegmentView 的作用域仲裁输入。也支持保留原生 ScrollRect，通过下述适配器接入同一套拖动仲裁。

### 保留业务原生 ScrollRect

页面绑定完成后，组件自动为 `vertical=true`、`horizontal=false` 的普通 ScrollRect 注册 `SegmentScrollRectAdapter`。原组件、viewport/content 引用、滚动参数和 UnityEvent 不替换。已使用 SegmentScrollRect 的列表会跳过。

```text
ScrollView (原生 ScrollRect + SegmentScrollRectAdapter)
└─ Viewport
   └─ Content
```

适配器实际挂在 **ScrollRect 同一节点**，不是 Viewport。它在 `OnInitializePotentialDrag` 中将 `pointerDrag` 转到无射线的专用代理，后续 Begin/Drag/End 只有代理接收；获准纵拖后再直接调用原生 ScrollRect。这样也支持 viewport 与 ScrollRect 在同一节点、以及直接命中列表根背景的情况，避免依赖射线一定落在 Viewport 子树。它不通过同时监听两个 Drag 回调来抵消滚动，也不改变业务按钮的点击目标。

动态新增列表或普通按钮后可显式注册，重复调用不会重复添加适配器：

```csharp
SegmentScrollRectAdapter.Install(newContentRoot);
```

也可手动在原生纵向 ScrollRect 所在节点挂 SegmentScrollRectAdapter。组件无效、移除或页面释放时结束当前授权手势；离开有效页面管理范围后恢复原生拖动分发。自动添加的适配器可以随已有页面保留，以便再次绑定使用。

与 SegmentScrollRect 一样，适配器公开 `OwnedBegin/OwnedDrag/OwnedEnd/OwnedCancel`。纵拖使用原生惯性；取消停止速度，取消后迟到的拖动不会继续写入位置。禁用原生 ScrollRect 本身会在适配器下一次事件或 LateUpdate 时取消所有权。

**兼容范围：** 已验证 Unity 2022.3 的 StandaloneInputModule 触摸式指针分发和鼠标拖动。原生滚轮/滚动条仍由 ScrollRect 自己处理，不保证其与分页拖动互斥；需要滚轮受控行为可使用 SegmentScrollRect。横向/双向 ScrollRect 不自动适配；子项自带拖拽处理器、第三方改写事件分发的 ScrollRect 子类及 InputSystemUIInputModule 需要额外验证，不能据此宣称任意列表都已兼容。

### 共享拖动规则

- 超过 EventSystem 起拖阈值后判定方向；横向需超过纵向的 1.2 倍，对角线优先纵向。
- 同次手势锁定方向，不在列表到顶/底后转为翻页。
- 一组嵌套组件只允许一个有效指针；其他手指不能接管已有手势。
- 分页停在两页之间时，再次触摸先接管横向分页；该次不会启动纵向滚动。
- 新过渡从当前视觉位置继续；旧动画不能提交状态。

默认标题使用 `SegmentButton`。初次绑定时组件会为页面已有普通 Button 安装 `SegmentPointerRelay`；业务之后动态创建普通按钮时，调用 `SegmentPointerRelay.Install(newContentRoot)`，或直接使用 SegmentButton。

自定义刷新/拖拽行为可订阅 `SegmentScrollRect.OwnedBegin/OwnedDrag/OwnedEnd/OwnedCancel`，这些事件仅在获准接收手势后分发。取消与正常松手不同，不能用取消提交刷新。组件不自动改造第三方组件内部的原始拖动处理器，业务需要自行接入授权事件。本包不包含某个外部列表框架的适配。

## 生命周期与异步任务

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

## Item 与指示器样式

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

## 状态与诊断

- CurrentPageId 是已提交页，TargetPageId 是过渡目标。
- Pager.Position 是连续视觉位置；IsTransitioning 表示横向吸附。
- SelectionChanged 仅在正式 Id 改变时触发；ItemReselected 用于重复点击。
- SelectionFailed 与 LastError 报告创建/绑定失败。
- CancelInteraction(reason) 释放手势并恢复已提交页。
- 可打开 `view.Diagnostics.Enabled`，通过 `Snapshot()` 获取固定容量事件记录；默认关闭，不记录每帧位移。

结构更新移除了旧当前页，而新页创建失败时，保留新数据并进入无当前页错误状态；不恢复已经失效的旧页。后续显式 Select 或 SetPages 可恢复。

## 验证与限制

测试包含快照校验、来源/修订更新、生命周期回滚和重入、预绑定页、对象池所有权、随机压力序列，以及真实 UGUI 输入模块的点击与拖动。具体执行结果见 `Verification.md`，原始结果和截图在项目 `Docs/SegmentUIVerification`。

未实现无限分页、同方向嵌套中途接力、共享吸顶头部、异步 Prefab 加载及自动页面淘汰。Editor 自动化与截图不等于微信/移动设备触摸验收；最终方向阈值与手感仍需目标设备确认。

### 演示页面来源为空时

若 `SegmentDemo.Initialize` 报 `Page source must be exactly one valid asset, factory or instance: activity`，先检查 Demo 的 `PageB`（以及 `PageNative`、`PageNested`）引用。外部更新生成 Prefab 后，已打开的 Unity Editor 可能仍缓存空的组件引用，即使磁盘上的 GUID/fileID 正确。

退出 Play 后执行 **Tools > Segment UI > Reimport Demo References**。该操作依次重导入四个页面和 Demo Prefab，不改写资产文件或保存场景。`Generate Demo` 检测到已有演示缺少页面引用时也会尝试此恢复。若磁盘引用本身缺失，工具会明确报错，需要修正生成 Prefab 的引用；运行时不会跳过 PageSource 校验。初始化失败后补回引用，可以再次调用 `Initialize()`。

### Prefab 编辑期收集

业务 Prefab 的 `RtlLayoutRoot` Inspector 提供 **Discover Components & Bake LTR (Reset Selection)** 按钮。先以 LTR 完成布局和节点策略，再收集并保存 Prefab；节点与原始布局数据随实例化一起复制。演示 Seeder 在保存前调用 `CaptureLayout()`，默认 Item、各 Page 与演示根均预存快照。运行时优先使用这些快照，新增节点才在 Bind 后作为兜底收集。

预收集布局是固定的横向基准；若业务需要新的固定布局，请在编辑期更新并重新收集。未预收集的动态节点必须先完成 LTR 配置，再应用方向。

适配根的 Inspector 中，`Collected Components`、`Ignored Nodes`、`Mirrored Nodes` 是可编辑配置，原始布局快照只读。编辑列表后使用 **Bake Selected Components (Keep Edited Lists)** 保存基准；再次 Discover 会重新发现组件并重置收集选择。反转列表使用原始 `localScale.x` 的相反数，恢复 LTR 时还原，不再添加图片 MeshEffect；它会整体反转该视觉节点及子节点。
