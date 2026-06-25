# ExanimapHelper

ExanimapHelper is a Windows tool for recording player movement in the game
**Exanima**. It reads the player's X/Y coordinates from the game's memory and
plots the path on a coordinate system.

> 📖 **Not a programmer?**
> Read the [simplified guide](docs/SIMPLE_README.md) instead.

## Getting started
### Preamble
Since this tool needs to be able to read the memory data of another program (Exanima), it must register itself as a program with administrative rights. This will trigger the Windows defender and might also trip some anti-virus scanners.

### Finding the correct addresses
Exanimas memory allocation is non-static, therefore the tool cannot query the memory addresses for the X or Y position by itself.<br>
To figure out the correct memory addresses, I recommend using [Cheatengine](https://www.cheatengine.org/).<br>

For more help on how to find the correct variables using cheatengine, refer to [this document](docs/CHEAT_ENGINE_INSTRUCTIONS.md)

### Usage
The usage is pretty straight forward:
1) Start Exanima
2) Link cheatengine to exanima and find the memory addresses for both X & Y axis
3) Copy the addresses into the tool
4) Enter the game
5) Press F8 to start
6) Walk around as you please
7) Press F8 to stop
8) View the graph or even export it as a png
