using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RT.Util;
using RT.Util.Consoles;
using RT.Util.ExtensionMethods;

namespace Editon
{
    partial class EditonProgram
    {
        public static FncFile Parse(string sourceFile)
        {
            var sourceText = File.ReadAllText(sourceFile);
            var result = new FncFile();

            // Turn into array of characters
            var lines = (sourceText.Replace("\r", "") + "\n\n").Split('\n');
            if (lines.Length == 0)
                return result;

            var longestLine = lines.Max(l => l.Length);
            if (longestLine == 0)
                return result;

            var source = new SourceAsChars(lines.Select(l => l.PadRight(longestLine).ToCharArray()).ToArray());
            var visited = Ut.NewArray<bool>(source.Width, source.Height);

            Action<int, int, bool, LineType> processHLine = null;
            Action<int, int, bool, LineType> processVLine = null;
            processHLine = (int x, int y, bool toLeft, LineType lineType) =>
            {
                var startX = x;
                var vxs = new List<int>();
                while (x > 0 && x < source.Width - 1 && (toLeft ? source.LeftLine(x, y) : source.RightLine(x, y)) == lineType && (toLeft ? source.RightLine(x - 1, y) : source.LeftLine(x + 1, y)) == lineType)
                {
                    x += toLeft ? -1 : 1;
                    if (!visited[x][y] && (source.TopLine(x, y) != LineType.None || source.BottomLine(x, y) != LineType.None))
                        vxs.Add(x);
                    visited[x][y] = true;
                }
                result.Items.Add(new HLine { LineType = lineType, X1 = toLeft ? x : startX, X2 = toLeft ? startX : x, Y = y });
                foreach (var vx in vxs)
                {
                    var top = source.TopLine(vx, y);
                    if (top != LineType.None)
                        processVLine(vx, y, true, top);
                    var bottom = source.BottomLine(vx, y);
                    if (bottom != LineType.None)
                        processVLine(vx, y, false, bottom);
                }
            };
            processVLine = (int x, int y, bool up, LineType lineType) =>
            {
                var startY = y;
                var hys = new List<int>();
                while (y > 0 && y < source.Height - 1 && (up ? source.TopLine(x, y) : source.BottomLine(x, y)) == lineType && (up ? source.BottomLine(x, y - 1) : source.TopLine(x, y + 1)) == lineType)
                {
                    y += up ? -1 : 1;
                    if (!visited[x][y] && (source.LeftLine(x, y) != LineType.None || source.RightLine(x, y) != LineType.None))
                        hys.Add(y);
                    visited[x][y] = true;
                }
                result.Items.Add(new VLine { LineType = lineType, X = x, Y1 = up ? y : startY, Y2 = up ? startY : y });
                foreach (var hy in hys)
                {
                    var left = source.LeftLine(x, hy);
                    if (left != LineType.None)
                        processHLine(x, hy, true, left);
                    var right = source.RightLine(x, hy);
                    if (right != LineType.None)
                        processHLine(x, hy, false, right);
                }
            };

            // Find boxes
            var hLineStarts = new List<Tuple<int, int, bool, LineType>>();
            var vLineStarts = new List<Tuple<int, int, bool, LineType>>();
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    // Go looking for a box only if this is a top-left corner of a box
                    if (source.TopLine(x, y) != LineType.None || source.LeftLine(x, y) != LineType.None || source.RightLine(x, y) == LineType.None || source.BottomLine(x, y) == LineType.None)
                        continue;

                    if (visited[x][y])
                        continue;
                    visited[x][y] = true;

                    // Find width of box by walking along top edge
                    var top = source.RightLine(x, y);
                    var index = x + 1;
                    while (index < source.Width && source.RightLine(index, y) == top)
                        index++;
                    if (index == source.Width || source.BottomLine(index, y) == LineType.None || source.TopLine(index, y) != LineType.None || source.RightLine(index, y) != LineType.None)
                        continue;
                    var width = index - x;

                    // Find height of box by walking along left edge
                    var left = source.BottomLine(x, y);
                    index = y + 1;
                    while (index < source.Height && source.BottomLine(x, index) == left)
                        index++;
                    if (index == source.Height || source.RightLine(x, index) == LineType.None || source.LeftLine(x, index) != LineType.None || source.BottomLine(x, index) != LineType.None)
                        continue;
                    var height = index - y;

                    // Verify the bottom edge
                    var bottom = source.RightLine(x, y + height);
                    index = x + 1;
                    while (index < source.Width && source.RightLine(index, y + height) == bottom)
                        index++;
                    if (index == source.Width || source.TopLine(index, y + height) == LineType.None || source.BottomLine(index, y + height) != LineType.None || source.RightLine(index, y + height) != LineType.None)
                        continue;
                    if (index - x != width)
                        continue;

                    // Verify the right edge
                    var right = source.BottomLine(x + width, y);
                    index = y + 1;
                    while (index < source.Height && source.BottomLine(x + width, index) == right)
                        index++;
                    if (index == source.Height || source.LeftLine(x + width, index) == LineType.None || source.RightLine(x + width, index) != LineType.None || source.BottomLine(x + width, index) != LineType.None)
                        continue;
                    if (index - y != height)
                        continue;

                    // If all edges are single lines, this is not a box
                    if (top == LineType.Single && right == LineType.Single && bottom == LineType.Single && left == LineType.Single)
                        continue;

                    result.Items.Add(new Box
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        LineTypes = new Dictionary<LineLocation, LineType>(4) { { LineLocation.Top, top }, { LineLocation.Right, right }, { LineLocation.Bottom, bottom }, { LineLocation.Left, left } },
                        Content = Enumerable.Range(y + 1, height - 1).Select(i => new string(source.Chars[i].Subarray(x + 1, width - 1)).Trim()).JoinString("\n")
                    });

