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
            new KeyBinding(ConsoleKey.UpArrow, 0, moveCursorUp),
            new KeyBinding(ConsoleKey.UpArrow, ConsoleModifiers.Control, moveCursorUpFar),
            new KeyBinding(ConsoleKey.RightArrow, 0, moveCursorRight),
            new KeyBinding(ConsoleKey.RightArrow, ConsoleModifiers.Control, moveCursorRightFar),
            new KeyBinding(ConsoleKey.DownArrow, 0, moveCursorDown),
            new KeyBinding(ConsoleKey.DownArrow, ConsoleModifiers.Control, moveCursorDownFar),
            new KeyBinding(ConsoleKey.LeftArrow, 0, moveCursorLeft),
            new KeyBinding(ConsoleKey.LeftArrow, ConsoleModifiers.Control, moveCursorLeftFar),

            new KeyBinding(ConsoleKey.End, 0, moveCursorRightEnd),
            new KeyBinding(ConsoleKey.Home, 0, moveCursorLeftEnd),

            new KeyBinding(ConsoleKey.Q, ConsoleModifiers.Control, inf => { inf.Exit = true; })
        );


        // ────────────────────────────────────────────────────────────────────────────

        static Dictionary<ConsoleKey, Dictionary<ConsoleModifiers, Action<KeyProcessingInfo>>> _keyBindingsCache;
        static Dictionary<ConsoleKey, Dictionary<ConsoleModifiers, Action<KeyProcessingInfo>>> KeyBindings
        {
            get
            {
                if (_keyBindingsCache == null)
                {
                    _keyBindingsCache = _keyBindingsRaw
                        .GroupBy(tup => tup.Key)
                        .ToDictionary(gr => gr.Key, gr => gr.ToDictionary(tup => tup.Modifiers, tup => tup.Action));
                }
                return _keyBindingsCache;
            }
        }

        private static void PostBuildCheck(IPostBuildReporter rep)
        {
            foreach (var pair in _keyBindingsRaw.UniquePairs())
            {
                if (pair.Item1.Key == pair.Item2.Key && pair.Item1.Modifiers == pair.Item2.Modifiers)
                {
                    var tok = "ConsoleKey.{0}, {1}".Fmt(pair.Item1.Key, pair.Item1.Modifiers == 0 ? "0" : "ConsoleModifiers." + pair.Item1.Modifiers);
                    rep.Error(@"There are two key bindings for {0}.".Fmt(
                        pair.Item1.Modifiers == 0 ? pair.Item1.Key.ToString() : pair.Item1.Modifiers + "+" + pair.Item1.Key
                    ), "_keyBindingsRaw", tok);
                    rep.Error(@"    -- Second use is here.".Fmt(
                        pair.Item1.Modifiers == 0 ? pair.Item1.Key.ToString() : pair.Item1.Modifiers + "+" + pair.Item1.Key
                    ), "_keyBindingsRaw", tok, tok);
                }
            }
        }
    }

    sealed class KeyBinding
    {
        public ConsoleKey Key { get; private set; }
        public ConsoleModifiers Modifiers { get; private set; }
        public Action<KeyProcessingInfo> Action { get; private set; }
        public KeyBinding(ConsoleKey key, ConsoleModifiers modifiers, Action<KeyProcessingInfo> action)
        {
            Key = key;
            Modifiers = modifiers;
            Action = action;
        }
    }
}
