# Test Assets

These images are deliberately obvious visual fixtures for texture compression
work. The original generated images are stored in `source/`; the normalized
512x512 PNGs in `normalized/` are the files intended for tests and visual QA.

## Processed Fixtures

- `normalized/hard-edges-512.png`: saturated color bars, diagonal hard edges,
  line patterns, checkerboards, and colored circles. Use this for block edge,
  channel order, ringing, and sharp transition checks.
- `normalized/gradients-512.png`: grayscale and chromatic ramps with soft radial
  transitions. Use this for banding, quantization, and color-space mistakes.
- `normalized/fine-detail-512.png`: woven fibers, scratches, holes, speckles, and
  repeated micro-patterns. Use this for high-frequency detail loss and block
  artifact checks.
- `normalized/natural-scene-512.png`: photoreal fruit, cloth, leaves, wood, soft
  shadows, and saturated colors. Use this for realistic visual regression
  checks after synthetic fixtures pass.

## Generation Notes

Generated with the built-in image generation tool, then resized to 512x512 with
`sips`. The generated source images were 1254x1254 PNGs.
