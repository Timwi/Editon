using System;
using System.Collections.Generic;
using System.Linq;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace Editon
{
    partial class EditonProgram
    {
        static readonly KeyBinding[] _keyBindingsRaw = Ut.NewArray
        (
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.Spacebar, enterMoveMode),

            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.UpArrow, moveCursorUp),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.RightArrow, moveCursorRight),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.DownArrow, moveCursorDown),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.LeftArrow, moveCursorLeft),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.Home, moveCursorHome),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.End, moveCursorEnd),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.PageUp, moveCursorPageUp),
            new KeyBinding(EditMode.Cursor, 0, ConsoleKey.PageDown, moveCursorPageDown),

            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.Q, exit),

            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.UpArrow, moveCursorUpFar),
            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.RightArrow, moveCursorRightFar),
            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.DownArrow, moveCursorDownFar),
            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.LeftArrow, moveCursorLeftFar),
            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.Home, moveCursorHomeFar),
            new KeyBinding(EditMode.Cursor, ConsoleModifiers.Control, ConsoleKey.End, moveCursorEndFar),


            new KeyBinding(EditMode.Moving, 0, ConsoleKey.Escape, leaveMoveMode),
            new KeyBinding(EditMode.Moving, 0, ConsoleKey.UpArrow, moveUp),
            new KeyBinding(EditMode.Moving, 0, ConsoleKey.RightArrow, moveRight),
            new KeyBinding(EditMode.Moving, 0, ConsoleKey.DownArrow, moveDown),
            new KeyBinding(EditMode.Moving, 0, ConsoleKey.LeftArrow, moveLeft)
        );


        // ────────────────────────────────────────────────────────────────────────────

        static Dictionary<EditMode, Dictionary<ConsoleKey, Dictionary<ConsoleModifiers, Action>>> _keyBindingsCache;
        static Dictionary<EditMode, Dictionary<ConsoleKey, Dictionary<ConsoleModifiers, Action>>> KeyBindings
        {
            get
            {
                if (_keyBindingsCache == null)
                {
                    _keyBindingsCache = _keyBindingsRaw
                        .GroupBy(binding => binding.Mode)
                        .ToDictionary(gr => gr.Key, gr => gr
                            .GroupBy(binding => binding.Key)
                            .ToDictionary(gr2 => gr2.Key, gr2 => gr2.ToDictionary(binding => binding.Modifiers, binding => binding.Action)));
                }
                return _keyBindingsCache;
            }
        }

        private static void PostBuildCheck(IPostBuildReporter rep)
        {
            foreach (var pair in _keyBindingsRaw.UniquePairs())
            {
                if (pair.Item1.Mode == pair.Item2.Mode && pair.Item1.Key == pair.Item2.Key && pair.Item1.Modifiers == pair.Item2.Modifiers)
                {
                    var tok = "EditMode.{0}, {1}, ConsoleKey.{2}".Fmt(pair.Item1.Mode, pair.Item1.Modifiers == 0 ? "0" : "ConsoleModifiers." + pair.Item1.Modifiers, pair.Item1.Key);
                    rep.Error(@"There are two key bindings for {0} in mode {1}.".Fmt(
                        pair.Item1.Modifiers == 0 ? pair.Item1.Key.ToString() : pair.Item1.Modifiers + "+" + pair.Item1.Key,
                        pair.Item1.Mode
                    ), "_keyBindingsRaw", tok);
                    rep.Error(@"    -- Second use is here.", "_keyBindingsRaw", tok, tok);
                }
            }
        }
    }

    sealed class KeyBinding
    {
        public EditMode Mode { get; private set; }
        public ConsoleModifiers Modifiers { get; private set; }
        public ConsoleKey Key { get; private set; }
        public Action Action { get; private set; }
        public KeyBinding(EditMode mode, ConsoleModifiers modifiers, ConsoleKey key, Action action)
        {
            Mode = mode;
            Modifiers = modifiers;
            Key = key;
            Action = action;
        }
    }
}
