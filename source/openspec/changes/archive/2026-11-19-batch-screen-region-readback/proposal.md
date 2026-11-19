## Why

同一显示器上的多个技能格来自同一帧。合并邻近区域的读取可以降低传输和同步开销，为流畅预览留出更多时间。

## What Changes

- 将邻近技能格合并成包围盒，从一次读回的数据中重新映射出各个技能格。
- 限制分散区域合并时读取的空白像素，并轮换读回缓冲区。
- 将预览刷新目标提高到每秒 60 帧，按桌面实际更新和 GPU 完成情况提交画面。

## Capabilities

### New Capabilities

### Modified Capabilities

- `screen-region-capture`：共享显示器帧、合批读回和预览节奏。

## Impact

改变 Windows 捕获到 WPF 预览之间的数据流，沿用已有布局与框选区域。
