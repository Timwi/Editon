using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RT.Util;
using RT.Util.Consoles;
using RT.Util.Dialogs;
using RT.Util.ExtensionMethods;

namespace Editon
{
    static partial class EditonProgram
    {
        public const int EditorLeft = 0;
        public const int EditorRight = 3;
        public const int EditorTop = 1;
        public const int EditorBottom = 2;

        static FncFileOptions _fileOptions = new FncFileOptions
        {
            BoxHPadding = 1,
            BoxHSpacing = 1
        };

        public static int EditorWidth { get { return Console.BufferWidth - EditorLeft - EditorRight; } }
        public static int EditorHeight { get { return Console.BufferHeight - EditorTop - EditorBottom; } }

        static Scrollbar _horizScroll = new Scrollbar(false), _vertScroll = new Scrollbar(true);

        static FncFile _file;
        static string _filePath;
        static bool _fileChanged;
        static string[] _fileCharsCache;

        static Item _selectedItem;
        static int _cursorX, _cursorY;

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
                Console.BackgroundColor = ConsoleColor.DarkBlue;
                Console.Clear();
                Console.ResetColor();
                Console.SetCursorPosition(Console.BufferWidth - 3, Console.BufferHeight - 2);
                ConsoleUtil.Write("▓▓▓".Color(ConsoleColor.Gray));

                while (true)
                {
                    if (!Console.KeyAvailable)
                        redrawIfNecessary();

                    var key = Console.ReadKey(intercept: true);
                    Dictionary<ConsoleModifiers, Action<KeyProcessingInfo>> dic;
                    Action<KeyProcessingInfo> action;
                    if (KeyBindings.TryGetValue(key.Key, out dic) && dic.TryGetValue(key.Modifiers, out action))
                    {
                        var inf = new KeyProcessingInfo();
                        action(inf);
                        if (inf.Exit)
                            return 0;
                    }
                }
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

        static void moveCursorUp(KeyProcessingInfo inf) { moveCursor(_cursorX, _cursorY > 0 ? _cursorY - 1 : _cursorY); }
        static void moveCursorRight(KeyProcessingInfo inf) { moveCursor(_cursorX + 1, _cursorY); }
        static void moveCursorDown(KeyProcessingInfo inf) { moveCursor(_cursorX, _cursorY + 1); }
        static void moveCursorLeft(KeyProcessingInfo inf) { moveCursor(_cursorX > 0 ? _cursorX - 1 : _cursorX, _cursorY); }

        static void moveCursorUpFar(KeyProcessingInfo inf)
        {
            moveCursor(
                _cursorX,
                _file.Items
                    .Where(item => item.PosY1 < _cursorY && _cursorX >= item.PosX1 && _cursorX < item.PosX2)
                    .MaxElementOrDefault(item => item.PosY1)
                    .NullOr(item => item.PosY1)
                    ?? 0);
        }
        static void moveCursorRightFar(KeyProcessingInfo inf)
        {
            moveCursor(
                _file.Items
                    .Where(item => item.PosX1 > _cursorX && _cursorY >= item.PosY1 && _cursorY < item.PosY2)
                    .MinElementOrDefault(item => item.PosX1)
                    .NullOr(item => item.PosX1)
                    ?? _file.Items
                        .Where(item => _cursorY >= item.PosY1 && _cursorY < item.PosY2)
                        .MaxElementOrDefault(item => item.PosX2)
                        .NullOr(item => item.PosX2)
                        ?? 0,
                _cursorY);
        }
        static void moveCursorDownFar(KeyProcessingInfo inf)
        {
            moveCursor(
                _cursorX,
                _file.Items
                    .Where(item => item.PosY1 > _cursorY && _cursorX >= item.PosX1 && _cursorX < item.PosX2)
                    .MinElementOrDefault(item => item.PosY1)
                    .NullOr(item => item.PosY1)
                    ?? _file.Items
                        .Where(item => _cursorX >= item.PosX1 && _cursorX < item.PosX2)
                        .MaxElementOrDefault(item => item.PosY2)
                        .NullOr(item => item.PosY2)
                        ?? 0);
        }
        static void moveCursorLeftFar(KeyProcessingInfo inf)
        {
            moveCursor(
                _file.Items
                    .Where(item => item.PosX1 < _cursorX && _cursorY >= item.PosY1 && _cursorY < item.PosY2)
                    .MaxElementOrDefault(item => item.PosX1)
                    .NullOr(item => item.PosX1)
                    ?? 0,
                _cursorY);
        }
        static void moveCursorRightEnd(KeyProcessingInfo inf)
        {
            moveCursor(
                _file.Items
                    .Where(item => _cursorY >= item.PosY1 && _cursorY < item.PosY2)
                    .MaxElementOrDefault(item => item.PosX2)
                    .NullOr(item => item.PosX2)
                    ?? 0,
                _cursorY);
        }
        static void moveCursorLeftEnd(KeyProcessingInfo inf)
        {
            moveCursor(
                _cursorX == 0 ? _file.Items.Where(item => _cursorY >= item.PosY1 && _cursorY < item.PosY2).MinElementOrDefault(item => item.PosX1).NullOr(item => item.PosX1) ?? 0 : 0,
                _cursorY
            );
        }

