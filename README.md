# ProjectPSX

**ProjectPSX is a C# coded emulator of the original Sony Playstation (PSX)**

*This is a personal project with the scope to learn about hardware and the development of emulators.*

ProjectPSX dosn't use any external dependency and uses rather simplistyc C# code.

At the moment the following is implemented:
- CPU and GTE Coprocessor
- Partial CDROM
- Partial GPU
...

> **Note:**  A valid SCPH1001.BIN PlayStation Bios is needed to run the emulator. 

## Compability

The emulator is in very early development and there are constant rewrites. Somehow and although with some limitations there are some games that already work like:
Ridge Racer, Ridge Racer Revolution, Ace Combat, Final Fantasy 7, Time Crisis, Crash Bandicoot 1 2 and 3, Spyro the dragon...

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
![psx](https://user-images.githubusercontent.com/28767885/60985122-30e29900-a33d-11e9-8956-4b933a2745b4.PNG)
![cpu](https://user-images.githubusercontent.com/28767885/60985112-304a0280-a33d-11e9-83b3-49a15fb1c117.PNG)
![gte](https://user-images.githubusercontent.com/28767885/60985120-30e29900-a33d-11e9-8cfa-1753b878e023.PNG)
![crash](https://user-images.githubusercontent.com/28767885/60985114-304a0280-a33d-11e9-80e2-08cd1c5abfbe.PNG)
![rr](https://user-images.githubusercontent.com/28767885/60985123-30e29900-a33d-11e9-9188-f942e44bcc3a.PNG)
![rrTranspBlend](https://user-images.githubusercontent.com/28767885/60985124-317b2f80-a33d-11e9-97b4-df9b50acd73e.PNG)
![ff7](https://user-images.githubusercontent.com/28767885/60985116-304a0280-a33d-11e9-9944-f0dfc4f085c3.PNG)
![ff7B](https://user-images.githubusercontent.com/28767885/60985118-304a0280-a33d-11e9-9170-af6902f8bd08.PNG)

## Quick Faq

TODO

