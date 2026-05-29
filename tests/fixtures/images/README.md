# Test Assets

These images are deliberately obvious visual fixtures for texture compression
work. Automated tests should reference the original generated images in
`source/`. The normalized 512x512 PNGs in `normalized/` are retained as smaller
visual QA copies.

## Source Fixtures

- `source/hard-edges-source.png`: saturated color bars, diagonal hard edges,
  line patterns, checkerboards, and colored circles. Use this for block edge,
  channel order, ringing, and sharp transition checks.
- `source/gradients-source.png`: grayscale and chromatic ramps with soft radial
  transitions. Use this for banding, quantization, and color-space mistakes.
- `source/fine-detail-source.png`: woven fibers, scratches, holes, speckles, and
  repeated micro-patterns. Use this for high-frequency detail loss and block
  artifact checks.
- `source/natural-scene-source.png`: photoreal fruit, cloth, leaves, wood, soft
  shadows, and saturated colors. Use this for realistic visual regression
  checks after synthetic fixtures pass.

## Generation Notes

Generated with the built-in image generation tool, then resized to 512x512 with
`sips`. The generated source images were 1254x1254 PNGs.
