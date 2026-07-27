# Demo mode and the README screencast

`funcy --demo` runs the whole UI against a fabricated estate. Nothing reaches Azure: the services
that normally call ARM, Resource Graph, Application Insights, Service Bus and Key Vault are replaced
with canned ones, and no credential, ARM client or `az` runner is even registered. A path that is not
covered fails as a missing dependency rather than quietly reaching a real tenant.

## Rules the mode follows

- **Explicit only.** Nothing but the exact `--demo` argument enables it. It is never a fallback for a
  failed Azure call, so a broken tenant still shows an error rather than plausible-looking numbers.
- **Always labelled.** The top panel carries a `DEMO DATA - not a real tenant` header for as long as
  the mode is active. It sits on the panel border, not the status line, so a status message cannot
  push it out of frame.
- **Isolated on disk.** The database, `settings.json` and logs go to a `demo` sub-directory of the
  normal data directory (`~/.local/share/funcy/demo` on Linux). A demo run never mutates the real
  inventory cache or your settings.

Delete `<data directory>/demo` to reset the demo to its initial state.

## What the estate contains

Composed so that every panel has something worth looking at, and so the states that matter are all
reachable in one walkthrough:

- three subscriptions, one of them with no function apps at all, so "hide empty" has an effect
- around a dozen apps across several resource groups, tagged so the tag column has content
- Service Bus, timer, HTTP and blob triggers, including a disabled function
- dead letters on two apps, and one app whose counts deliberately fail to resolve
- a staging slot, a pinned app and a stopped app
- application settings including two Key Vault references that resolve on reveal
- Application Insights rows covering traces, requests and an exception

The numbers are in `src/Funcy.Demo/DemoDataset.cs`. The demo services pace their answers with short
artificial delays (`DemoLatency`), which is what makes the spinner, the progress counter and the
progressive fill visible in a recording.

## Recording the screencast

The recording is scripted with [VHS](https://github.com/charmbracelet/vhs), which replays a tape in
its own terminal and renders the result. That makes it deterministic and re-runnable: no need to hit
the right keys at the right moment, and a change means editing the tape and running it again.

Prerequisites: `vhs`, `ttyd` and `ffmpeg`.

```bash
dotnet pack src/Funcy.Console -c Release -o ./nupkg
dotnet tool update --global --add-source ./nupkg az-funcy
vhs docs/demo.tape          # records docs/assets/demo-raw.mp4
docs/annotate-demo.sh       # writes docs/assets/demo.mp4 and docs/assets/demo.gif
```

Two steps, because VHS cannot draw text over a full-screen TUI. The tape records the session and
`annotate-demo.sh` burns in the captions from `docs/demo-scenes.txt`, then encodes the GIF the README
embeds. `demo-raw.mp4` is an intermediate and is not committed; the captioned MP4 is what to post to
LinkedIn or Reddit.

### Changing the captions or the pacing

The captions live in `docs/demo-scenes.txt` as `start|end|text`, where the times are timestamps in
the raw recording. `demo.tape` carries a `# t=` comment at each step with the same numbers, so the
two files can be kept in step by hand.

Changing a `Sleep` shifts every caption after it. When you do, re-record, then check that the
captions still land on the right scenes before encoding. A quick way is to pull one frame per scene
midpoint and look at the panel title:

```bash
ffmpeg -ss 42 -i docs/assets/demo-raw.mp4 -frames:v 1 -y /tmp/f.png
```

`annotate-demo.sh` refuses to run on a caption wider than the frame, since `drawtext` would silently
clip it at both ends instead of complaining.

### Notes

- **Fonts come from the recording machine.** The tape asks for JetBrains Mono. az-funcy only uses
  ordinary Unicode glyphs, so no Nerd Font is needed, but the font does need to cover Braille
  (`⠋`, the spinner) and box drawing.
- **The prompt leaks.** The tape resets `PS1` behind `Hide` for that reason. Check the first frame of
  the finished file anyway.
- **Mouse input does not exist.** VHS only feeds keystrokes.
- **Do not set `LoopOffset`.** It opens the GIF on a later frame, which is tempting for a thumbnail,
  but it does that by rotating the whole recording, and every caption would then sit on the wrong
  scene. The first caption serves as the title frame instead.
- **Escape sequences must reach the app whole.** Driving the TUI from a script rather than VHS (a
  bare pty, for instance) means writing `\e[B` in one call: split across writes, `Console.ReadKey`
  sees a lone Escape and pops the panel. The same rig also has to answer the cursor-position report
  (`\e[6n`) that `Console.CursorTop` sends, or the app blocks after its first keystroke.
- Keep the GIF under 5 MB. If it will not fit, drop the frame rate before the width in
  `annotate-demo.sh`: text survives fewer frames better than it survives downscaling.

GIF renders from a relative path in a README; MP4 does not. Use `demo.mp4` for LinkedIn and for a
Reddit post instead.
