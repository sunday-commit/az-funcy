#!/usr/bin/env bash
# Burn the captions from demo-scenes.txt into the raw recording, then produce the README GIF.
#
#   vhs docs/demo.tape          # writes docs/assets/demo-raw.mp4
#   docs/annotate-demo.sh       # writes docs/assets/demo.mp4 and docs/assets/demo.gif
#
# VHS cannot draw text over a full-screen TUI, so the captions are a post-processing step. They go in
# the empty band below the panels, which is why the tape records a viewport taller than the UI needs.
#
# Requires ffmpeg. Fonts come from this machine; the font file is resolved via fc-match.
set -euo pipefail

docs="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
assets="$docs/assets"
raw="$assets/demo-raw.mp4"
captioned="$assets/demo.mp4"
gif="$assets/demo.gif"
scenes="$docs/demo-scenes.txt"

[[ -f "$raw" ]] || { echo "Missing $raw. Run: vhs docs/demo.tape" >&2; exit 1; }
[[ -f "$scenes" ]] || { echo "Missing $scenes" >&2; exit 1; }

# Caption band: below the tallest panel (the 15-row log view ends around y=415 in a 560px viewport).
caption_y=478
font_size=22
font_file="$(fc-match -f '%{file}' 'JetBrains Mono' 2>/dev/null || true)"
[[ -n "$font_file" && -f "$font_file" ]] || font_file="$(fc-match -f '%{file}' 'monospace')"

# Each caption goes in its own file and is referenced with textfile=. drawtext's own parser treats
# : ' , and \ structurally, so passing captions inline would mean escaping them through both bash
# and ffmpeg — the reason an earlier version of this script broke on an apostrophe.
text_dir="$(mktemp -d)"
trap 'rm -rf "$text_dir"' EXIT

filters=()
scene_count=0
while IFS='|' read -r start end caption; do
    [[ -z "${start// /}" || "${start:0:1}" == "#" ]] && continue
    [[ -z "${caption// /}" ]] && continue

    # A caption wider than the frame is clipped at both ends, and drawtext says nothing about it.
    # JetBrains Mono advances 0.6em, so the frame holds (width - 2*boxborderw) / (0.6 * fontsize).
    max_chars=$(( (1600 - 2 * 14) / (font_size * 6 / 10) ))
    if (( ${#caption} > max_chars )); then
        echo "Caption at ${start}s is ${#caption} chars, over the ${max_chars} that fit. Shorten it:" >&2
        echo "  $caption" >&2
        exit 1
    fi

    caption_file="$text_dir/caption-$scene_count.txt"
    printf '%s' "$caption" > "$caption_file"

    filters+=("drawtext=fontfile=${font_file}\
:textfile=${caption_file}\
:fontsize=${font_size}\
:fontcolor=0xE6E6E6\
:x=(w-text_w)/2\
:y=${caption_y}\
:box=1:boxcolor=0x1E1E2E@0.92:boxborderw=14\
:enable=between(t\\,${start}\\,${end})")
    scene_count=$((scene_count + 1))
done < "$scenes"

[[ $scene_count -gt 0 ]] || { echo "No captions found in $scenes" >&2; exit 1; }

echo "Burning in $scene_count captions..."
filter_chain=$(IFS=,; echo "${filters[*]}")
ffmpeg -loglevel error -i "$raw" -vf "$filter_chain" -c:v libx264 -pix_fmt yuv420p -crf 20 -y "$captioned"

# Two passes with a per-file palette: the difference between a readable GIF and mush. Drop the frame
# rate before the width if it needs to be smaller — text survives fewer frames better than scaling.
echo "Encoding GIF..."
palette="$text_dir/palette.png"

ffmpeg -loglevel error -i "$captioned" \
    -vf "fps=12,scale=1200:-1:flags=lanczos,palettegen=stats_mode=diff" -y "$palette"
ffmpeg -loglevel error -i "$captioned" -i "$palette" \
    -lavfi "fps=12,scale=1200:-1:flags=lanczos,paletteuse=dither=bayer:bayer_scale=3" -y "$gif"

printf 'Wrote %s (%s) and %s (%s)\n' \
    "$captioned" "$(du -h "$captioned" | cut -f1)" \
    "$gif" "$(du -h "$gif" | cut -f1)"