        static void moveCursor(int x, int y)
        {
            var prevX = _cursorX;
            var prevY = _cursorY;
            var prevSel = _selectedItem;

            _cursorX = x;
            _cursorY = y;

            _selectedItem =
                _file.Items.FirstPreferNonBox(item =>
                    prevX >= item.PosX1 && prevX < item.PosX2 && prevY >= item.PosY1 && prevY < item.PosY2 &&
                    _cursorX >= item.PosX1 && _cursorX < item.PosX2 && _cursorY >= item.PosY1 && _cursorY < item.PosY2) ??
                _file.Items.FirstPreferNonBox(item =>
                    _cursorX >= item.PosX1 && _cursorX < item.PosX2 && _cursorY >= item.PosY1 && _cursorY < item.PosY2);

            if (_selectedItem != prevSel)
            {
                invalidate(prevSel);
                invalidate(_selectedItem);
            }
            invalidate(prevX - 1, prevY - 1, prevX + 1, prevY + 1);
            invalidate(_cursorX - 1, _cursorY - 1, _cursorX + 1, _cursorY + 1);
        }

        static void redrawIfNecessary()
        {
            if (!_invalidatedRegion)
                return;

            _invalidatedRegion = false;
            _horizScroll.Render();
            _vertScroll.Render();

            var w = Console.BufferWidth - EditonProgram.EditorLeft - EditonProgram.EditorRight;
            var h = Console.BufferHeight - EditonProgram.EditorTop - EditonProgram.EditorBottom;

            if ((_invalidatedRegionX1 >= _horizScroll.Value + w) || (_invalidatedRegionY1 >= _vertScroll.Value + h) || (_invalidatedRegionX2 <= _horizScroll.Value) || (_invalidatedRegionY2 <= _vertScroll.Value))
                return;

            ensureFileChars();

            _invalidatedRegionX1 = 0;
            _invalidatedRegionX2 = Console.BufferWidth;
            Console.CursorVisible = false;

            for (int bufY = EditorTop; bufY < Console.BufferHeight - EditorBottom; bufY++)
            {
                var y = bufY + _vertScroll.Value - EditorTop;
                if (y < _invalidatedRegionY1 || y > _invalidatedRegionY2)
                    continue;

                var s = Math.Max(_invalidatedRegionX1, _horizScroll.Value);
                var l = Math.Min(_invalidatedRegionX2, _horizScroll.Value + w) - s;
                Console.SetCursorPosition(s - _horizScroll.Value + EditorLeft, bufY);

                if (y < _fileCharsCache.Length)
                {
                    var str = _fileCharsCache[y].SubstringSafe(s, l).PadRight(l).Color(ConsoleColor.Gray);
                    if (_selectedItem != null && y >= _selectedItem.PosY1 && y < _selectedItem.PosY2 && s + l > _selectedItem.PosX1 && s < _selectedItem.PosX2)
                    {
                        var st = Math.Max(0, _selectedItem.PosX1 - s);
                        str = str.ColorSubstring(st, Math.Min(_selectedItem.PosX2 - _selectedItem.PosX1, l - st), ConsoleColor.White, ConsoleColor.DarkBlue);
                    }
                    if (_cursorY == y && _cursorX >= s && _cursorX < s + l)
                        str = str.ColorSubstring(_cursorX - s, 1, ConsoleColor.White, _selectedItem != null && _selectedItem.Contains(_cursorX, _cursorY) ? ConsoleColor.Blue : ConsoleColor.DarkBlue);
                    ConsoleUtil.Write(str);
                }
                else
                    Console.Write(new string(' ', l));
            }

            if (
                _cursorX >= _horizScroll.Value && _cursorX < _horizScroll.Value + EditorWidth &&
                _cursorY >= _vertScroll.Value && _cursorY < _vertScroll.Value + EditorHeight)
            {
                Console.SetCursorPosition(_cursorX - _horizScroll.Value + EditorLeft, _cursorY - _vertScroll.Value + EditorTop);
                Console.CursorVisible = true;
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
                var x = box.X + 1 + _fileOptions.BoxHPadding;
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

        static void invalidate(Item item)
        {
            if (item != null)
                invalidate(item.PosX1, item.PosY1, item.PosX2, item.PosY2);
        }
        static void invalidateAll() { invalidate(0, 0, _horizScroll.Value + Console.BufferWidth, _vertScroll.Value + Console.BufferHeight); }

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
