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
        public const int EditorTop = 0;
        public const int EditorBottom = 2;

        static FncFileOptions _fileOptions = new FncFileOptions
        {
            BoxHPadding = 1,
            HSpacing = 1
        };

        public static int EditorWidth { get { return Console.BufferWidth - EditorLeft - EditorRight; } }
        public static int EditorHeight { get { return Console.BufferHeight - EditorTop - EditorBottom; } }

        static Scrollbar _horizScroll = new Scrollbar(false), _vertScroll = new Scrollbar(true);

        static FncFile _file;
        static string _filePath;
        static bool _fileChanged;
        static string[] _fileCharsCache;
        static bool[][] _hasLineCache;

        static EditMode _mode;
        static int _cursorX, _cursorY;
        static Item _selectedItem;
        static int _selectedBoxTextAreaIndex = -1;

        // X1,Y1 = inclusive; X2,Y2 = exclusive
        static bool _invalidatedRegion;
        static int _invalidatedRegionX1, _invalidatedRegionY1, _invalidatedRegionX2, _invalidatedRegionY2;
        static bool _exit = false;

        [STAThread]
        static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            if (args.Length == 2 && args[0] == "--post-build-check")
                return Ut.RunPostBuildChecks(args[1], typeof(EditonProgram).Assembly);

            var hadUi = false;
            var prevBufWidth = Console.BufferWidth;
            var prevBufHeight = Console.BufferHeight;
            try
            {
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
                Console.TreatControlCAsInput = true;
                Console.SetBufferSize(Console.WindowWidth, Console.WindowHeight);
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
                    drawStatusBar("{0}{1} ({2}; U+{3:X4})".Fmt(key.Modifiers == 0 ? "" : key.Modifiers + " + ", key.Key, key.KeyChar, (int) key.KeyChar));
                    Dictionary<ConsoleKey, Dictionary<ConsoleModifiers, Action>> dic1;
                    Dictionary<ConsoleModifiers, Action> dic2;
                    Action action;
                    if (KeyBindings.TryGetValue(_mode, out dic1) && dic1.TryGetValue(key.Key, out dic2) && dic2.TryGetValue(key.Modifiers, out action))
                    {
                        action();
                        if (_exit)
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

        static void drawStatusBar(string status)
        {
            Console.SetCursorPosition(1, Console.BufferHeight - 1);
            ConsoleUtil.Write(status.SubstringSafe(0, Console.BufferWidth - 2).PadRight(Console.BufferWidth - 2).Color(ConsoleColor.White, ConsoleColor.DarkBlue));
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

        static void moveCursor(int x, int y)
        {
            var prevX = _cursorX;
            var prevY = _cursorY;
            var prevSel = _selectedItem;
            var prevEditingTextAreaIndex = _selectedBoxTextAreaIndex;

            _cursorX = x;
            _cursorY = y;

            _selectedItem =
                (Item) _file.Items.OfType<NonBoxItem>().FirstOrDefault(item => item.X == _cursorX && item.Y == _cursorY) ??
                (Item) _file.Items.OfType<Box>().FirstOrDefault(box => box.Contains(_cursorX, _cursorY));

            _selectedBoxTextAreaIndex = _selectedItem.IfType(
                (Box box) => box.TextAreas.IndexOf(area => area.Any(line => _cursorY == line.Y && _cursorX >= line.X && _cursorX < line.X + line.Content.Length)),
                otherwise => -1);

            while (_cursorX >= _horizScroll.Value + EditorWidth)
            {
                _horizScroll.Value += 20;
                invalidateAll();
            }
            while (_cursorX < _horizScroll.Value)
            {
                _horizScroll.Value = Math.Max(0, _horizScroll.Value - 20);
                invalidateAll();
            }
            while (_cursorY >= _vertScroll.Value + EditorHeight)
            {
                _vertScroll.Value += 10;
                invalidateAll();
            }
            while (_cursorY < _vertScroll.Value)
            {
                _vertScroll.Value = Math.Max(0, _vertScroll.Value - 10);
                invalidateAll();
            }

            if (_selectedItem != prevSel || _selectedBoxTextAreaIndex != prevEditingTextAreaIndex)
            {
                Invalidate(prevSel);
                Invalidate(_selectedItem);
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

            if ((_invalidatedRegionX1 >= _horizScroll.Value + EditorWidth) || (_invalidatedRegionY1 >= _vertScroll.Value + EditorHeight) || (_invalidatedRegionX2 <= _horizScroll.Value) || (_invalidatedRegionY2 <= _vertScroll.Value))
                return;

            ensureFileChars();

            // For now, it is fast enough to update entire lines. This fixes some console rendering bugs (e.g. gaps in the box-drawing characters).
            _invalidatedRegionX1 = 0;
            _invalidatedRegionX2 = _horizScroll.Value + Console.BufferWidth;
            Console.CursorVisible = false;

            var maxBufY = Console.BufferHeight - EditorBottom;
            for (int bufY = EditorTop; bufY < maxBufY; bufY++)
            {
                var y = bufY + _vertScroll.Value - EditorTop;
                if (y < _invalidatedRegionY1 || y >= _invalidatedRegionY2)
                    continue;

                var s = Math.Max(_invalidatedRegionX1, _horizScroll.Value);
                var l = Math.Min(_invalidatedRegionX2, _horizScroll.Value + EditorWidth) - s;
                Console.SetCursorPosition(s - _horizScroll.Value + EditorLeft, bufY);

                if (y < _fileCharsCache.Length)
                {
                    var str = _fileCharsCache[y].SubstringSafe(s, l).PadRight(l).Color(ConsoleColor.Gray);
                    if (_selectedItem != null && y >= _selectedItem.Y && y < _selectedItem.Y2 && s + l > _selectedItem.X && s < _selectedItem.X2)
                    {
                        var st = Math.Max(0, _selectedItem.X - s);
                        str = str.ColorSubstring(st, Math.Min(_selectedItem.X2 - _selectedItem.X, l - st), ConsoleColor.White, _mode == EditMode.Moving ? ConsoleColor.DarkRed : ConsoleColor.DarkBlue);
                    }
                    if (_mode == EditMode.Cursor)
                    {
                        if (_selectedItem != null && _selectedBoxTextAreaIndex != -1)
                            foreach (var line in ((Box) _selectedItem).TextAreas[_selectedBoxTextAreaIndex])
                                if (line.Y == y && line.X < s + l && line.X + line.Content.Length >= s)
                                {
                                    var st = Math.Max(0, line.X - s);
                                    str = str.ColorSubstringBackground(st, Math.Min(line.Content.Length, l - st), ConsoleColor.DarkCyan);
                                }
                        if (_cursorY == y && _cursorX >= s && _cursorX < s + l)
                            str = str.ColorSubstringBackground(_cursorX - s, 1, _selectedBoxTextAreaIndex != -1 ? ConsoleColor.Cyan : _selectedItem != null ? ConsoleColor.Blue : ConsoleColor.DarkBlue);
                    }
                    ConsoleUtil.Write(str);
                }
                else
                    Console.Write(new string(' ', l));
            }

            if (
                _mode == EditMode.Cursor &&
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
            _fileCharsCache = getFileChars(_file.Items, out _hasLineCache);
        }

        static string[] getFileChars(List<Item> items, out bool[][] hasLine, bool ignoreTextAreas = false)
        {
            var dic = new Dictionary<int, Dictionary<int, LineChars>>();
            var hasLineRet = Ut.NewArray<bool>(items.MaxOrDefault(i => i.X2, 0), items.MaxOrDefault(i => i.Y2, 0));
            var set = Ut.Lambda((int x, int y, LineChars lc) =>
            {
                dic.BitwiseOrSafe(x, y, lc);
                hasLineRet[x][y] = true;
            });
            foreach (var item in items)
            {
                item.IfType(
                    (Box box) =>
                    {
                        for (int x = 0; x < box.Width; x++)
                        {
                            set(box.X + x, box.Y, box.LineTypes[Direction.Up].At(Direction.Right));
                            set(box.X + x + 1, box.Y, box.LineTypes[Direction.Up].At(Direction.Left));
                            set(box.X + x, box.Y + box.Height, box.LineTypes[Direction.Down].At(Direction.Right));
                            set(box.X + x + 1, box.Y + box.Height, box.LineTypes[Direction.Down].At(Direction.Left));
                        }
                        for (int y = 0; y < box.Height; y++)
                        {
                            set(box.X, box.Y + y, box.LineTypes[Direction.Left].At(Direction.Down));
                            set(box.X, box.Y + y + 1, box.LineTypes[Direction.Left].At(Direction.Up));
                            set(box.X + box.Width, box.Y + y, box.LineTypes[Direction.Right].At(Direction.Down));
                            set(box.X + box.Width, box.Y + y + 1, box.LineTypes[Direction.Right].At(Direction.Up));
                        }
                    },
                    (Node n) =>
                    {
                        var lineType = n.LineTypes[Direction.Right];
                        if (lineType != LineType.None)
                        {
                            var x2 = n.JoinUpWith[Direction.Right].X;
                            for (int x = n.X; x < x2; x++)
                            {
                                set(x, n.Y, lineType.At(Direction.Right));
                                set(x + 1, n.Y, lineType.At(Direction.Left));
                            }
                            if (n.JoinUpWith[Direction.Right] is LineEnd)
                                set(x2, n.Y, lineType.At(Direction.Right));
                        }
                        lineType = n.LineTypes[Direction.Down];
                        if (lineType != LineType.None)
                        {
                            var y2 = n.JoinUpWith[Direction.Down].Y;
                            for (int y = n.Y; y < y2; y++)
                            {
                                set(n.X, y, lineType.At(Direction.Down));
                                set(n.X, y + 1, lineType.At(Direction.Up));
                            }
                            if (n.JoinUpWith[Direction.Down] is LineEnd)
                                set(n.X, y2, lineType.At(Direction.Down));
                        }
                    },
                    (LineEnd e) =>
                    {
                        if (e.Direction == Direction.Right)
                        {
                            set(e.X, e.Y, e.LineType.At(Direction.Left));
                            var x2 = e.JoinUpWith.X;
                            for (int x = e.X; x < x2; x++)
                            {
                                set(x, e.Y, e.LineType.At(Direction.Right));
                                set(x + 1, e.Y, e.LineType.At(Direction.Left));
                            }
                            if (e.JoinUpWith is LineEnd)
                                set(x2, e.Y, e.LineType.At(Direction.Right));
                        }
                        else if (e.Direction == Direction.Down)
                        {
                            set(e.X, e.Y, e.LineType.At(Direction.Up));
                            var y2 = e.JoinUpWith.Y;
                            for (int y = e.Y; y < y2; y++)
                            {
                                set(e.X, y, e.LineType.At(Direction.Down));
                                set(e.X, y + 1, e.LineType.At(Direction.Up));
                            }
                            if (e.JoinUpWith is LineEnd)
                                set(e.X, y2, e.LineType.At(Direction.Down));
                        }
                    });
            }

            var sb = new StringBuilder();
            var width = dic.Keys.Max();
            var height = dic.Values.SelectMany(val => val.Keys).Max();
            var result = new string[height + 1];
            for (int y = 0; y <= height; y++)
            {
                for (int x = 0; x <= width; x++)
                {
                    var val = dic.Get(x, y, (LineChars) 0);
                    sb.Append(" │║─└╙═╘╚││║┌├╟╒╞╠║║║╓╟╟╔╠╠─┘╜─┴╨═╧╩┐┤╢┬┼╫╤╪╬╖╢╢╥╫╫╦╬╬═╛╝═╧╩═╧╩╕╡╣╤╪╬╤╪╬╗╣╣╦╬╬╦╬╬"[
                        ((int) (val & LineChars.TopMask) >> (2 * (int) (int) Direction.Up)) +
                        ((int) (val & LineChars.RightMask) >> (2 * (int) (int) Direction.Right)) * 3 +
                        ((int) (val & LineChars.BottomMask) >> (2 * (int) (int) Direction.Down)) * 3 * 3 +
                        ((int) (val & LineChars.LeftMask) >> (2 * (int) (int) Direction.Left)) * 3 * 3 * 3
                    ]);
                }
                result[y] = sb.ToString();
                sb.Clear();
            }

            if (!ignoreTextAreas)
                foreach (var box in items.OfType<Box>())
                    foreach (var area in box.TextAreas)
                        foreach (var line in area)
                            result[line.Y] = result[line.Y].Substring(0, line.X) + line.Content + result[line.Y].Substring(line.X + line.Content.Length);

            hasLine = hasLineRet;
            return result;
        }

        public static void Invalidate(Item item)
        {
            if (item != null)
                invalidate(item.X, item.Y, item.X2, item.Y2);
        }
        static void invalidateAll() { invalidate(_horizScroll.Value, _vertScroll.Value, _horizScroll.Value + Console.BufferWidth, _vertScroll.Value + Console.BufferHeight); }

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
            _horizScroll.Max = _file.Items.Max(i => i.X2) + 1;
            _vertScroll.Max = _file.Items.Max(i => i.Y2) + 1;
        }

        static bool canDestroy()
        {
            return false;
        }

        static void move(Direction dir)
        {
            if (_selectedItem == null)
                return;

            throw new NotImplementedException();
        }
    }
}
