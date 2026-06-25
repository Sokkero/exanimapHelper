# ExanimapHelper

ExanimapHelper is a Windows tool for recording player movement in the game
**Exanima**. It reads the player's X/Y coordinates from the game's memory and
plots the path on a coordinate system.

> 📖 **Not a programmer?**
> Read the [simplified guide](docs/SIMPLE_README.md) instead.

## Getting started
### Preamble
Since this tool needs to be able to read the memory data of another program (Exanima), it must request administrative rights when it starts. This will trigger Windows Defender and might also trip some anti-virus scanners.

### Finding the correct addresses
Exanima's memory allocation is non-static, therefore the tool cannot query the memory addresses for the X or Y position by itself.<br>
To figure out the correct memory addresses, I recommend using [Cheat Engine](https://www.cheatengine.org/).<br>

For more help on how to find the correct variables using Cheat Engine, refer to [this document](docs/CHEAT_ENGINE_INSTRUCTIONS.md)

> ⚠️ Because the addresses are non-static, they change every time Exanima restarts. You will need to find them again after each restart of the game.

### Usage
The usage is straightforward:
1) Start Exanima
2) Link Cheat Engine to Exanima and find the memory addresses for both the X & Y axis
3) Copy the addresses into the tool
4) Enter the game
5) Press F8 to start
6) Walk around as you please
7) Press F8 to stop
8) View the graph or even export it as a png
