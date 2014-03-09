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

        static EditMode _mode;
        static int _cursorX, _cursorY;
        static Box _selectedBox;
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
                Invalidate(0, 0, Console.BufferWidth, Console.BufferHeight);
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
            var prevSel = _selectedBox;
            var prevEditingTextAreaIndex = _selectedBoxTextAreaIndex;

            _cursorX = x;
            _cursorY = y;

            _selectedBox = _file.Boxes.FirstOrDefault(box => box.Contains(_cursorX, _cursorY));
            _selectedBoxTextAreaIndex = _selectedBox == null ? -1 : _selectedBox.TextAreas.IndexOf(area => area.Any(line => _cursorY == line.Y && _cursorX >= line.X && _cursorX < line.X + line.Content.Length));

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

            if (_selectedBox != prevSel || _selectedBoxTextAreaIndex != prevEditingTextAreaIndex)
            {
                Invalidate(prevSel);
                Invalidate(_selectedBox);
            }
            Invalidate(prevX - 1, prevY - 1, prevX + 1, prevY + 1);
            Invalidate(_cursorX - 1, _cursorY - 1, _cursorX + 1, _cursorY + 1);
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

                var str = _file.Source[y].SubstringSafe(s, l).PadRight(l).Color(ConsoleColor.Gray);
                if (_selectedBox != null && y >= _selectedBox.Y && y < _selectedBox.Y2 && s + l > _selectedBox.X && s < _selectedBox.X2)
                {
                    var st = Math.Max(0, _selectedBox.X - s);
                    str = str.ColorSubstring(st, Math.Min(_selectedBox.X2 - _selectedBox.X, l - st), ConsoleColor.White, _mode == EditMode.Moving ? ConsoleColor.DarkRed : ConsoleColor.DarkBlue);
                }
                if (_mode == EditMode.Cursor)
                {
                    if (_selectedBox != null && _selectedBoxTextAreaIndex != -1)
                        foreach (var line in ((Box) _selectedBox).TextAreas[_selectedBoxTextAreaIndex])
                            if (line.Y == y && line.X < s + l && line.X + line.Content.Length >= s)
                            {
                                var st = Math.Max(0, line.X - s);
                                str = str.ColorSubstringBackground(st, Math.Min(line.Content.Length, l - st), ConsoleColor.DarkCyan);
                            }
                    if (_cursorY == y && _cursorX >= s && _cursorX < s + l)
                        str = str.ColorSubstringBackground(_cursorX - s, 1, _selectedBoxTextAreaIndex != -1 ? ConsoleColor.Cyan : _selectedBox != null ? ConsoleColor.Blue : ConsoleColor.DarkBlue);
                }
                ConsoleUtil.Write(str);
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

        static void invalidateAll() { Invalidate(_horizScroll.Value, _vertScroll.Value, _horizScroll.Value + Console.BufferWidth, _vertScroll.Value + Console.BufferHeight); }

        public static void Invalidate(Item item)
        {
            if (item != null)
                Invalidate(item.X, item.Y, item.X2, item.Y2);
        }

        public static void Invalidate(int x1, int y1, int x2, int y2)
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

        static void updateAfterEdit()
        {
            _horizScroll.Max = _file.Boxes.MaxOrDefault(i => i.X2, 0) + 1;
            _vertScroll.Max = _file.Boxes.MaxOrDefault(i => i.Y2, 0) + 1;
        }

        static bool canDestroy()
        {
            return false;
        }

        static void move(Direction dir)
        {
            if (_selectedBox == null)
                return;

            //ensureFileChars();  // We need _hasLineCache to be up to date

            //var toMove = new HashSet<Item>();
            //if (tryMove(_selectedBox, dir, toMove))
            //    foreach (var item in toMove)
            //        item.Move(dir);
        }
    }
}
