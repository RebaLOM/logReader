# LOGER 2 application icon

`loger2.png` is the original generated RGBA source. `loger2.ico` is its Windows
container, with 16, 20, 24, 32, 48, 64, 128 and 256 pixel versions. The transparent
margin is retained. The classic application's `logger.ico` is unchanged.

Generated with the built-in image generation tool. Final prompt:

> Use case: logo-brand. Asset type: Windows desktop application icon for LOGER, an industrial CAN bus log analysis and vehicle diagnostics application. Primary request: one striking, very simple geometric yellow L symbol integrated with a short square-wave signal trace, centered on a charcoal rounded-square tile. Style: flat precise vector-like raster icon, bold clean edges, solid warm yellow #F4CC45 and near-black charcoal #171B22, no gradients, no gloss, no shadow, no texture. Composition: one icon only, square canvas, tile fills about 90% of the canvas with small balanced transparent margin; chunky L at left and broad trace step extending to the upper right with one square pulse, unified graphic, legible at 16 pixels. Transparent background outside the rounded tile. Constraints: no text, no numerals, no 2.0, no wordmark, no letters other than the abstract L-shaped mark, no border, no badge, no extra symbols, no watermark. All shapes in the main symbol should be thick and simple, avoid thin traces and fine gaps.

The UI embeds the ICO independently from the EXE icon so every window receives
the same asset, even if opened without a main window. Each form owns an icon clone.
