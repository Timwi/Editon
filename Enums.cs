using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editon
{
    enum EditMode
    {
        Cursor,
        EditingBoxText,
        Moving
    }

    enum LineType
    {
        None = 0,
        Single = 1,
        Double = 2
    }

    enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3
    }

    [Flags]
    enum LineChars
    {
        TopNone = LineType.None << Direction.Up,
        TopSingle = 1,
        TopDouble = 2,

        RightNone = 0 << 2,
        RightSingle = 1 << 2,
        RightDouble = 2 << 2,

        BottomNone = 0 << 4,
        BottomSingle = 1 << 4,
        BottomDouble = 2 << 4,

        LeftNone = 0 << 6,
        LeftSingle = 1 << 6,
        LeftDouble = 2 << 6,

        TopMask = 3,
        RightMask = 3 << 2,
        BottomMask = 3 << 4,
        LeftMask = 3 << 6
    }
}
