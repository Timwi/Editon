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
        static Scrollbar _horizScroll = new Scrollbar(false), _vertScroll = new Scrollbar(true);
        static string[] _fileCharsCache;

        static FncFileOptions _fileOptions = new FncFileOptions { HBoxPadding = 1, HBoxSpacing = 1 };

        static Box _selectedBox;
        static HLine _selectedHLine;
        static VLine _selectedVLine;

        // X1,Y1 = inclusive; X2,Y2 = exclusive
        static int _invalidatedRegionX1, _invalidatedRegionY1, _invalidatedRegionX2, _invalidatedRegionY2;
        static bool _invalidatedRegion;

        [STAThread]
        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; }
            catch { }

            if (args.Length == 2 && args[0] == "--post-build-check")
                return Ut.RunPostBuildChecks(args[1], typeof(EditonProgram).Assembly);

            var hadUi = false;
            var prevBufWidth = Console.BufferWidth;
            var prevBufHeight = Console.BufferHeight;
            try
            {
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);

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

                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.Clear();
                Console.BackgroundColor = ConsoleColor.Black;

                redrawIfNecessary();
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
                ConsoleUtil.WriteParagraphs("{0/Magenta} {1/Red} ({2})".Color(ConsoleColor.DarkRed).Fmt("Error:", e.Message, e.GetType().FullName), "Error: ".Length);
                return 1;
            }
            finally
            {
                Console.SetBufferSize(prevBufWidth, prevBufHeight);
            }
        }

        private static void redrawIfNecessary()
        {
            _horizScroll.Render();
            _vertScroll.Render();

            if (!_invalidatedRegion)
                return;
            _invalidatedRegion = false;
            var w = Console.BufferWidth - 3;   // Vertical scrollbar = 3 chars
            var h = Console.BufferHeight - 2;   // Horiz scrollbar + Status bar
            if ((_invalidatedRegionX1 >= _horizScroll.Value + w) || (_invalidatedRegionY1 >= _vertScroll.Value + h) || (_invalidatedRegionX2 <= _horizScroll.Value) || (_invalidatedRegionY2 <= _vertScroll.Value))
                return;
            ensureFileChars();
            for (int bufY = 0; bufY < Console.BufferHeight - 2; bufY++)
            {
                var y = bufY + _vertScroll.Value;
                if (y < _invalidatedRegionY1 || y > _invalidatedRegionY2)
                    continue;
                var s = Math.Max(_invalidatedRegionX1, _horizScroll.Value);
                var l = Math.Min(_invalidatedRegionX2, _horizScroll.Value + w) - s;
                Console.CursorLeft = _invalidatedRegionX1 - _horizScroll.Value;
                Console.CursorTop = y;
                if (y < _fileCharsCache.Length)
                {
                    var str = _fileCharsCache[y].SubstringSafe(s, l).PadRight(l).Color(ConsoleColor.Gray);
                    if (_selectedBox != null && y >= _selectedBox.Y && y <= _selectedBox.Y + _selectedBox.Height && _selectedBox.X + _selectedBox.Width >= s && _selectedBox.X < s + l)
                    {
                        var st = Math.Max(0, _selectedBox.X - s);
                        str = str.ColorSubstring(st, Math.Min(_selectedBox.Width + 1, l - st), ConsoleColor.Cyan);
                    }
                    ConsoleUtil.Write(str);
                }
                else
                    Console.Write(new string(' ', l));
            }
        }

        private static void ensureFileChars()
        {
            if (_fileCharsCache != null)
                return;
            var dic = new Dictionary<int, Dictionary<int, LineChars>>();
            foreach (var box in _file.Boxes)
            {
                for (int x = 0; x < box.Width; x++)
                {
                    dic.BitwiseOrSafe(box.X + x, box.Y, box.LineTypes[LineLocation.Top].At(LineLocation.Right));
                    dic.BitwiseOrSafe(box.X + x + 1, box.Y, box.LineTypes[LineLocation.Top].At(LineLocation.Left));
                    dic.BitwiseOrSafe(box.X + x, box.Y + box.Height, box.LineTypes[LineLocation.Bottom].At(LineLocation.Right));
                    dic.BitwiseOrSafe(box.X + x + 1, box.Y + box.Height, box.LineTypes[LineLocation.Bottom].At(LineLocation.Left));
                }
                for (int y = 0; y < box.Height; y++)
                {
                    dic.BitwiseOrSafe(box.X, box.Y + y, box.LineTypes[LineLocation.Left].At(LineLocation.Bottom));
                    dic.BitwiseOrSafe(box.X, box.Y + y + 1, box.LineTypes[LineLocation.Left].At(LineLocation.Top));
                    dic.BitwiseOrSafe(box.X + box.Width, box.Y + y, box.LineTypes[LineLocation.Right].At(LineLocation.Bottom));
                    dic.BitwiseOrSafe(box.X + box.Width, box.Y + y + 1, box.LineTypes[LineLocation.Right].At(LineLocation.Top));
                }
            }
            foreach (var hl in _file.HLines)
            {
                for (int x = hl.X1; x < hl.X2; x++)
                {
                    dic.BitwiseOrSafe(x, hl.Y, hl.LineType.At(LineLocation.Right));
                    dic.BitwiseOrSafe(x + 1, hl.Y, hl.LineType.At(LineLocation.Left));
                }
            }
            foreach (var vl in _file.VLines)
            {
                for (int y = vl.Y1; y < vl.Y2; y++)
                {
                    dic.BitwiseOrSafe(vl.X, y, vl.LineType.At(LineLocation.Bottom));
                    dic.BitwiseOrSafe(vl.X, y + 1, vl.LineType.At(LineLocation.Top));
                }
            }
            var sb = new StringBuilder();
            var width = dic.Keys.Max();
            var height = dic.Values.SelectMany(val => val.Keys).Max();
            _fileCharsCache = new string[height + 1];
            for (int y = 0; y <= height; y++)
            {
                for (int x = 0; x <= width; x++)
                {
                    var val = dic.Get(x, y, (LineChars) 0);
                    sb.Append(" │║─└╙═╘╚││║┌├╟╒╞╠║║║╓╟╟╔╠╠─┘╜─┴╨═╧╩┐┤╢┬┼╫╤╪╬╖╢╢╥╫╫╦╬╬═╛╝═╧╩═╧╩╕╡╣╤╪╬╤╪╬╗╣╣╦╬╬╦╬╬"[
                        ((int) (val & LineChars.TopMask) >> (2 * (int) LineLocation.Top)) +
                        ((int) (val & LineChars.RightMask) >> (2 * (int) LineLocation.Right)) * 3 +
                        ((int) (val & LineChars.BottomMask) >> (2 * (int) LineLocation.Bottom)) * 3 * 3 +
                        ((int) (val & LineChars.LeftMask) >> (2 * (int) LineLocation.Left)) * 3 * 3 * 3
                    ]);
                }
                _fileCharsCache[y] = sb.ToString();
                sb.Clear();
            }
            foreach (var box in _file.Boxes)
            {
                var x = box.X + 1 + _fileOptions.HBoxPadding;
                var y = box.Y + 1;
                var content = box.Content;
                while (content.Length > 0)
                {
                    var p = content.IndexOf('\n');
                    if (p == -1)
                        break;
                    _fileCharsCache[y] = _fileCharsCache[y].Substring(0, x) + content.Substring(0, p) + _fileCharsCache[y].Substring(x + p);
                    content = content.Substring(p + 1);
                    y++;
                }
                _fileCharsCache[y] = _fileCharsCache[y].Substring(0, x) + content + _fileCharsCache[y].Substring(x + content.Length);
            }
        }

        static void invalidate(int x1, int y1, int x2, int y2)
        {
            _fileCharsCache = null;
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
                invalidate(0, 0, Console.BufferWidth, Console.BufferHeight);
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