                    for (int xx = 0; xx <= width; xx++)
                        for (int yy = 0; yy <= height; yy++)
                            visited[x + xx][y + yy] = true;

                    // Search for outgoing edges along top and bottom
                    for (int i = x + 1; i < x + width; i++)
                    {
                        var topOut = source.TopLine(i, y);
                        if (topOut != LineType.None)
                            vLineStarts.Add(Tuple.Create(i, y, true, topOut));
                        var bottomOut = source.BottomLine(i, y + height);
                        if (bottomOut != LineType.None)
                            vLineStarts.Add(Tuple.Create(i, y + height, false, bottomOut));
                    }
                    // Search for outgoing edges along left and right
                    for (int i = y + 1; i < y + height; i++)
                    {
                        var leftOut = source.LeftLine(x, i);
                        if (leftOut != LineType.None)
                            hLineStarts.Add(Tuple.Create(x, i, true, leftOut));
                        var rightOut = source.RightLine(x + width, i);
                        if (rightOut != LineType.None)
                            hLineStarts.Add(Tuple.Create(x + width, i, false, rightOut));
                    }
                }
            }

            // Process all the lines (starting from the outgoing edges of each box)
            foreach (var hl in hLineStarts)
                processHLine(hl.Item1, hl.Item2, hl.Item3, hl.Item4);
            foreach (var vl in vLineStarts)
                processVLine(vl.Item1, vl.Item2, vl.Item3, vl.Item4);

            // Join up lines that are directly adjacent or overlapping
            var toRemove = new List<Item>();
            foreach (var pair in result.Items.OfType<HLine>().UniquePairs())
            {
                if (pair.Item1.Y != pair.Item2.Y)
                    continue;

                if (pair.Item1.X2 == pair.Item2.X1)
                    pair.Item1.X2 = pair.Item2.X2;
                else if (pair.Item1.X1 == pair.Item2.X2)
                    pair.Item1.X1 = pair.Item2.X1;
                else if (pair.Item1.X1 == pair.Item2.X1)
                    pair.Item1.X2 = Math.Max(pair.Item1.X2, pair.Item2.X2);
                else if (pair.Item1.X2 == pair.Item2.X2)
                    pair.Item1.X1 = Math.Min(pair.Item1.X1, pair.Item2.X1);
                else
                    continue;

                toRemove.Add(pair.Item2);
            }
            foreach (var pair in result.Items.OfType<VLine>().UniquePairs())
            {
                if (pair.Item1.X != pair.Item2.X)
                    continue;

                if (pair.Item1.Y2 == pair.Item2.Y1)
                    pair.Item1.Y2 = pair.Item2.Y2;
                else if (pair.Item1.Y1 == pair.Item2.Y2)
                    pair.Item1.Y1 = pair.Item2.Y1;
                else if (pair.Item1.Y1 == pair.Item2.Y1)
                    pair.Item1.Y2 = Math.Max(pair.Item1.Y2, pair.Item2.Y2);
                else if (pair.Item1.Y2 == pair.Item2.Y2)
                    pair.Item1.Y1 = Math.Min(pair.Item1.Y1, pair.Item2.Y1);
                else
                    continue;

                toRemove.Add(pair.Item2);
            }
            result.Items.RemoveRange(toRemove);
            return result;
        }
    }
}
