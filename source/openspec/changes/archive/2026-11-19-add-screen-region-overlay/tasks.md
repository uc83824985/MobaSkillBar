## 1. Capture

- [x] 1.1 Create the WPF app and implement DPI-aware region selection.
- [x] 1.2 Implement shared monitor capture, cropped frames, and recovery.

## 2. Floating HUD

- [x] 2.1 Add transparent display and editable placement with click-through and feedback prevention.
- [x] 2.2 Add per-tile scale, opacity, contrast, and stacking controls.

## 3. Presets and verification

- [x] 3.1 Implement layout management and local persistence.
- [x] 3.2 Verify strict specifications, build, and desktop scenarios supported by the environment.

Desktop smoke tests covered two live sources, two displays at 150% DPI, actual overlap with capture exclusion, native pointer hit testing, simulated display loss and restoration, picker cancellation, and preset persistence. Mixed-DPI displays and an in-match MOBA session remain unverified.
