# Standalone Mac overlay

Open **STS2 Damage Overlay.app** before or during a battle. On first launch,
allow **STS2 Damage Overlay** under **System Settings → Privacy & Security →
Screen & System Audio Recording**. Reopen the overlay if macOS asks. The app
captures only the Slay the Spire 2 window and processes frames locally in memory.

Hold **Control (⌃)** and drag anywhere on the panel. Release Control to save its
position and resume click-through. You can also choose **Move overlay** from the
shield menu, drag the panel, then choose **Lock position**. During play the panel passes
clicks through to the game. **Hide overlay** keeps the reader running. **Quit**
closes the app. The app waits for the next game session if STS2 exits.

## Build on your Mac

Requires macOS 14 or newer, the Swift compiler from Xcode or Command Line Tools,
Python 3.14 or newer with the standard-library Zstandard module, and the Steam
version of Slay the Spire 2. From the repository root, run

```sh
./macOS/build.sh
open "artifacts/macOS/STS2 Damage Overlay.app"
```

For a different Steam library, pass the path to `Slay the Spire 2.pck` as the
first build argument. The build reads the installed game’s attack icons, shield,
and number font to generate local recognition references. The resulting app
runs on the architecture it was built for and includes those references. Rebuild
if a game update changes its artwork.

## Reading damage

The app finds attack icons, reads the number beside each icon and sums the
visible attacks. A label such as `3x4` contributes 12. If a single player HP
bar and its block state are readable, the panel also displays the estimated
damage after block. **LETHAL** marks an estimate at or above current HP.

Recognition depends on the standard combat HUD. Tooltips, animations, unusual
UI scales, modded artwork, or obscured enemies can hide attacks and cause an
undercount. When no attack is recognized, or a detected icon lacks a readable
label, the panel clears. The shield menu shows the waiting status.
After-block estimates use simple subtraction. Multiplayer targeting, Osty,
potions, and per-hit mitigation are outside this reader’s coverage.

## Verification

Tested against an active Fool battle on Apple silicon, STS2 v0.113.1. The live
reader found the Sludge Spinner’s 8-damage intent and reported 8 after block.

To check one live frame or a saved game-window image, run

```sh
overlay="artifacts/macOS/STS2 Damage Overlay.app/Contents/MacOS/STS2DamageOverlay"
"$overlay" --probe
"$overlay" --image /path/to/game-window.png
```

Both commands print the reading. Exit status 0 means damage was recognized,
2 means no readable attack, and 1 indicates an error. To test the image reader
with generated frames after building, run

```sh
xcrun swiftc -O macOS/Recognition.swift macOS/Tests/main.swift -o artifacts/macOS/recognition-tests
artifacts/macOS/recognition-tests "artifacts/macOS/STS2 Damage Overlay.app/Contents/Resources"
```
