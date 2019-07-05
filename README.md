# ProjectPSX

**ProjectPSX is a C# coded emulator of the original Sony Playstation (PSX)**

*This is a personal project with the scope to learn about hardware and the development of emulators.*

ProjectPSX dosn't use any external dependency and uses rather simplistyc C# code.

At the moment the following is implemented:
- CPU and GTE Coprocessor
- Partial CDROM
- Partial GPU
...

> **Note:**  A valid PSX Bios (SCPH1001.BIN) is needed to run the emulator. 

## Compability

The emulator is in very early development and there are constant rewrites. Somehow and although with some limitations there are some games that already work like:
Ridge Racer Ace Combat, Final Fantasy 7, Time Crisis, Ace Combat...

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

TODO
