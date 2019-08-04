# ProjectPSX

![psx](https://user-images.githubusercontent.com/28767885/60985122-30e29900-a33d-11e9-8956-4b933a2745b4.PNG)

**ProjectPSX is a C# coded emulator of the original Sony Playstation (Playstation 1/PS1/PSX)**

*This is a personal project with the scope to learn about hardware and the development of emulators.*

ProjectPSX dosn't use any external dependency and uses rather simplistic C# code.

At the moment the following is implemented:
- CPU (MIPS R3000A) with the Coprocessor 0 and Geometry Transformation Engine (GTE) Coprocessor.
- A BUS to interconnect the components.
- Partial GPU (software polygonal rasterizer) with all the commands implemented with VRAM renderer.
- Partial CDROM: Implemented the common cd access commands.
- DMA transfers.
- Partial TIMERS.
- Digital controller support (currently hardcoded to keyboard). Lacks memory cards.
- A basic bios and mips disassembler.
- MDEC for video decoding 16bpp and 24bpp.

What is not implemented (but should be...):
- GPU Blending and transparency.
- A proper screen output that respects the hardware tv output sizes instead the current VRAM viewer.
- Proper 24bpp support (the current VRAM viewer only handles 16bpp).
- CDROM: Proper timmings and a .cue parser.
- TIMERS: dotclock, h and vsync cases.
- JOYPAD: Memory Card support.

> **Note:**  A valid PlayStation Bios is needed to run the emulator. SCPH1001.BIN is the default bios on the development but some others like SCPH5501 or SCPH7001 have been reported to work.

## Compability

The emulator is in very early development and there are constant rewrites. Somehow and although with some limitations there are some games that already "work" like:
Ridge Racer / Revolution, Rage Racer, Castlevania Symphony of the Night, Final Fantasy 7, Crash Bandicoot 1 and 2, Spyro the dragon, Tekken, Toshinden...
Some others like Time Crisis, Final Fantasy IX, Gran Turismo, PacMan World or Tobal 2 boot but have random problems to be fixed.

> **Note:**  Memory Card files are unsupported so any progress will be lost upon exit.


## Using the emulator

The emulator should work dragging a BIN iso or a PSX EXE to the GUI.
Yet this is still unimplented and the CD names are hardcoded as strings on the CD Class and the Bios on the Bus Class.
 
Once power on, Input is mapped as:

* D-Pad UP: **Up**
* D-Pad Left: **Left**
* D-Pad Down: **Down**
* D-Pad Right: **Right**
* Triangle: **W**
* Square: **A**
* X: **S**
* Circle: **D**
* Start: **Enter**
* Select: **Space**
* L1: **Q**
* R1: **E**
* L2: **1**
* R2: **2**

## Screenshots
![cpu](https://user-images.githubusercontent.com/28767885/60985112-304a0280-a33d-11e9-83b3-49a15fb1c117.PNG)
![gte](https://user-images.githubusercontent.com/28767885/60985120-30e29900-a33d-11e9-8cfa-1753b878e023.PNG)
![crash](https://user-images.githubusercontent.com/28767885/60985114-304a0280-a33d-11e9-80e2-08cd1c5abfbe.PNG)
![rr](https://user-images.githubusercontent.com/28767885/60985123-30e29900-a33d-11e9-9188-f942e44bcc3a.PNG)
![rrTranspBlend](https://user-images.githubusercontent.com/28767885/60985124-317b2f80-a33d-11e9-97b4-df9b50acd73e.PNG)
![ff7](https://user-images.githubusercontent.com/28767885/60985116-304a0280-a33d-11e9-9944-f0dfc4f085c3.PNG)
![ff7B](https://user-images.githubusercontent.com/28767885/60985118-304a0280-a33d-11e9-9170-af6902f8bd08.PNG)

## Quick Faq

- What's up with that window with all the weird colors and the double game screen?

The emulator is very early on development there is no real tv output "screen". What you see rendered is the Playstation whole VRAM. It includes all the textures and color lockup tables used by the games. Also almost all games on the PSX used double buffering, that's why there's 2 game screens on the VRAM as the hardware draws on one and outputs the other.

- Why "insert game here" doesn't work?

Probably due to not implemented hardware, mainly cdrom timmings. Per example Parasite Eve loads but main screen looks awful as 24bpp is not supported yet. Gran Turismo crash after some loading or main screen because CDROM timmings.

- How can i get console TTY or BIOS output?

Uncomment the bios.verbose() or TTY() functions on the CPU main loop. You can also dissasemble() the MIPS CPU instructions and printRegs() on the current opcode.

- Why there's green squares as textures on Ridge Racer or magic looks solid in Final Fantasy 7?

At the moment there's no texture blending or transparency support implemented.

- Why both RTPS and RTPT Geometry Transformation Engine Coprocessor commands give wrong values on tests?

At the moment i just use common division where the original PSX uses a fastest, but less accurate division mechanism (based on Unsigned Newton-Raphson (UNR) algorithm.

- Why MDEC looks awful on "insert game here"? 

Even if the emulator can actually decode 16 and 24bpp video the current window (VRAM viewer) only supports 16bpp.
If you want to check currently MDEC you can try 16bpp encoded videos like the last part of the Final Fantasy VII intro (train sequence) or the TEKKEN intro (both 16bpp encoded).

- Why you did this?

I have been interested in emulating hardware for some time. I started doing a Java [Chip8](https://github.com/BluestormDNA/Chip8) and a C# [Intel 8080 CPU](https://github.com/BluestormDNA/i8080-Space-Invaders) (used on the classic arcade Space Invaders). Some later i did [Nintendo Gameboy](https://github.com/BluestormDNA/ProjectDMG). I wanted to keep forward to do some 3D so i ended with the PSX as it had a good library of games...

- How you did this?

I  mainly used Martin Korth PSX-SPX documentation about the Playstation hardware at https://problemkaputt.de/psx-spx.htm
Also the people at the #playstation channel on the emudev discord at https://discord.gg/dkmJAes was very helpful.

