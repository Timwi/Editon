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
            new KeyBinding(0, ConsoleKey.UpArrow, moveCursorUp),
            new KeyBinding(0, ConsoleKey.RightArrow, moveCursorRight),
            new KeyBinding(0, ConsoleKey.DownArrow, moveCursorDown),
            new KeyBinding(0, ConsoleKey.LeftArrow, moveCursorLeft),
            new KeyBinding(0, ConsoleKey.Home, moveCursorHome),
            new KeyBinding(0, ConsoleKey.End, moveCursorEnd),
            new KeyBinding(0, ConsoleKey.PageUp, moveCursorPageUp),
            new KeyBinding(0, ConsoleKey.PageDown, moveCursorPageDown),
            
            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.Q, inf => { inf.Exit = true; }),

            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.UpArrow, moveCursorUpFar),
            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.RightArrow, moveCursorRightFar),
            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.DownArrow, moveCursorDownFar),
            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.LeftArrow, moveCursorLeftFar),
            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.Home, moveCursorHomeFar),
            new KeyBinding(ConsoleModifiers.Control, ConsoleKey.End, moveCursorEndFar)
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
        public ConsoleModifiers Modifiers { get; private set; }
        public ConsoleKey Key { get; private set; }
        public Action<KeyProcessingInfo> Action { get; private set; }
        public KeyBinding(ConsoleModifiers modifiers, ConsoleKey key, Action<KeyProcessingInfo> action)
        {
            Modifiers = modifiers;
            Key = key;
            Action = action;
        }
    }
}
