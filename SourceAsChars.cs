using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editon
{
    sealed class SourceAsChars
    {
        private string[] _lines;

        public string this[int index] { get { return index < 0 || index >= _lines.Length ? "" : _lines[index]; } }

        public SourceAsChars(string[] lines)
        {
            if (lines == null)
                throw new ArgumentNullException("lines");
            _lines = lines;
        }

        public LineType TopLine(int x, int y)
        {
            return
                y < 0 || y >= _lines.Length || x < 0 || x >= _lines[y].Length ? LineType.None :
                "│└┘├┤┴╛╘╡╧┼╞╪".Contains(_lines[y][x]) ? LineType.Single :
                "║╚╝╠╣╩╜╙╢╨╬╟╫".Contains(_lines[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType LeftLine(int x, int y)
        {
            return
                y < 0 || y >= _lines.Length || x < 0 || x >= _lines[y].Length ? LineType.None :
                "─┐┘┤┬┴╜╖╢╨╥╫┼".Contains(_lines[y][x]) ? LineType.Single :
                "═╗╝╣╦╩╛╕╡╧╤╪╬".Contains(_lines[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType RightLine(int x, int y)
        {
            return
                y < 0 || y >= _lines.Length || x < 0 || x >= _lines[y].Length ? LineType.None :
                "─└┌├┬┴╓╙╨╟╥╫┼".Contains(_lines[y][x]) ? LineType.Single :
                "═╚╔╠╦╩╒╘╧╞╤╪╬".Contains(_lines[y][x]) ? LineType.Double : LineType.None;
        }
        public LineType BottomLine(int x, int y)
        {
            return
                y < 0 || y >= _lines.Length || x < 0 || x >= _lines[y].Length ? LineType.None :
                "│┌┐├┤┬╒╕╡╞╤╪┼".Contains(_lines[y][x]) ? LineType.Single :
                "║╔╗╠╣╦╓╖╢╟╥╫╬".Contains(_lines[y][x]) ? LineType.Double : LineType.None;
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
            return "─│┌┐└┘├┤┬┴┼═║╒╓╔╕╖╗╘╙╚╛╜╝╞╟╠╡╢╣╤╥╦╧╨╩╪╫╬".Contains(_lines[y][x]);
        }
        public int NumLines { get { return _lines.Length; } }
    }
}
