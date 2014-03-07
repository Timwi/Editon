﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editon
{
    sealed class SourceAsChars
    {
        public char[][] Chars { get; private set; }

        public SourceAsChars(char[][] chars) { Chars = chars; }

        public LineType TopLine(int x, int y)
        {
            return
                y < 0 || y >= Chars.Length || x < 0 || x >= Chars[y].Length ? LineType.None :
                "│└┘├┤┴╛╘╡╧┼╞╪".Contains(Chars[y][x]) ? LineType.Single :
                "║╚╝╠╣╩╜╙╢╨╬╟╫".Contains(Chars[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType LeftLine(int x, int y)
        {
            return
                y < 0 || y >= Chars.Length || x < 0 || x >= Chars[y].Length ? LineType.None :
                "─┐┘┤┬┴╜╖╢╨╥╫┼".Contains(Chars[y][x]) ? LineType.Single :
                "═╗╝╣╦╩╛╕╡╧╤╪╬".Contains(Chars[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType RightLine(int x, int y)
        {
            return
                y < 0 || y >= Chars.Length || x < 0 || x >= Chars[y].Length ? LineType.None :
                "─└┌├┬┴╓╙╨╟╥╫┼".Contains(Chars[y][x]) ? LineType.Single :
                "═╚╔╠╦╩╒╘╧╞╤╪╬".Contains(Chars[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType BottomLine(int x, int y)
        {
            return
                y < 0 || y >= Chars.Length || x < 0 || x >= Chars[y].Length ? LineType.None :
                "│┌┐├┤┬╒╕╡╞╤╪┼".Contains(Chars[y][x]) ? LineType.Single :
                "║╔╗╠╣╦╓╖╢╟╥╫╬".Contains(Chars[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType Line(int x, int y, Direction dir)
        {
            switch (dir)
            {
                case Direction.Up: return TopLine(x, y);
                case Direction.Right: return RightLine(x, y);
                case Direction.Down: return BottomLine(x, y);
                case Direction.Left: return LeftLine(x, y);
            }
            throw new ArgumentException("Invalid direction.", "dir");
        }
        public bool AnyLine(int x, int y)
        {
            return "─│┌┐└┘├┤┬┴┼═║╒╓╔╕╖╗╘╙╚╛╜╝╞╟╠╡╢╣╤╥╦╧╨╩╪╫╬".Contains(Chars[y][x]);
        }
        public int Width { get { return Chars[0].Length; } }
        public int Height { get { return Chars.Length; } }
    }
}
