# Low Poly Cloud Skybox (URP)

Shader: `RPG/Skybox/Low Poly Clouds`.
Ready-to-use material: `Assets/Materials/LowPolyCloudSkybox.mat`.

## Day/night controller and editor preview

Add **Day Night Cycle** (`World.DayNightCycle`) to one GameObject in the scene.
Assign **Sun** (a Directional Light), **Skybox Material**, and optionally the scene's
**Volume** containing **Color Adjustments** and **Volumetric Fog**.

The Inspector has four lighting sets: **Night** (0 / 1), **Dawn** (0.25), **Day**
(0.5), and **Dusk** (0.75). Each set contains numbers and colors for sun pitch,
light intensity, sky exposure, sun disk intensity, Post Exposure, fog Density and
Attenuation Distance, and the light/sky/cloud/ambient/ground palette. These values
blend smoothly between adjacent sets, including dusk -> night -> dawn. Only the
night-time light angle has a separate curve.

**Light Path** separates light movement from the visible sun:

- **Night Curve** (default): daytime pitch follows Dawn -> Day -> Dusk, while
  **Night Sun Rotation** controls the night directly. Curve X is 0 at dusk (cycle
  0.75), 0.5 at midnight (cycle 0 / 1), and 1 at dawn (cycle 0.25). Y is pitch in
  degrees, without shortest-angle interpolation or wrapping. The default curve
  180 -> 90 -> 0 returns along the same upper arc. Match its endpoint orientations
  to Dusk/Dawn Sun Rotation for continuous transitions; angles may exceed 360.
- **Return Along Day Arc**: Dawn -> Day -> Dusk during the day, then
  Dusk -> Day -> Dawn during the night. It samples the exact same arc backwards,
  independently of the Night rotation value. Night colors, light intensity,
  exposure and fog still interpolate through the Night settings normally.
- **Full Orbit**: retains the original shortest-angle interpolation between all
  four rotation values, including Night. This supports a complete orbit below the
  horizon with the default -90 / 0 / 90 / 180 pitches.

**Hide Sun Disk At Night** hides only the skybox's disk and halo outside 0.25..0.75.
**Sun Disk Fade** controls their transition duration inside dawn/dusk (fraction of
the full cycle). The shader's independent **Sun Disk Visibility** parameter handles
this appearance; no light or GameObject is disabled or replaced. The same assigned
Directional Light and `RenderSettings.sun` remain in use. Night light intensity
defaults to 0.2; keep phase intensities positive when other systems require a
contributing main light. The Demo controller uses Night Curve, initialized from
its previous return path, and preserves
its authored colors, intensities, fog, exposure, rotations and clock settings.

**Preview In Editor** applies the current **Time Of Day** without entering Play
Mode. Change the slider or any lighting set to refresh the active scene. Preview
is restricted to the active regular scene and does not run in Prefab Mode. Enable
Scene View lighting and effects to see the lighting/post-processing there; cameras
need post processing enabled for Post Exposure. Time advances automatically only in
Play Mode, controlled by **Animate** and **Cycle Duration** (seconds per full day).
Animation respects game pause/time scale.

The controller uses temporary copies of the material and Volume profile, preserving
other overrides and any pre-existing instantiated profile. Disable the component
or turn editor preview off to restore the previous lighting and profile. Preview is
released before scene saving, script reloads, and Play Mode transitions, then rebuilt
when appropriate; temporary references are not saved into the scene. Editing phase
settings changes the controller's scene data, not the source material/profile assets.

The Demo controller has been converted from its earlier curves/gradients, sampling
its four phase values and retaining references and clock settings. Night defaults
provide visible blue ambient light and readable cloud facets. Exposure and fog also
change through night, with matching values at the midnight boundary. Nonnegative
light/sky brightness and the fog package's physical limits (density 0..1, attenuation
distance >= 0.05) apply; Post Exposure allows negative EV values.

Use one active controller. Missing Volume effects are skipped (with a warning in
Play Mode); the fog renderer feature must be enabled to see volumetric fog. This is
visual animation only: it does not drive quests, save game time, or refresh baked
lighting/reflection probes.

Assign the material in **Window > Rendering > Lighting > Environment > Skybox Material**.
The camera's **Background Type** must be **Skybox**. The material is supplied separately;
creating it does not change the scene, lighting settings, or the active skybox.

Clouds are two procedural 2D layers projected onto a curved sky dome. A triangular
noise lattice produces straight contour segments and flat shaded facets without
textures, cloud meshes, depth sampling, or ray marching. Analytic noise gradients
provide fake surface normals for directional sun lighting, with a sunlit rim near
the silhouette. Both layers cover the sun; the near layer covers the distant one.
Clouds fade into a clear band near the horizon. The sky does not move with camera
position, and the layers remain infinitely distant rather than physical volumes.

- **Projection Blend (Flat To Dome)**: 0 uses the flat projection, 1 uses the curved
  dome, and intermediate values smoothly morph the cloud coordinates for both layers.
  The default is 1. Facet orientation follows the transition; apparent thickness,
  wind stretch, and movement remain independent. No extra noise evaluations are needed.
- **Dome Curvature**: controls the curved end of the projection blend, reducing
  the flat ceiling appearance and horizon compression.
  Higher values spread the pattern more evenly between the zenith and horizon.
- **Apparent Thickness / Sun Lighting**: adjust pseudo-volume and the influence of
  sun direction on the facets. Thickness changes the shading, not the silhouette.
- **Sunlit Edges**: adds a rim around cloud edges close to the sun.
- **Lit Facets / Shaded Facets / Facet Contrast**: shared palette and facet variation.
  `DayNightCycle` continues to animate the palette for both layers.
- Each layer has its own **Coverage**, **Cloud Frequency**, **Opacity**, **Wind
  Direction**, **Wind Speed**, **Stretch Along Wind**, and **Pattern Offset (XY)**.
  Coverage 0 removes that layer; 1 covers the upper sky (subject to opacity and
  horizon fade). Set both layers' coverage or opacity to 0 for a clear sky.
- **Cloud Frequency**: lower values make larger clouds across both axes.
- **Stretch Along Wind**: 1 keeps equal scale along/across the wind, values above 1
  lengthen clouds along the wind, and values below 1 compress them. Crosswind scale
  and travel speed are preserved. This also works with wind speed set to zero.
- **Shape Detail**: extra silhouette detail for the near layer. The distant layer
  uses one noise octave; near detail adds a second only when this value is positive.
- **Wind Speed**: zero freezes that layer's pattern.
- **Distance Haze**: blends the distant layer toward the sky color, separating the
  layers without introducing another color palette to the day/night controller.
- Distant-layer opacity 0 skips that layer's noise and lighting work. With both
  layers enabled, there are two noise evaluations per pixel, or three when near
  detail is enabled. Fake lighting reuses their gradients rather than sampling again.
- **Follow Main Directional Light**: aligns the disk with URP's main directional light.
  Assign the intended Directional Light as **Sun Source** in Lighting settings.
  Without a main directional light, the manual angles are used.
- **Manual Azimuth / Manual Elevation**: place the sun with light following disabled;
  azimuth 0 points along +Z, 90 along +X. The sun fades below the horizon.
- **Sun Radius** is angular radius, not diameter. Sun intensity and halo are independent
  of light intensity; the skybox does not create or control a scene light.
- **Exposure** scales the complete sky, including clouds and the HDR sun.

This is an infinitely distant sky background, not local cloud geometry: it has no
parallax or cloud shadows on the world. Animated sky pixels do not automatically
refresh baked ambient lighting or reflection probes.
