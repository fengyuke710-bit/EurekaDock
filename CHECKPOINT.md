# EurekaDock checkpoint — 2026-10-07

## Completed

- General has a bilingual, immediately saved "Pause desktop widgets in fullscreen
  or maximized apps" switch. Default is on, including existing configurations.
  Turning it off bypasses both window destruction and the separate music pause.
  Tray hide and explicit Dock/visualizer visibility controls remain independent.

- Reveal pause is idempotent. Restoring a fractional carousel position no longer
  silently changes its target to a neighboring slot before the live handoff.
- The frozen composite uses actual Win32 client bounds and whole-pixel margins.
  Its bitmap sizes and placement match the live windows. The spring's remaining
  scale error settles smoothly to exactly 1 before the handoff at 0.75 seconds.
- A small frozen track/artwork DTO survives desktop-window destruction. Album
  artwork and text participate in the unified popup immediately. Audio capture,
  metadata polling, record rotation, hover and carousel workers start afterwards.
- A previously suppressed album returns directly in the expanded waveform-only
  layout. No album or title briefly appears and disappears during the popup.
- Fullscreen/maximized coverage still destroys the rendered windows. The retained
  DTO does not retain their object graph. Tray, settings IPC and coverage detection
  remain available while desktop windows are closed.
- Running version and initialized portable ZIP were updated. Personal settings,
  applications and icons were preserved; the preceding runtime and ZIP are backed
  up outside source control. Private GitHub synchronization remains configured.

## Validation

Release build: zero warnings/errors. 46 lifecycle checks and 8 default-configuration checks
passed. Verification includes real maximized and borderless foreground windows,
exact fractional carousel restoration, physical-pixel snapshot alignment, album
pixels present in the reveal, no stale album pixels in waveform-only mode, idle
workers during popup, collectible closed windows and rapid cancellation.

The installed and packaged DLLs match the tested build. The live application
restarted successfully, attached to the desktop, loaded all 20 configured items,
and preserved the user's config hash. The share ZIP contains the portable runtime
and no personal configuration, logs or confusing root ICO file.

## Limits and next step

The popup intentionally displays the last visible track state without polling in
the background; live media metadata refreshes after the popup. Display refresh
and Windows composition still determine perceptual smoothness. Await user visual
feedback; no further functional changes are pending.
