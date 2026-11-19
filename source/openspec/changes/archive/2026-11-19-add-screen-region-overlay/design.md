## Context

原型面向 Windows 11 桌面及无边框游戏。屏幕原有技能格提供显示内容，用户负责把它们放到便于观察的位置。

## Goals

- 通过普通桌面窗口完成取景、布局和显示。游戏保持接收鼠标输入。
- 按控件调整位置、比例、透明度和对比度，并将调整保存在命名布局中。

## Decisions

- 使用 .NET 8 WPF 控制窗口和支持鼠标穿透的分层悬浮窗。控件布局使用 WPF DIP。源区域、悬浮窗位置和重叠检测使用物理像素。
- Windows Graphics Capture 按活跃显示器共享 D3D11 帧池。UI 最多以 30 Hz 消费最新帧，仅把所需裁剪区域读回内存。使用与 128 中灰的距离调整 RGB 对比度，再由 WPF 合成透明度。
- 使用 `WDA_EXCLUDEFROMCAPTURE` 排除悬浮窗口。若系统拒绝排除，暂停受重叠影响的控件并提示移动悬浮栏。正常屏幕遮挡按 Windows 捕获结果显示。
- 在 `%LOCALAPPDATA%\MobaSkillBar\layouts.json` 原子保存 JSON。布局为本地配置，画面帧只保留在内存中。

## Risks and verification

- 受保护内容和独占全屏的表现取决于 Windows 和应用。验收覆盖普通桌面及可用的无边框游戏。
- 混合 DPI 和屏幕重连用物理像素框选验收，并验证悬浮栏位置恢复。

## Migration Plan

首次启动创建空白布局，可直接从屏幕选择技能格。
