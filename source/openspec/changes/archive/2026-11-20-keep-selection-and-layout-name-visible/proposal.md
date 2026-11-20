## Why

详情面板获得焦点后，技能格的选中高亮变得难以辨认。重命名布局后，下拉框也需要立即呈现新名称，方便确认修改已经生效。

## What Changes

- 技能格的列表高亮随选中状态显示，支持在详情面板中继续操作。
- 重命名布局时立即刷新当前名称和下拉列表。

## Capabilities

### New Capabilities

### Modified Capabilities

- `floating-layout-editor`：在详情面板获得焦点时保持选中高亮。
- `layout-presets`：布局名称即时更新。

## Impact

修改列表样式和布局名称绑定，沿用现有布局数据。
