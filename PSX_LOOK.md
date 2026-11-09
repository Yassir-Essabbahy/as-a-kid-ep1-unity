# Authentic PlayStation 1 (PSX) Visual Pipeline Architecture

## 1. Executive Summary & Aesthetic Goals
This project transitions **"as-a-kid-ep1-unity"** from a simulated VHS cassette filter (fisheye barrel distortion, chromatic bleed, moving analog tape noise, and scanlines) to a faithful, hardware-accurate **PlayStation 1 (PSX / PS1)** aesthetic suitable for atmospheric psychological horror on PC and Android.

Rather than overlaying a synthetic CRT or VHS filter, an authentic PSX look is achieved through the intrinsic architectural quirks of the original 1994 Sony PlayStation hardware:
1. **Screen-Space Vertex Snapping (Jitter):** Simulates the PS1 GTE lack of subpixel precision and fixed-point vertex coordinates, creating characteristic wobbling geometry during camera movement.
2. **Affine Texture Warping:** Simulates the lack of perspective-correct texture coordinate interpolation ($1/W$), allowing textures to swim subtly across polygon surfaces without destroying legibility.
3. **Aspect-Aware Low-Resolution Rendering:** Renders 3D geometry at genuine retro resolutions (~240p to 270p vertical lines) with point-filtering upscale. Width dynamically scales to match camera aspect ratio ($W = \text{round}(H \times \text{aspect})$), ensuring pixels are geometrically square across 16:9, 19.5:9, 20:9, and 21:9 displays without distortion.
4. **15-bit Color Depth Quantization & 4x4 Bayer Ordered Dithering:** Recreates the PS1's 5-bits-per-channel framebuffer (32,768 colors) with hardware Bayer matrix dithering to eliminate smooth gradients while preventing harsh color stepping.
5. **Atmospheric Distance Fog & Vertex Lighting:** Gouraud/vertex-shaded diffuse lighting paired with dense, tinted distance fog reminiscent of *Silent Hill* and *Resident Evil*.
6. **Point-Filtered Textures:** Removes modern bilinear blur from textures up close while preserving critical text readability.
7. **Razor-Sharp UI Canvas Isolation:** Renders UI (`ScreenSpaceOverlay` Canvas, subtitles, dialogue prompts, interactive crosshairs) after the low-res post-process pass, keeping all text, touch buttons, and safe-area boundaries 100% crisp and readable on mobile devices.

---

## 2. Research & Comparison of Approaches

| Approach | Pros | Cons | Verdict |
| :--- | :--- | :--- | :--- |
| **A. Monolithic Asset Pack (e.g. RetroPSX-URP / PSX-Effects)** | Comprehensive out-of-the-box. | Heavy dependency tree, alters URP packages, high variant count, often breaks on mobile Android or Unity 6 RenderGraph. | **Rejected** for core pipeline to prevent build instability. |
| **B. Unmodified VNTG (Colby-O)** | Already in project, Unlicense license, Unity 6 RenderGraph compatible. | Lacks dynamic aspect-ratio calculation (stretches on 20:9 mobile screens), CRT filter contains unwanted VHS noise/scanlines, no global singleton runtime controller, materials not yet converted. | **Adopted as base, upgraded & specialized.** |
| **C. Hybrid Handcrafted Architecture (Chosen)** | Combines VNTG's permissive RenderGraph pass with a custom aspect-aware `PSXRenderFeature`, dedicated lightweight `PSX/PSX_Lit` & `PSX/PSX_Unlit` HLSL shaders, and a global `PSXSystem` manager. | Requires careful material conversion across project scenes. | **Selected (Winner)**: Clean, zero-bloat, 100% compliant licensing, maximal mobile performance. |

---

## 3. System Architecture & Components

```
                      [ PSXSystem.cs ] (Global Singleton & Presets)
                             │
       ┌─────────────────────┴──────────────────────┐
       ▼                                            ▼
[ Global Shader Uniforms ]                 [ PSXRenderFeature (URP) ]
  • _PSX_VertexPrecision                     • Aspect-Aware Low-Res Blit
  • _PSX_AffineStrength                      • 4x4 Bayer Matrix Dither
  • _PSX_ScreenResolution                    • 15-bit Color Quantization
  • _PSX_FogColor & Density                  • Atmospheric Depth Fog
       │                                            │
       ▼                                            ▼
[ PSX Shaders: Lit & Unlit ]               [ 3D Scene Output (240p/270p) ]
  • Per-vertex screen snapping                      │
  • Non-perspective UV warp                         ▼
  • Gouraud / Vertex diffuse lighting      [ Screen Space UI Canvas ]
  • Alpha cutout & transparency              • Crisp subtitles & touch controls
```

### Quality Presets
1. **PC (High):**
   - Target Height: 270p (480x270 at 16:9)
   - Color Precision: 32 levels per channel (authentic 15-bit RGB555)
   - Dither: 4x4 Bayer matrix, additive/multiplicative hybrid
   - Vertex Snapping: Moderate (precision 180..240)
   - Affine Warping: Subtle (0.15..0.25) to preserve architectural lines
   - Fog: Active, atmospheric depth fog
   - SSAO: Enabled
2. **Mobile (Android / Low):**
   - Target Height: 240p (426x240 at 16:9, 533x240 at 20:9)
   - Color Precision: 32 levels per channel
   - Dither: Fast ordered Bayer dither
   - Vertex Snapping: Optimized (precision 140..180)
   - Affine Warping: Controlled (0.1..0.2)
   - Fog: Cheap exponential depth fog
   - SSAO: Disabled (drastically reduces mobile bandwidth & battery draw)

---

## 4. Mobile & Touch Screen Considerations
- **Touch Interaction & Raycasting:** Camera projection matrices and screen coordinates remain unaltered; only rasterized output is quantized. Input raycasts (e.g. touch/mouse to interact with doors, backpack, radio) remain pixel-accurate.
- **Aspect Ratios:** Automatically adapts between 16:9, 18:9, 19.5:9, 20:9, and 21:9 displays without distortion or pillarboxing.
- **Safe Area:** Mobile UI elements anchor to standard Unity Screen.safeArea without clipping or low-res blurring.
