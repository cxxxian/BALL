# Cabinet relighting trial

Checkpoint before this experiment: `b403c00`. Experiment changes are intentionally left uncommitted for comparison.

## Assets and controls

- `Assets/Art/SlotVisualLabV2/cabinet_albedo_trial.png`: 1024×1536, generated with built-in imagegen using the current cabinet as edit target. Removed hard white contours and most baked lamp spill while preserving layout.
- `Assets/Art/SlotVisualLabV2/cabinet_normal_trial.png`: 1024×1536, generated approximate tangent-space RGB normal directions for the same cabinet. Imported as uncompressed linear RGB data; the shader decodes RGB explicitly and rejects invalid dark boundary directions. It is not a mesh-baked normal map.
- `SlotCabinetRelighting.shader`: diffuse, restrained specular and Fresnel with an orthographic tangent-space view direction. Separate lamp-mask pass selects bright saturated source pixels from the original cabinet. Mask is a runtime RenderTexture, rather than another generated PNG.
- `SlotCabinetRelighting`: renders the cabinet into one live texture shared by assembly faces and the completed UI background. Text, glyphs, result borders and input continue through the existing controller. F8 toggles a moving demonstration light, also available in LAB. The original-image toggle has been removed.

V2 always uses the relit scheme. Its editor background uses the new albedo; its assembly and runtime UI share the relit output. The unchanged original cabinet is sampled only for lamp-mask and silhouette data. Normal strength, lamp strength, rim intensity/power and tangent-space light direction are exposed in the Inspector. The additional render targets and per-frame cabinet pass have not been profiled on mobile hardware. Removing hard bitmap contours reduces their visibility, but neither approximate normals nor this shader guarantee elimination of all raster aliasing.

## Final prompts (built-in imagegen)

### Normal map

Produce a production tangent-space NORMAL MAP for this exact UI cabinet texture, not a beauty render. Output single opaque RGB normal-map image at identical portrait aspect and exact pixel alignment (1024 x 1536 desired). Reference is edit target geometry; preserve ALL silhouette, top header bevels, three reel cavities, lower result plaque and hexagonal button positions exactly; no new features no text. Flat front-facing panels MUST be neutral blue RGB(128,128,255). Encode only surface direction of chamfered architectural bevels, side rails and cylindrical reel cavity walls. +X normals red, +Y normals green (OpenGL/Unity tangent convention green up), Z blue. Strong blue-purple normal-map appearance, controlled smooth gradients on reel cylinders and narrow clean bevel transitions, restrained depth; cyan/amber colors and white specular lines in input must NOT become painted colors or raised details; interpret actual geometry ONLY. Uniform neutral-blue outside cabinet too. No illumination, no shadows, no emissive cyan orange, no optical glow, no beauty colors, no labels. This must function as a technical normal texture to relight the original texture while leaving UI overlay shapes entirely alone.

### Albedo

Edit target is this exact cabinet image. Produce an ALBEDO/base-color texture for normal-map relighting in Unity, identical portrait aspect desired 1024x1536. Lock ALL geometry positions, silhouette, three reel windows, header, lower rectangular result plaque, hexagonal button and colored emitter paths EXACTLY; no redesign or new features. Remove the thin stark white specular contour lines and glossy white reflection highlights from bevels and reel cylinders, replacing them with clean dark graphite material. Remove baked light spill/halos and reflections from cyan and amber lamps; preserve only solid moderately dark cyan/amber light-source shapes at exactly same positions, antialiased edges. Black front panels should remain subtle flat dark graphite/black, very restrained broad shading acceptable to preserve shape. Main purpose: shader will supply highlights, Fresnel and Bloom, so NO white outlines, no glowing optical bloom, no prelit speculars. Keep cabinet recognizable and detailed but clean flat dark material, no texture noise or scratches. Keep exterior truly alpha transparent; do not add checkerboard. No text, icons, symbols or overlays.
