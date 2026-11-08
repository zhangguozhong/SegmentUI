# 验证记录

2026-11-09 发布前在独立 Unity 工程联合导入 SegmentUI、RTLLayout，使用 Unity 2022.3.62f3c1、UGUI 1.0.0、Unity Test Framework 1.1.33：

| 模式 | 通过 | 失败 |
| --- | ---: | ---: |
| EditMode | 99 | 0 |
| PlayMode | 44 | 0 |

计数包含两个组件、桥接、资产契约与继承夹具执行，不等于独立需求数量。

覆盖生命周期、页面来源和所有权、槽位与懒加载、原生 ScrollRect 和 Button、嵌套手势、三个种子各 6000 次压力操作、RTL 方向切换、布局基准与嵌套所有权、重复收集和 Inspector 临时预览。预览测试校验序列化缓存不变、退出恢复未烘焙编辑值、保存前恢复以及普通 Transform 缩放恢复。

此前已实际检查三种竖屏尺寸的示例截图；本轮未修改运行时与预制体。Prefab Stage 手工验证 LTR/RTL 切换及退出恢复。图片见仓库 Docs/Images。

自动化不等于真机验收。新 Input System、iOS/Android/微信设备触摸、系统中断、安全区、设备帧率与 GC 基准仍需目标项目验证。
