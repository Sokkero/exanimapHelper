# ExanimapHelper

A Windows desktop tool that records a player's movement through the game
**Exanima** by reading the player's X/Y coordinates from the game's process memory,
then plots the path on a coordinate system. Built to help the community trace level
layouts and reconstruct maps.

## What it does

- Reads two memory addresses (player X and Y) as 32-bit floats from `Exanima.exe`.
- Polls them on a configurable interval (default 1s) while recording.
- Lists every recorded `(x, y)` pair and draws the path as a connected trail.
- Exports the recorded coordinates to a text file and the graph to a PNG.

## How to use

1. Find your X and Y coordinate addresses in Exanima (e.g. with Cheat Engine).
2. Paste them into the X-Axis and Y-Axis address fields.
3. (Optional) Adjust the polling interval.
4. Press **Start**, walk through the level, press **Stop**.
5. Export the data and/or the graph.

> **Note:** Memory addresses change each time Exanima restarts (ASLR), so you must
> re-find them each session in v1.

## Tech

- C# / WPF, .NET, x64 — **Windows only**.
- Memory access via Win32 `OpenProcess` / `ReadProcessMemory`.

## Status

Early development. See [PROJECT.md](PROJECT.md) for the full plan and
[TODO.md](TODO.md) for the step-by-step implementation roadmap.
