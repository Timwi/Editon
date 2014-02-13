using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using RT.Util;
using RT.Util.CommandLine;
using RT.Util.Consoles;
using RT.Util.ExtensionMethods;
using RT.Util.Xml;
using System.Collections.Generic;
using System.Windows.Forms;
using RT.Util.Dialogs;

namespace Editon
{
    static partial class EditonProgram
    {
        static FncFile _file;
        static string _filePath;
        static bool _fileChanged;
        static bool _exit;
        static Scrollbar _horizScroll, _vertScroll;

        static Box _selectedBox;
        static HLine _selectedHLine;
        static VLine _selectedVLine;

        static int _invalidatedRegionX1, _invalidatedRegionY1, _invalidatedRegionX2, _invalidatedRegionY2;
        static bool _invalidatedRegion;

        [STAThread]
        static int Main(string[] args)
        {
            var hadUi = false;
            try
            {
                try { Console.OutputEncoding = Encoding.UTF8; }
                catch { }

                if (args.Length == 2 && args[0] == "--post-build-check")
                    return Ut.RunPostBuildChecks(args[1], typeof(EditonProgram).Assembly);

                if (args.Length > 1)
                {
                    Console.WriteLine("Only one command-line argument allowed (file to open).");
                    return 1;
                }

                if (args.Length == 1)
                    fileOpen(Path.GetFullPath(args[0]), true);
                else
                    fileNew();

                hadUi = true;
                while (!_exit)
                {
                    while (Console.KeyAvailable)
                        processKey(Console.ReadKey(intercept: true));
                    redrawIfNecessary();
                }

                return 0;
            }
            catch (Exception e)
            {
                if (hadUi)
                    Console.Clear();
                ConsoleUtil.WriteParagraphs("{0/Magenta} {1/Red} ({2/DarkRed})".Color(ConsoleColor.DarkRed).Fmt("Error:", e.Message, e.GetType().FullName), "Error: ".Length);
                return 1;
            }
        }

        private static void redrawIfNecessary()
        {
            if (!_invalidatedRegion)
                return;
            _invalidatedRegion = false;
            var sb = new StringBuilder();
            for (int y = _invalidatedRegionY1; y < _invalidatedRegionY2; y++)
            {
                sb.Clear();
                for (int x = _invalidatedRegionX1; x < _invalidatedRegionX2; x++)
                {
                    var box = _file.Boxes.FirstOrDefault(bx => x >= bx.X && x < bx.X + bx.Width && y >= bx.Y && y < bx.Y + bx.Height);
                    if (box != null)
                    {
                        var left = x == box.X;
                        var right = x == box.X + box.Width;
                        var top = x == box.Y;
                        var bottom = x == box.Y + box.Height;

                        if ((top || bottom) && (left || right))
                            // Corner
                            sb.Append("┌╒╓╔┐╕╖╗└╘╙╚┘╛╜╝"[(bottom ? 8 : 0) + (right ? 4 : 0) + (box.LineTypes[left ? 3 : 1] == LineType.Double ? 2 : 0) + (box.LineTypes[top ? 0 : 2] == LineType.Double ? 1 : 0)]);
                        else if (top || bottom)
                            sb.Append(box.LineTypes[top ? 0 : 2] == LineType.Double ? '═' : '─');
                        else if (left || right)
                            sb.Append(box.LineTypes[left ? 3 : 1] == LineType.Double ? '║' : '│');

                        continue;
                    }
                    // TODO: lines
                }
            }
        }

        static void invalidate(int x1, int y1, int x2, int y2)
        {
            if (!_invalidatedRegion)
            {
                _invalidatedRegionX1 = x1;
                _invalidatedRegionY1 = y1;
                _invalidatedRegionX2 = x2;
                _invalidatedRegionY2 = y2;
            }
            else
            {
                _invalidatedRegionX1 = Math.Min(_invalidatedRegionX1, x1);
                _invalidatedRegionY1 = Math.Min(_invalidatedRegionY1, y1);
                _invalidatedRegionX2 = Math.Max(_invalidatedRegionX2, x2);
                _invalidatedRegionY2 = Math.Max(_invalidatedRegionY2, y2);
            }
            _invalidatedRegion = true;
        }

        static void processKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.Spacebar && key.Modifiers == ConsoleModifiers.Control)
            {
                MessageBox.Show("Ctrl+Space!");
                _exit = true;
            }
        }

        static void fileOpen(string filePath, bool doThrow)
        {
            try
            {
                _file = Parse(filePath);
                _filePath = filePath;
                _fileChanged = false;
                _selectedBox = _file.Boxes.FirstOrDefault();
                updateAfterEdit();
            }
            catch (Exception e)
            {
                if (doThrow)
                    throw;
                DlgMessage.Show("{0} ({1})".Fmt(e.Message, e.GetType().Name), "Error", DlgType.Error);
            }
        }

        static void fileNew()
        {
            if (canDestroy())
            {
                _file = new FncFile();
                _filePath = null;
                _fileChanged = false;
                updateAfterEdit();
            }
        }

        static void updateAfterEdit()
        {
            _horizScroll.Max = Math.Max(Math.Max(
                _file.Boxes.Max(b => b.X + b.Width),
                _file.HLines.Max(hl => hl.X2)),
                _file.VLines.Max(vl => vl.X));
            _vertScroll.Max = Math.Max(Math.Max(
                _file.Boxes.Max(b => b.Y + b.Height),
                _file.HLines.Max(hl => hl.Y)),
                _file.VLines.Max(vl => vl.Y2));
        }

        static bool canDestroy()
        {
            return false;
        }
    }
}
