# ConsoleForge

Universal video converter for PS3, PSP and PS Vita. Takes any source file and transcodes it
to the exact format each console plays natively.

## Requirements

- .NET 8 SDK
- `ffmpeg` and `ffprobe` on `PATH` (or explicit paths in the config file)

## Getting the source

```bash
git clone https://github.com/jotadev27/ConsoleForge.git
cd ConsoleForge
```

## Build and run

```bash
dotnet build
dotnet run --project ConsoleForge.UI
```

## Projects

| Project | Contents |
| --- | --- |
| `ConsoleForge.Core` | Device profiles, conversion planning, resolution/bitrate rules, service interfaces. No I/O. |
| `ConsoleForge.Infrastructure` | FFmpeg/ffprobe process wrappers, progress parsing, config and folder scanning. |
| `ConsoleForge.UI` | Avalonia desktop app (MVVM), square-cornered theme. |
| `ConsoleForge.Tests` | xUnit tests for profile constraints and resolution alignment. |

Run the tests with `dotnet test`.

## Device profiles

Defaults always target the highest quality each console plays reliably.

| | PS3 | PSP | PS Vita |
| --- | --- | --- | --- |
| Max resolution | 1920x1080 | 480x272 | 960x544 (1280x720 advanced) |
| H.264 | High L4.1 | **Baseline L3.0**, no B-frames, 1 ref | High L3.1 |
| Video bitrate | 16 Mbps | **1.5 Mbps** | 6 Mbps |
| Audio | AAC-LC 192k @ 48 kHz | **AAC-LC 128k @ 44.1 kHz** | AAC-LC 192k @ 48 kHz |
| Dimension alignment | 2 px | **16 px** | **16 px** |
| Frame rate cap | 60 | 30 | 30 |
| Default encoder | NVENC if present | **always libx264** | NVENC if present |
| Cover thumbnail | — | **160x120 `.THM`** | — |
| Output folder | `VIDEO/` | `VIDEO/` | `PS Vita/VIDEO/` |

The PSP profile pins libx264 through `PreferredEncoder`: NVENC's rate control is tuned for
higher bitrates and loses detail at 480x272 / 2.5 Mbps. The pin only sets the default — NVENC
stays selectable for PSP in the encoder dropdown. PS3 and Vita pick NVENC automatically
whenever it is detected. Selecting a device always resets the encoder to that device's default.

PSP values are tuned for what the Media Engine decodes smoothly rather than for the format's
theoretical ceiling: 1.5 Mbps video and 128 kbps audio at 44.1 kHz, since higher rates stutter
on real hardware. Audio is always re-encoded (never stream-copied), so the payload sample rate
always matches what the container declares.

PSP and Vita decoders expect dimensions aligned to 16 px macroblocks, so the fitted resolution
is rounded to the nearest multiple of 16 after the aspect ratio calculation. A small aspect
deviation is accepted in exchange for playback: a 16:9 source targeting PSP becomes 480x272
rather than the geometrically exact 480x270. PS3 stays on 2 px alignment — its own 1080p
ceiling is not a multiple of 16.

Sources at or below the device ceiling keep their native resolution (no upscaling unless
requested in advanced mode). Larger sources are scaled to fit the ceiling with aspect ratio
preserved, then aligned.

Bitrate scales with output pixel count so a small source does not get a 1080p bitrate, but it
never exceeds the profile ceiling and never drops below the profile floor at native resolution.

## Cover thumbnails

The PSP profile writes a 160x120 `.THM` JPEG next to each converted file, which the XMB shows
as the video's cover. By default the frame is taken from 10% into the source. Clicking the
thumbnail in the queue's THUMB column opens a picker to use your own image instead; it is
letterboxed to 160x120 (never stretched) and replaces the auto frame for that file only. Each
queue row keeps its own cover. PS3 and Vita do not use this mechanism and show a placeholder.

Previews are generated lazily: a row's thumbnail is extracted only when the row scrolls into
view, cached in memory for that row, and reused on scroll-back. Enqueueing a large batch costs
no ffmpeg calls at all. Assigning a custom cover generates immediately, on or off screen.

## Queue row controls

Each row carries a square stop button (red) and, while encoding, a pause button; paused rows
show play instead. Cancelling asks for confirmation first: an encoding file has its ffmpeg
process killed and its partial output deleted, a queued file is simply removed. Pausing stops
the current file, marks it PAUSED, keeps its queue position and lets the next file start —
**resuming re-encodes from the beginning**, since a partial H.264 encode cannot be continued.

Double-clicking a name in the FILE column edits the **output** name inline. Enter commits,
Escape or focus loss reverts, invalid characters are rejected, and existing files are never
overwritten (`name_2.mp4`). The source file is never renamed.

Closing the window while a conversion is running asks for confirmation; confirming stops the
encode and deletes the partial file. With nothing running it closes immediately.

## Window chrome

The window is fully client-decorated: `SystemDecorations="None"` plus
`ExtendClientAreaToDecorationsHint` and `ExtendClientAreaChromeHints="NoChrome"`. The WM draws
nothing, so the app's own title bar is the only one, and it carries **minimise and close only** —
there is no maximise control anywhere. Dragging it moves the window (`BeginMoveDrag`);
double-clicking it is swallowed so it cannot maximise, and any attempt to enter `Maximized` or
`FullScreen` is reverted in `OnPropertyChanged`.

Because `NoChrome` also removes the WM's resize border, resizing is reimplemented: an overlay
grid lays 5 px transparent hitboxes over the four edges and four corners, each tagged with its
`WindowEdge` and calling `BeginResizeDrag` on left-press.

**History.** Two earlier attempts failed on Fedora + GNOME. `NoChrome` alone was not honoured —
Mutter kept its own title bar, producing two stacked bars. Reverting to native chrome fixed the
duplication but left a working maximise button that had to be neutralised in code.
`SystemDecorations="None"` is what actually removes Mutter's decorations, confirmed by the X11
window frame measuring exactly the client size (1440x860) and `_MOTIF_WM_HINTS` reporting
`decorations = 0`.

**Platform note.** This is verified on Fedora/GNOME (Wayland session via XWayland). On Windows,
`SystemDecorations="None"` likewise removes the native frame, so the same custom bar and manual
grips apply; snap gestures that rely on the maximised state (Aero Snap to the top edge, `Win`+`Up`)
stay blocked by the state guard. If manual resizing ever misbehaves on a given WM, switching
`SystemDecorations` back to `Full` restores native borders at the cost of showing the native
maximise button again.

## Output layout

Files are written to `<output root>/<device subfolder>/<sanitized name>.mp4`. Point the output
root at the memory stick / USB drive root and the structure matches what each console expects.
Names are sanitized to ASCII and length-capped per device; existing files are never overwritten
(a `_2`, `_3` … suffix is added).

## Configuration

Stored as JSON at `%APPDATA%/ConsoleForge/config.json` (Windows) or
`~/.config/consoleforge/config.json` (Linux/macOS). `FFmpegPath` and `FFprobePath` can be set
there if the binaries are not on `PATH`.
