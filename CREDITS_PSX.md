# PSX Retro Graphics Credits & Licenses

All external shaders, algorithms, and references used for the PlayStation 1 (PSX) retro look implementation in **as-a-kid-ep1-unity** are strictly licensed under permissive open-source licenses allowing free commercial use and distribution in a shipped game.

---

### 1. VNTG Shader Pack for Unity URP
- **Author:** Colby-O (Colby O'Keefe)
- **Repository / Source:** [https://github.com/Colby-O2/VNTG](https://github.com/Colby-O2/VNTG)
- **License:** The Unlicense (Public Domain)
- **Used For:** PSX fullscreen render pass foundations, ordered Bayer dithering matrix math, color quantization, and shader subgraphs.
- **License Text:**
  ```text
  This is free and unencumbered software released into the public domain.

  Anyone is free to copy, modify, publish, use, compile, sell, or
  distribute this software, either in source code form or as a compiled
  binary, for any purpose, commercial or non-commercial, and by any
  means.

  In jurisdictions that recognize copyright laws, the author or authors
  of this software dedicate any and all copyright interest in the
  software to the public domain. We make this dedication for the benefit
  of the public at large and to the detriment of our heirs and
  successors. We intend this dedication to be an overt act of
  relinquishment in perpetuity of all present and future rights to this
  software under copyright law.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
  EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
  MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
  IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
  OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
  ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
  OTHER DEALINGS IN THE SOFTWARE.

  For more information, please refer to <https://unlicense.org>
  ```

---

### 2. URP-PSX Custom Lighting (PSXLighting_URP.hlsl)
- **Original Author:** Codrin-Mihail
- **Modifications Author:** Colby-O
- **Source:** [https://github.com/Kodrin/URP-PSX](https://github.com/Kodrin/URP-PSX)
- **License:** MIT License
- **Used For:** URP lighting loop adaptation, texel snapping logic, and vertex lighting models.
- **License Text:**
  ```text
  MIT License

  Copyright (c) 2020 Codrin-Mihail

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
  ```

---

### 3. PS1 Screen-Space Vertex Snapping & Affine Mapping Implementation
- **Implementation:** Custom hand-crafted HLSL shaders (`PSX_Lit.shader`, `PSX_Unlit.shader`, `PSXPostProcess_URP.shader`) and C# render features (`PSXRenderFeature.cs`, `PSXSystem.cs`).
- **Mathematical Foundations:** Classical Sony PlayStation 1 GTE (Geometry Transformation Engine) fixed-point coordinate truncation ($NDC \times \text{Precision}$) and non-perspective UV interpolation ($\text{UV} \cdot W$).
- **License:** MIT License / Public Domain (Project-owned).
