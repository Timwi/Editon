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
        public const int EditorLeft = 0;
        public const int EditorRight = 3;
        public const int EditorTop = 1;
        public const int EditorBottom = 2;

        static Scrollbar _horizScroll = new Scrollbar(false), _vertScroll = new Scrollbar(true);

        static FncFile _file;
        static string _filePath;
        static bool _fileChanged;
        static bool _exit;
        static string[] _fileCharsCache;

        static FncFileOptions _fileOptions = new FncFileOptions { HBoxPadding = 1, HBoxSpacing = 1 };

        static Item _selectedItem;

        static bool _editingBox;
        static int _selectionStart, _selectionLength;

        // X1,Y1 = inclusive; X2,Y2 = exclusive
        static bool _invalidatedRegion;
        static int _invalidatedRegionX1, _invalidatedRegionY1, _invalidatedRegionX2, _invalidatedRegionY2;

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

                var f = new Form();
                f.Show();
                f.Hide();

                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.Clear();
                Console.ResetColor();
                Console.SetCursorPosition(Console.BufferWidth - 3, Console.BufferHeight - 2);
                ConsoleUtil.Write("▓▓▓".Color(ConsoleColor.Gray));

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

        static void fileNew()
        {
            if (canDestroy())
            {
                _file = new FncFile();
                _filePath = null;
                _fileChanged = false;
                Console.Title = "Untitled — Editon";
                updateAfterEdit();
            }
        }

        static void fileOpen(string filePath, bool doThrow)
        {
            try
            {
                _file = Parse(filePath);
                _filePath = filePath;
                _fileChanged = false;
                _selectedItem = _file.Items.FirstOrDefault();
                invalidate(0, 0, Console.BufferWidth, Console.BufferHeight);
                Console.Title = Path.GetFileName(_filePath) + " — Editon";
                updateAfterEdit();
            }
            catch (Exception e)
            {
                if (doThrow)
                    throw;
                DlgMessage.Show("{0} ({1})".Fmt(e.Message, e.GetType().Name), "Error", DlgType.Error);
            }
        }

        static void moveCursorVert(bool up)
        {
            try
            {
                var candidate = _file.Items.Where(item =>
                {
                    if (item == _selectedItem)
                        return false;
                    var x = item.CenterX - _selectedItem.CenterX;
                    var y = item.CenterY - _selectedItem.CenterY;
                    return up ? (y <= 2 * x && y <= -2 * x) : (y >= 2 * x && y >= -2 * x);
                }).MinElement(item => up ? -item.CenterY : item.CenterY);
                invalidate(_selectedItem);
                _selectedItem = candidate;
                invalidate(_selectedItem);
            }
            catch (InvalidOperationException)
            {
            }
        }

        static void moveCursorHoriz(bool left)
        {
            try
            {
                var candidate = _file.Items.Where(item =>
                {
                    if (item == _selectedItem)
                        return false;
                    var x = item.CenterX - _selectedItem.CenterX;
                    var y = item.CenterY - _selectedItem.CenterY;
                    return left ? (2 * y >= x && 2 * y <= -x) : (2 * y <= x && 2 * y >= -x);
                }).MinElement(item => left ? -item.CenterX : item.CenterX);
                invalidate(_selectedItem);
                _selectedItem = candidate;
                invalidate(_selectedItem);
            }
            catch (InvalidOperationException)
            {
            }
        }

        static void redrawIfNecessary()
        {
            _horizScroll.Render();
            _vertScroll.Render();

            if (!_invalidatedRegion)
                return;
            _invalidatedRegion = false;
            var w = Console.BufferWidth - EditonProgram.EditorLeft - EditonProgram.EditorRight;
            var h = Console.BufferHeight - EditonProgram.EditorTop - EditonProgram.EditorBottom;
            if ((_invalidatedRegionX1 >= _horizScroll.Value + w) || (_invalidatedRegionY1 >= _vertScroll.Value + h) || (_invalidatedRegionX2 <= _horizScroll.Value) || (_invalidatedRegionY2 <= _vertScroll.Value))
                return;
            ensureFileChars();
            for (int bufY = EditorTop; bufY < Console.BufferHeight - EditorBottom; bufY++)
            {
                var y = bufY + _vertScroll.Value - EditorTop;
                if (y < _invalidatedRegionY1 || y > _invalidatedRegionY2)
                    continue;
                var s = Math.Max(_invalidatedRegionX1, _horizScroll.Value);
                var l = Math.Min(_invalidatedRegionX2, _horizScroll.Value + w) - s;
                Console.SetCursorPosition(_invalidatedRegionX1 - _horizScroll.Value + EditorLeft, bufY);
                if (y < _fileCharsCache.Length)
                {
                    var str = _fileCharsCache[y].SubstringSafe(s, l).PadRight(l).Color(ConsoleColor.Gray);
                    if (_selectedItem != null && y >= _selectedItem.PosY1 && y < _selectedItem.PosY2 && s > _selectedItem.PosX1 - l && s < _selectedItem.PosX2)
                    {
                        var st = Math.Max(0, _selectedItem.PosX1 - s);
                        str = str.ColorSubstring(st, Math.Min(_selectedItem.PosX2 - _selectedItem.PosX1, l - st), ConsoleColor.White, ConsoleColor.DarkBlue);
                    }
                    ConsoleUtil.Write(str);
                }
                else
                    Console.Write(new string(' ', l));
            }
        }

        static void ensureFileChars()
        {
            if (_fileCharsCache != null)
                return;
            var dic = new Dictionary<int, Dictionary<int, LineChars>>();
            foreach (var item in _file.Items)
                item.IfType(
                    (Box box) =>
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
                    },
                    (HLine hl) =>
                    {
                        for (int x = hl.X1; x < hl.X2; x++)
                        {
                            dic.BitwiseOrSafe(x, hl.Y, hl.LineType.At(LineLocation.Right));
                            dic.BitwiseOrSafe(x + 1, hl.Y, hl.LineType.At(LineLocation.Left));
                        }
                    },
                    (VLine vl) =>
                    {
                        for (int y = vl.Y1; y < vl.Y2; y++)
                        {
                            dic.BitwiseOrSafe(vl.X, y, vl.LineType.At(LineLocation.Bottom));
                            dic.BitwiseOrSafe(vl.X, y + 1, vl.LineType.At(LineLocation.Top));
                        }
                    });

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

            foreach (var box in _file.Items.OfType<Box>())
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

        static void invalidate(Item item) { invalidate(item.PosX1, item.PosY1, item.PosX2, item.PosY2); }

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
            Dictionary<ConsoleModifiers, Action> dic;
            Action action;
            if (KeyBindings.TryGetValue(key.Key, out dic) && dic.TryGetValue(key.Modifiers, out action))
                action();
        }

        static void updateAfterEdit()
        {
            _horizScroll.Max = _file.Items.Max(i => i.PosX2) + 1;
            _vertScroll.Max = _file.Items.Max(i => i.PosY2) + 1;
        }

        static bool canDestroy()
        {
            return false;
        }
    }
}
