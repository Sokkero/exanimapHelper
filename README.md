# ExanimapHelper

ExanimapHelper is a Windows tool for recording player movement in the game
**Exanima**. It reads the player's X/Y coordinates from the game's memory and
plots the path on a coordinate system.

> 📖 **Not a programmer?**
> Read the [simplified guide](docs/SIMPLE_README.md) instead.

## Getting started
### Preamble
Since this tool needs to be able to read the memory data of another program (Exanima), it must request administrative rights when it starts. This will trigger Windows Defender and might also trip some anti-virus scanners.

### Addresses
The tool ships with Exanima's default coordinate addresses (`Exanima.exe+48DDD0` for X and `Exanima.exe+48DDD8` for Y) pre-filled, so in most cases it works out of the box — just start the game and the tool. These are module-relative offsets, so they keep working across game restarts even though Exanima loads at a different base address every launch.

If the defaults don't work for your version (e.g. after a game update moves the data), you'll need to find the addresses yourself with [Cheat Engine](https://www.cheatengine.org/) and paste them into the tool. For how to do that, refer to [this document](docs/CHEAT_ENGINE_INSTRUCTIONS.md).

> ⚠️ When finding addresses manually, only the **green** (persistent) ones survive a game restart — see the Cheat Engine instructions. The built-in defaults are persistent, so you don't have to worry about this unless you fall back to manual addresses.

### Usage
The usage is straightforward:
1) Start Exanima
2) Start the tool (the default addresses are already filled in)
3) Enter the game
4) Press F8 to start
5) Walk around as you please
6) Press F8 to stop
7) View the graph or even export it as a png

If the live readout shows no or obviously-wrong values, the defaults didn't work for your version — find the addresses with Cheat Engine (see above) and paste them in before step 4.
