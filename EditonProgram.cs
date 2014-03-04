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
            Console.CursorLeft = 1;
            Console.CursorTop = Console.BufferHeight - 1;
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
                _file.Items.FirstPreferNonBox(item =>
                    prevX >= item.PosX1 && prevX < item.PosX2 && prevY >= item.PosY1 && prevY < item.PosY2 &&
                    _cursorX >= item.PosX1 && _cursorX < item.PosX2 && _cursorY >= item.PosY1 && _cursorY < item.PosY2) ??
                _file.Items.FirstPreferNonBox(item =>
                    _cursorX >= item.PosX1 && _cursorX < item.PosX2 && _cursorY >= item.PosY1 && _cursorY < item.PosY2);

            if (_selectedItem is Box)
                _selectedBoxTextAreaIndex = ((Box) _selectedItem).TextAreas.IndexOf(area => area.Any(line => _cursorY == line.Y && _cursorX >= line.X && _cursorX < line.X + line.Content.Length));
            else
                _selectedBoxTextAreaIndex = -1;

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
                    if (_selectedItem != null && y >= _selectedItem.PosY1 && y < _selectedItem.PosY2 && s + l > _selectedItem.PosX1 && s < _selectedItem.PosX2)
                    {
                        var st = Math.Max(0, _selectedItem.PosX1 - s);
                        str = str.ColorSubstring(st, Math.Min(_selectedItem.PosX2 - _selectedItem.PosX1, l - st), ConsoleColor.White, _mode == EditMode.Moving ? ConsoleColor.DarkRed : ConsoleColor.DarkBlue);
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
                    sb.Append(" │║─└╙═╘╚││║┌├╟╒╞╠║║║╓╟╟╔╠╠─┘╜─┴╨═╧╩┐┤╢┬┼╫╤╪╬╖╢╢╥╫╫╦╬╬═╛╝═╧╩═╧╩╕╡╣╤╪╬╤╪╬╗╣╣╦╬╬╦╬╬?????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????"[
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
                foreach (var area in box.TextAreas)
                    foreach (var line in area)
                        _fileCharsCache[line.Y] = _fileCharsCache[line.Y].Substring(0, line.X) + line.Content + _fileCharsCache[line.Y].Substring(line.X + line.Content.Length);
        }

        public static void Invalidate(Item item)
        {
            if (item != null)
                invalidate(item.PosX1, item.PosY1, item.PosX2, item.PosY2);
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
            _horizScroll.Max = _file.Items.Max(i => i.PosX2) + 1;
            _vertScroll.Max = _file.Items.Max(i => i.PosY2) + 1;
        }

        static bool canDestroy()
        {
            return false;
        }

        static void move(Direction dir)
        {
            if (_selectedItem == null)
                return;

            if ((dir == Direction.Up || dir == Direction.Down) && (_selectedItem is VLine))
                return;
            if ((dir == Direction.Right || dir == Direction.Left) && (_selectedItem is HLine))
                return;

            var modifications = new List<Modification>();
            if (tryMove(_selectedItem, dir, modifications))
                foreach (var modification in modifications)
                    modification.Make();
        }

        static bool tryMove(Item item, Direction dir, List<Modification> mods)
        {
            var already = mods.OfType<MoveItem>().FirstOrDefault(m => m.Item == item);
            if (already != null)
                return already.Direction == dir;

            mods.Add(new MoveItem(item, dir));

            var box = item as Box;
            var vline = item as VLine;
            var hline = item as HLine;

            var xOff = dir == Direction.Left ? -1 : dir == Direction.Right ? 1 : 0;
            var yOff = dir == Direction.Up ? -1 : dir == Direction.Down ? 1 : 0;

            foreach (var other in _file.Items)
            {
                if (other == item)
                    continue;

                // See what’s in the way
                switch (dir)
                {
                    case Direction.Up:
                        if (!(other is VLine))
                            if (other.PosY2 > item.PosY1 && other.PosY2 <= item.PosY1 + 1 && other.PosX2 > item.PosX1 && other.PosX1 < item.PosX2)
                                if (!tryMove(other, Direction.Up, mods))
                                    return false;
                        break;

                    case Direction.Down:
                        if (!(other is VLine))
                            if (other.PosY1 < item.PosY2 && other.PosY1 >= item.PosY2 - 1 && other.PosX2 > item.PosX1 && other.PosX1 < item.PosX2)
                                if (!tryMove(other, Direction.Down, mods))
                                    return false;
                        break;

                    case Direction.Left:
                        if (!(other is HLine))
                            if (other.PosX2 > item.PosX1 - _fileOptions.HSpacing && other.PosX2 <= item.PosX1 + 1 && other.PosY2 > item.PosY1 && other.PosY1 < item.PosY2)
                                if (!tryMove(other, Direction.Left, mods))
                                    return false;
                        break;

                    case Direction.Right:
                        if (!(other is HLine))
                            if (other.PosX1 < item.PosX2 + _fileOptions.HSpacing && other.PosX1 >= item.PosX2 - 1 && other.PosY2 > item.PosY1 && other.PosY1 < item.PosY2)
                                if (!tryMove(other, Direction.Right, mods))
                                    return false;
                        break;
                }

                if (box != null)
                {
                    // Move all the lines inside of the box
                    if (other.PosX1 < box.PosX2 - xOff - 1 && other.PosX2 > box.PosX1 - xOff + 1 && other.PosY1 < box.PosY2 - yOff - 1 && other.PosY2 > box.PosY1 - yOff + 1)
                        mods.Add(new MoveItem(other, dir));

                    // Lines attached to the right
                    else if (other.PosY1 >= box.PosY1 - yOff && other.PosY2 <= box.PosY2 - yOff && other.PosX1 == box.PosX2 - xOff - 1)
                    {
                        if (!tryAdjust(other, Direction.Left, dir, mods))
                            return false;
                    }

                    // Lines attached to the left
                    else if (other.PosY1 >= box.PosY1 - yOff && other.PosY2 <= box.PosY2 - yOff && other.PosX2 == box.PosX1 - xOff + 1)
                    {
                        if (!tryAdjust(other, Direction.Right, dir, mods))
                            return false;
                    }

                    // Lines attached above
                    else if (other.PosX1 >= box.PosX1 - xOff && other.PosX2 <= box.PosX2 - xOff && other.PosY2 == box.PosY1 - yOff + 1)
                    {
                        if (!tryAdjust(other, Direction.Down, dir, mods))
                            return false;
                    }

                    // Lines attached below
                    else if (other.PosX1 >= box.PosX1 - xOff && other.PosX2 <= box.PosX2 - xOff && other.PosY1 == box.PosY2 - yOff - 1)
                    {
                        if (!tryAdjust(other, Direction.Up, dir, mods))
                            return false;
                    }
                }
                else if (vline != null)
                {
                    // Lines attached to the right
                    if (other.PosY1 >= vline.PosY1 && other.PosY2 <= vline.PosY2 && other.PosX1 == vline.PosX1)
                    {
                        if (!tryAdjust(other, Direction.Left, dir, mods))
                            return false;
                    }

                   // Lines attached to the left
                    else if (other.PosY1 >= vline.PosY1 && other.PosY2 <= vline.PosY2 && other.PosX2 == vline.PosX2)
                    {
                        if (!tryAdjust(other, Direction.Right, dir, mods))
                            return false;
                    }
                }
                else if (hline != null)
                {
                    // Lines attached above
                    if (other.PosX1 >= hline.PosX1 && other.PosX2 <= hline.PosX2 && other.PosY2 == hline.PosY2)
                    {
                        if (!tryAdjust(other, Direction.Down, dir, mods))
                            return false;
                    }

                    // Lines attached below
                    else if (other.PosX1 >= hline.PosX1 && other.PosX2 <= hline.PosX2 && other.PosY1 == hline.PosY1)
                    {
                        if (!tryAdjust(other, Direction.Up, dir, mods))
                            return false;
                    }
                }
            }
            return true;
        }

        private static bool tryAdjust(Item item, Direction end, Direction dir, List<Modification> mods)
        {
            Ut.Assert(!(item is Box));

            var already = mods.OfType<AdjustEnd>().FirstOrDefault(m => m.Item == item && m.End == end);
            if (already != null)
                return already.Direction == dir;

            // We are making the line longer.
            if (end == dir)
            {
                mods.Add(new AdjustEnd(item, end, dir));
                return true;
            }

            return item.IfType(
                (VLine vline) =>
                {
                    if (end == Direction.Left || end == Direction.Right)
                        throw new InvalidOperationException("Unexpected vline edge.");

                    if (dir == Direction.Up || dir == Direction.Down)
                    {
                        Ut.Assert(end == dir.Opposite());
                        mods.Add(new AdjustEnd(item, end, dir));
                        if (item.PosY1 == item.PosY2 - 2)
                        {
                            if (!tryAdjust(item, end.Opposite(), dir, mods))
                                return false;
                        }
                        else
                        {
                            foreach (var other in _file.Items)
                            {
                                if (other is HLine && (dir == Direction.Up ? other.PosY2 == item.PosY2 - 1 : other.PosY1 == item.PosY1 + 1))
                                {
                                    if (other.PosX1 == item.PosX1)
                                    {
                                        if (!tryAdjust(other, Direction.Left, dir, mods))
                                            return false;
                                    }
                                    else if (other.PosX2 == item.PosX2)
                                    {
                                        if (!tryAdjust(other, Direction.Right, dir, mods))
                                            return false;
                                    }
                                }
                            }
                        }
                    }
                    else    // dir == Left || Right
                    {
                        // We are trying to move the end of the line sideways.

                        // If there is something in the way of moving the end (i.e. no space to add a kink), we have to move that.
                        var push = Ut.Lambda((Item other) =>
                        {
                            if (other is VLine && (other.PosY1 == (end == Direction.Up ? item.PosY1 : item.PosY2 - 2) || other.PosY1 == (end == Direction.Up ? item.PosY1 + 1 : item.PosY2 - 1)))
                            {
                                if (!tryAdjust(other, Direction.Up, dir, mods))
                                    return false;
                            }
                            else if (other is VLine && (other.PosY2 == (end == Direction.Up ? item.PosY1 + 1 : item.PosY2 - 1) || other.PosY1 == (end == Direction.Up ? item.PosY1 + 2 : item.PosY2)))
                            {
                                if (!tryAdjust(other, Direction.Down, dir, mods))
                                    return false;
                            }
                            else if (other is HLine)
                            {
                                if (!tryAdjust(other, dir.Opposite(), dir, mods))
                                    return false;
                            }
                            else
                            {
                                if (!tryMove(other, dir, mods))
                                    return false;
                            }
                        });

                        var inTheWayAtEnd = _file.Items.Where(other =>
                            (dir == Direction.Left ? (other.PosX2 <= item.PosX1 && other.PosX2 >= item.PosX1 - _fileOptions.HSpacing) : (other.PosX1 >= item.PosX2 && other.PosX1 <= item.PosX2 + _fileOptions.HSpacing)) &&
                            (end == Direction.Up ? (other.PosY1 <= item.PosY1 + 1 && other.PosY2 > item.PosY1) : (other.PosY1 < item.PosY2 && other.PosY2 >= item.PosY2 - 1))
                        ).ToArray();
                        foreach (var other in inTheWayAtEnd)
                            if (!push(other))
                                return false;

                        // What else is in the way?
                        var inTheWay = _file.Items.Where(other =>
                            (dir == Direction.Left ? (other.PosX2 <= item.PosX1 && other.PosX2 >= item.PosX1 - _fileOptions.HSpacing) : (other.PosX1 >= item.PosX2 && other.PosX1 <= item.PosX2 + _fileOptions.HSpacing)) &&
                            other.PosY2 > item.PosY1 && other.PosY1 < item.PosY2 &&
                            !inTheWayAtEnd.Contains(other)
                        ).ToArray();

                        // Find all the perpendicular lines
                        var perpendicular = _file.Items.OfType<HLine>().Where(h => h.Y >= vline.Y1 && h.Y < vline.Y2 && (h.X1 == vline.X || h.X2 == vline.X)).ToArray();

                        // Find the first place where we could add a kink
                        var gap = Enumerable.Range(1, vline.Y2 - vline.Y1 - 1)
                            .Select(yoff => end == Direction.Up ? vline.Y1 + yoff : vline.Y2 - yoff)
                            .Where(y => !perpendicular.Any(p => p.Y == y) && !inTheWay.Any(itw => y >= itw.PosY1 && y < itw.PosY2))
                            .FirstOrNull();

                        if (inTheWay.Length == 0 || gap == null)
                        {
                            // If nothing is in the way, OR there is no gap, move the whole line
                            mods.Add(new MoveItem(item, dir));
                            foreach (var other in inTheWay)
                                if (!push(other))
                                    return false;
                            foreach (var other in perpendicular)
                                if (!tryAdjust(other, other.X1 == vline.X ? Direction.Left : Direction.Right, dir, mods))
                                    return false;
                            return true;
                        }
                        else
                        {
                            // Add a kink
                            throw new NotImplementedException();
                        }
                    }
                    return false;
                },
                (HLine hline) =>
                {
                    if (end == Direction.Up || end == Direction.Down)
                        throw new InvalidOperationException("Unexpected hline edge.");
                    throw new NotImplementedException();
                },
                @else =>
                {
                    throw new InvalidOperationException("Unexpected type of item.");
                });
        }
    }
}
