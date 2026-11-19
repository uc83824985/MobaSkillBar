## Why

玩家需要在画面上直接选中、移动和缩放技能格，并让对战中的预览留在编辑时看到的位置。窗口切换引起的位置偏移会让玩家反复校准技能栏。

## What Changes

- 给选中的技能格显示边框和八个缩放控制点，保留等比缩放。
- 让编辑和显示模式使用相同的窗口和技能格几何。
- 将实现与 OpenSpec 集中到 `source/`，从根目录的 `run.bat` 启动。说明当前预览帧率。

## Capabilities

### New Capabilities

### Modified Capabilities

- `floating-layout-editor`：画布上的对象选中、移动、缩放，以及模式切换时的位置和尺寸一致性。

## Impact

影响 WPF 悬浮栏和启动路径。沿用现有布局文件格式和捕获周期。
