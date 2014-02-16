using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util;

namespace Editon
{
    partial class EditonProgram
    {
        static readonly Dictionary<ConsoleKey, Dictionary<ConsoleModifiers, Action>> KeyBindings = Ut.NewArray
        (
            Tuple.Create<ConsoleKey, ConsoleModifiers, Action>(ConsoleKey.DownArrow, 0, () => moveCursorVert(up: false)),
            Tuple.Create<ConsoleKey, ConsoleModifiers, Action>(ConsoleKey.UpArrow, 0, () => moveCursorVert(up: true)),
            Tuple.Create<ConsoleKey, ConsoleModifiers, Action>(ConsoleKey.RightArrow, 0, () => moveCursorHoriz(left: false)),
            Tuple.Create<ConsoleKey, ConsoleModifiers, Action>(ConsoleKey.LeftArrow, 0, () => moveCursorHoriz(left: true)),
            Tuple.Create<ConsoleKey, ConsoleModifiers, Action>(ConsoleKey.Q, ConsoleModifiers.Control, () => _exit = true)
        )
            .GroupBy(tup => tup.Item1)
            .ToDictionary(gr => gr.Key, gr => gr.ToDictionary(tup => tup.Item2, tup => tup.Item3));
    }
}
