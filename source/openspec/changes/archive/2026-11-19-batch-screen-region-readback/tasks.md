## 1. Capture pipeline

- [x] 1.1 Group capture regions into bounded readback rectangles and map the tiles by offset.
- [x] 1.2 Pipeline GPU copies with reusable buffers and update the preview cadence.

## 2. Verification

- [x] 2.1 Verify six live regions, readback batching, pixel positions, capture recovery, and measured preview cadence.

本机测试验证了六路同帧像素、每帧一个 120 × 69 像素的读回区域、远距离区域拆分、双屏和捕获恢复。最终版本的动态桌面样本测得每秒 58.2 次预览更新。实际对战帧时间待测。
