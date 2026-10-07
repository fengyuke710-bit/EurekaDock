# EurekaDock checkpoint — 2026-10-07

## Latest update: live music during unified reveal

- The fixed popup combines a frozen Dock with a live VisualBrush of the selected music visual. A single transform scales both; HWND positions and layout remain fixed.
- Loopback capture starts as soon as the reveal is visible. Bars and the linear wave respond during the popup; the audio worker continues through the final handoff.
- Artwork visibility is primed before animation. Metadata updates and other-audio cover transitions wait until the reveal completes, preventing layout shifts or stale-cover flashes.
- Returning to fullscreen or cancelling a reveal immediately closes capture and releases the live brush references. Suspended resource behavior from the preceding checkpoint is preserved.
- Version backups now use Git, per user preference. The preceding working version is tagged before-live-reveal; no additional old runtime/configuration copies are created.

Validation: Release build has zero warnings/errors. All 118 relevant checks passed: 56 lifecycle/reveal, 31 bar visualization and 31 music checks. New tests play a real loopback tone, confirm bar and linear-wave movement before popup completion, verify changing composite pixels, and check fixed bounds, immediate artwork, waveform-only layout, handoff and rapid cancellation. Running runtime and portable ZIP are updated; personal configuration is unchanged.

## Completed

- Suspension detects maximized, fullscreen and work-area-sized apps behind small foreground windows. Utility windows no longer accidentally restart the Dock, audio capture and animation workers. Minimized, cloaked, passive and desktop/system windows are excluded.
- Starting while covered also reclaims startup working-set pages once. Collection and trimming happen on suspension, never periodically.
- Bar visualization retains its drawing graph, artwork brushes, geometry and text layout. Animation updates properties instead of recreating objects each frame. Cached text still follows wave colors.
- Native release builds enable compiler optimization; FFT Hann coefficients are precomputed. Frequency coverage, volume response and frame rate are preserved. No artificial frame-rate cap was introduced.
- Suspension settings, elastic reveal, immediate artwork and waveform-only reveal are preserved. Current runtime and initialized share ZIP are synchronized; prior versions and personal configuration are backed up outside source control. Personal application lists are preserved.

## Validation

Release build: zero warnings/errors. All 159 checks passed: 52 lifecycle, 31 bar visualization, 31 audio/music and 45 core checks. Coverage tests include small overlays, custom work-area windows, minimized recovery, covered startup, one-time reclamation and reveal cancellation. Audio tests include real loopback playback; drawing tests verify changed text colors.

Short samples on the user's machine measured active CPU at approximately 2.58% before and 1.75% after. A small utility over a maximized app measured approximately 0.13% CPU and 11.2 MB total working set. Isolated sustained suspension measured approximately 0–0.02% CPU. Audio content and sampling intervals affect these results; they are measurements, not fixed guarantees.

Controlled visualization samples near 180 Hz measured CPU changing from 4.62% to 3.28–3.62%, allocations from 11.36 to approximately 2.5 MB/s, and native spectrum processing from approximately 0.576 to 0.031 ms per call.

## Limits and next step

The tray, coverage detector and CLR remain alive during suspension. Private committed memory differs from resident working set; Task Manager can use different memory accounting. Active high-refresh-rate WPF composition still consumes resources. Further tuning should use measured hot paths, rather than periodic collection or working-set trimming.

Await user visual and resource-usage feedback. No further functional changes are pending in this checkpoint.
