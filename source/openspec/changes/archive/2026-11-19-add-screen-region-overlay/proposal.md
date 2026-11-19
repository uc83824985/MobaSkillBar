## Why

MOBA 技能格通常位于画面底部。让玩家把这些技能格的实时画面放到中央视线附近，可以减少对战中查看技能状态时的视线移动。

## What Changes

- 框选屏幕上的技能格，将实时裁剪画面显示为独立控件。
- 在透明置顶技能栏内重新排列、缩放控件，并独立调整透明度和对比度。
- 保存、命名和切换多套布局。显示模式下可穿透悬浮栏操作下方应用。

## Capabilities

### New Capabilities

- `screen-region-capture`：屏幕框选和实时区域预览。
- `floating-layout-editor`：悬浮显示、排列和画面调整。
- `layout-presets`：多套布局与本地自动保存。

### Modified Capabilities

## Impact

新增 Windows 11 桌面应用。使用 Windows Graphics Capture，画面在本机内存中处理，布局保存在当前用户目录。
