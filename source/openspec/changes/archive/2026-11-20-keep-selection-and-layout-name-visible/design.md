## Decisions

- 用列表行的 `IsSelected` 驱动系统高亮色，使选中行在编辑详情时仍清楚可见。
- 布局名称通过 `INotifyPropertyChanged` 通知当前项和下拉列表，保留同一个布局对象与选中状态。
