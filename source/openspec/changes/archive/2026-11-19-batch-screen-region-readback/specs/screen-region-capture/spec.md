## ADDED Requirements

### Requirement: Batched preview readback
The application SHALL share a captured frame across regions on the same display. Nearby regions SHALL share a bounded rectangular readback, with each tile mapped to its own rectangle. The preview SHALL target up to 60 updates per second and consume completed readbacks without waiting for the GPU.

#### Scenario: Preview adjacent skill slots
- **WHEN** four to six nearby skill slots are captured on a single display
- **THEN** the application SHALL read their shared bounding box once per frame and display each tile's own pixel rectangle from that frame

#### Scenario: Preview widely separated regions
- **WHEN** combining regions would cause excessive readback of intervening pixels
- **THEN** the application SHALL use separate bounding boxes and preserve the mapping of each tile
