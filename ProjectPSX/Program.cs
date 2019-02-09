using OpenTK;
using System;

namespace ProjectPSX {
    class Program {
        static void Main() {
            GameWindow window = new GameWindow(800, 600);
            window.Title = "ProjectPSX";
            Emu emu = new Emu(window);
            window.Run();
        }
    }
}
