# EurekaDock checkpoint — 2026-10-21

## Latest fix: reflection edge fading

- Dock reflections now reuse the icon body's premultiplied pixel fading pipeline and the same viewport-anchored quintic alpha curve. The independent group opacity mask is removed. Left/right fading and the finite mode's progressively restored fade therefore stay consistent with the body.
- Reflection edge frames are cached per visible carousel slot, pruned when offscreen, and released on reload/suspension teardown. Icons outside the fade region retain the static cached reflection path; idle rendering still stops.
- Increased the size-aware icon/reflection gap slightly again, to 5–8 DIP, while retaining perspective, progressive vertical blur, the previous clarity adjustment and the icon/music baseline.
- Release build has zero warnings/errors. All 132 relevant checks passed: 31 icon-effect checks including actual rendered alpha comparisons, 22 carousel edge checks, 23 Quick Panel checks and 56 fullscreen/lifecycle checks.
- Pixel tests cover both infinite-carousel sides, subpixel movement, finite start/end without fading, and the finite transition back into edge fading. Center and edge reference pixels are sampled from the recorded drawing in local coordinates, excluding parent visual offsets. The central reference is checked for a solid body and visible reflection before ratios are compared.
- Running runtime, initialized share ZIP and public Git repository are synchronized with personal configuration preserved. Version checkpoints remain in Git.

## Earlier refinement

- Increased the icon/reflection gap from 2 DIP to a size-aware 3.5–6 DIP, leaving a slightly clearer separation at normal and large icon sizes.
- Reduced the cached Gaussian blur radii by approximately 22–24%, retaining progressive downward blur, perspective and the existing opacity fade.
- Release build has zero warnings/errors. All 46 relevant checks passed (23 icon effects, 23 Quick Panel), including the maximum-size/hover viewport bounds, color alpha, physical baseline and idle rendering. Updated Dock and Quick Panel captures were visually inspected.
- Existing personal effect selections and app configuration are preserved. The running runtime, portable share ZIP and public Git repository are synchronized; version backups remain in Git.

## Completed

- Added Settings → App Icons between Dock and Apps. Click feedback and resting appearance are independent, immediately saved selectors. Both use Default for new and existing configurations, preserving the current look and interaction.
- Bounce (macOS style) plays three diminishing upward hops after a confirmed launch click. Dragging and context menus do not trigger it. Pulse state expires and rendering stops at the resting position; suspension cancels it immediately.
- Mirror reflection uses a cached premultiplied bitmap with perspective widening towards the viewer, progressively increasing blur, and a downward opacity fade. It adds no background tray. Dock reflections follow the carousel's edge mask and existing hover size.
- Both effects apply to Dock and Quick Panel app icons. Quick Panel launches still close the panel automatically. Effect insets are inside its scroll viewport to prevent clipping at large icon/hover settings.
- DPI-aware physical-pixel padding preserves the Dock icon baseline, music anchor and left controls while providing room for bounce and reflection. Music-only mode does not reserve unnecessary Dock-effect space.
- Added bilingual setting descriptions, an in-app tutorial, README instructions and a version-controlled portable-package manual. The package also includes EurekaDock's MIT license separately from the Microsoft runtime license.
- Existing full-width Bar Wave defaults, paused-cover retention until player/session closure, live visualizer popup and fullscreen suspension behavior are preserved. No personal configuration migration is required.

## Validation

The original feature checkpoint passed all 214 related checks; the latest parameter-only refinement is validated separately above:

- 23 icon-effect checks: legacy/default configuration, fallback values, persistence, actual GUI selectors, cached reflection, color alpha, perspective, progressive blur, physical baseline, bounded bounce, idle stop, suspension and actual Quick Panel target launch.
- 22 finite-carousel edge checks; 23 Quick Panel checks; 37 bilingual/settings checks across nine pages.
- 56 fullscreen/lifecycle checks, 8 default-configuration checks and 45 core checks.

Visual QA includes Dock reflection and bounce, both settings languages, Quick Panel reflection and 128-DIP icons at 1.5× hover. The latter is checked against the real scroll viewport, including both reflection edges and the top of the enlarged icon.

## Deployment and limits

The running runtime and initialized portable ZIP are synchronized after validation; installed and archived application files match the tested build. Personal configuration and application lists remain unchanged. Git stores the preceding version and the new checkpoint; source and documentation are synchronized to the existing public MIT-licensed GitHub repository.

Reflection pixels are prepared only when icon resources/effect settings change. Idle icons have no animation timer; music visualization continues to use its separate existing workers. Quick Panel launch feedback runs during the panel's normal closing animation rather than delaying target launch or panel dismissal.
