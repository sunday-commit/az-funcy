# Records the README screencast from demo mode (canned data, no Azure access needed).
#
#   dotnet pack src/Funcy.Console -c Release -o ./nupkg
#   dotnet tool update --global --add-source ./nupkg az-funcy
#   vhs docs/demo.tape
#   docs/annotate-demo.sh          # burns in the captions and writes the GIF
#
# Requires vhs (charmbracelet/vhs), ttyd and ffmpeg. See docs/DEMO.md.
#
# This tape produces an UNCAPTIONED recording. annotate-demo.sh adds the captions and produces the
# GIF the README embeds, driven by docs/demo-scenes.txt. The two are kept in step by the timeline
# comments below: each one is the video timestamp where that step begins, and the scene file uses
# the same numbers. Change a Sleep here and the captions after it shift, so re-check the scene file.
#
# No LoopOffset: it rotates the whole recording, which would put every caption on the wrong scene.
# The captions make a title frame unnecessary anyway.

Output docs/assets/demo-raw.mp4

Require funcy

Set Shell "bash"
Set FontFamily "JetBrains Mono"
# 1600x560 at font size 13 gives just over 186 columns, which is what the layout needs before it
# starts squeezing the top panel's subscription column, and enough rows for the tallest panel (the
# 15-row log view). The captions are drawn in the empty band below the panels.
Set FontSize 13
Set Width 1600
Set Height 560
Set Theme "Catppuccin Mocha"
Set Framerate 20
Set TypingSpeed 60ms

# Bare prompt: the real one carries a user, host and path that have no business being in a GIF.
# Hidden, so recording starts at the first visible frame and t=0 is the command being typed.
Hide
Type "PS1='$ ' && clear" Enter
Show

# t=0.0
Type "funcy --demo" Enter
Sleep 6s

# t=6.9 splash waits for a key
Enter
# t=7.0 the list fills in: inventory, then details, then the Service Bus counts
Sleep 15s

# t=22.0 filter down to the orders flow
Type "f"
Type "orders"
Sleep 2s
Enter
Sleep 5s

# t=29.5 into the receiver's functions
Enter
Sleep 8s

# t=37.6 Application Insights logs for the selected function
Enter
Sleep 9s

# t=46.7 widen the time range from one hour to six
Type "."
Sleep 6s

# t=52.8 back to the functions list, then out to the app list
Escape
Sleep 2s
Escape
Sleep 3s

# t=58.0 environment variables
Type "v"
Sleep 6s

# t=64.1 move down to a Key Vault reference and reveal it
Down
Down
Down
Down
Sleep 2s
Type "m"
Sleep 6s
Escape
Sleep 2s

# t=74.7 the issues panel: the app whose namespace is not readable
Type "i"
Sleep 6s
Escape
Sleep 2s

# t=82.9 every subscription, including the empty one hidden by default
Type "u"
Sleep 4s
Type "h"
Sleep 5s
Escape
Sleep 2s

# t=94.2 Delete clears the active filter; F alone would reopen search with the old text in it
Delete
Sleep 2s

# t=96.3 start the stopped app and let the status column run through Starting -> Success
Type "f"
Type "calculator"
Enter
Sleep 3s
Type "s"
Sleep 8s

Sleep 2s
