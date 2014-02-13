using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util;
using RT.Util.ExtensionMethods;
using System.Threading;
using RT.Util.Consoles;

namespace Editon
{
    class Editor
    {
        private ConsoleColoredString[] CurrentFile;
        private string CurrentFilePath;
        private int X, Y;
        private Scrollbar HorizScrollbar, VertScrollbar;
        private Queue<InputElement> InputQueue;

        public Editor()
        {
        }

        public void RunUI()
        {
            Console.CursorVisible = false;
            Console.BufferHeight = 80;

            HorizScrollbar = new Scrollbar { Min = 0, Max = CurrentFile.Max(l => l.Length), Value = 0 };
            VertScrollbar = new Scrollbar { Min = 0, Max = CurrentFile.Length, Value = 0 };

            RedrawAll();
        }

        private void RedrawAll()
        {
            RedrawEditingWindow(0, 0, Console.BufferWidth - 4, Console.BufferHeight - 2);
            RedrawScrollbars();
        }

        private void RedrawScrollbars()
        {
            HorizScrollbar.RenderHorizontal();
            VertScrollbar.RenderVertical();
        }

        private void RedrawEditingWindow(int fromX, int fromY, int toX, int toY)
        {
            for (int y = fromY; y <= toY && y < CurrentFile.Length; y++)
            {
                var lineNumber = y + VertScrollbar.Value;
                var line = CurrentFile[lineNumber];
                Console.SetCursorPosition(fromX, y);
                var fromInLine = HorizScrollbar.Value + fromX;
                var print = fromInLine < line.Length ? line.Substring(fromInLine, Math.Min(fromInLine + toX - fromX + 1, line.Length - fromInLine)) : "";
                ConsoleUtil.Write(print + new string(' ', toX - fromX + 1 - print.Length));
            }
        }
    }
}
