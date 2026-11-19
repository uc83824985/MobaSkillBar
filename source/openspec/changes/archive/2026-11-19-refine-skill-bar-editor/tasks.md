## 1. Editor and mode transitions

- [x] 1.1 Implement direct selection and eight proportional resize handles.
- [x] 1.2 Keep tile screen rectangles identical across edit and display modes.

## 2. Repository and verification

- [x] 2.1 Move implementation and OpenSpec under source and add the root launcher and usage instructions.
- [x] 2.2 Verify launch, resizing, and mode geometry and confirm the current preview cadence.

验证覆盖实际鼠标选中、拖动与缩放、八个控制点的锚点计算、三次模式往返的屏幕矩形一致性，以及从其他工作目录运行 `run.bat`。动态桌面样本在 34 毫秒轮询配置下测得约 14.9 次有效画面更新每秒。
