using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RT.Util;
using RT.Util.Collections;
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
            var edgeFromBox = Ut.NewArray<bool>(source.Width, source.Height);

            var __debug_output = Ut.Lambda(() =>
            {
                var strings = new AutoList<ConsoleColoredString>();
                var putChar = Ut.Lambda((ConsoleColoredString input, int index, char ch) => input == null ? new string(' ', index) + ch : index >= input.Length ? input + new string(' ', index - input.Length) + ch.ToString() : input.Substring(0, index) + ch.ToString() + input.Substring(index + 1));
                foreach (var box in result.Items.OfType<Box>())
                {
                    for (int x = box.X; x <= box.X + box.Width; x++)
                        for (int y = box.Y; y <= box.Y + box.Height; y++)
                            strings[y] = putChar(strings[y], x, '·');
                }
                foreach (var n in result.Items.OfType<NonBoxItem>())
                    strings[n.Y] = putChar(strings[n.Y], n.X, n is Node ? 'N' : 'E');
                for (int y = 0; y < source.Height; y++)
                {
                    for (int x = 0; x < source.Width; x++)
                        if (visited[x][y])
                        {
                            if (strings[y] == null || strings[y].Length <= x)
                                strings[y] = putChar(strings[y], x, ' ');
                            strings[y] = strings[y].ColorSubstringBackground(x, 1, ConsoleColor.DarkRed);
                        }
                    ConsoleUtil.WriteLine(strings[y]);
                }
                System.Diagnostics.Debugger.Break();
            });

            // Find boxes
            var lineStarts = new List<Tuple<int, int, Direction, LineType>>();
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    // Go looking for a box only if this is a top-left corner of a box
                    if (source.TopLine(x, y) != LineType.None || source.LeftLine(x, y) != LineType.None || source.RightLine(x, y) == LineType.None || source.BottomLine(x, y) == LineType.None)
                        continue;

                    if (visited[x][y])
                        continue;

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
                        LineTypes = Helpers.MakeDictionary(top, right, bottom, left)
                    });

                    for (int xx = 0; xx <= width; xx++)
                    {
                        visited[x + xx][y] = true;
                        visited[x + xx][y + height] = true;
                    }
                    for (int yy = 0; yy <= height; yy++)
                    {
                        visited[x][y + yy] = true;
                        visited[x + width][y + yy] = true;
                    }

                    // Search for lines starting along top and bottom
                    for (int i = x + 1; i < x + width; i++)
                    {
                        LineType topOut = source.TopLine(i, y), topIn = source.BottomLine(i, y);
                        if (topOut != LineType.None || topIn != LineType.None)
                        {
                            result.Items.Add(new Node { X = i, Y = y, LineTypes = Helpers.MakeDictionary(topOut, LineType.None, topIn, LineType.None) });
                            edgeFromBox[i][y] = true;
                        }

                        LineType bottomOut = source.BottomLine(i, y + height), bottomIn = source.TopLine(i, y + height);
                        if (bottomOut != LineType.None || bottomIn != LineType.None)
                        {
                            result.Items.Add(new Node { X = i, Y = y + height, LineTypes = Helpers.MakeDictionary(bottomIn, LineType.None, bottomOut, LineType.None) });
                            edgeFromBox[i][y + height] = true;
                        }
                    }

                    // Search for outgoing edges along left and right
                    for (int j = y + 1; j < y + height; j++)
                    {
                        LineType leftOut = source.LeftLine(x, j), leftIn = source.RightLine(x, j);
                        if (leftOut != LineType.None || leftIn != LineType.None)
                        {
                            result.Items.Add(new Node { X = x, Y = j, LineTypes = Helpers.MakeDictionary(LineType.None, leftIn, LineType.None, leftOut) });
                            edgeFromBox[x][j] = true;
                        }

                        LineType rightOut = source.RightLine(x + width, j), rightIn = source.LeftLine(x + width, j);
                        if (rightOut != LineType.None || rightIn != LineType.None)
                        {
                            result.Items.Add(new Node { X = x + width, Y = j, LineTypes = Helpers.MakeDictionary(LineType.None, rightOut, LineType.None, rightIn) });
                            edgeFromBox[x + width][j] = true;
                        }
                    }
                }
            }

            // Find all other nodes
            for (int y = 0; y < source.Height; y++)
            {
                for (int x = 0; x < source.Width; x++)
                {
                    if (!visited[x][y])
                    {
                        var lineTypes = Helpers.MakeDictionary(source.TopLine(x, y), source.RightLine(x, y), source.BottomLine(x, y), source.LeftLine(x, y));
                        var isNode = false;
                        foreach (var dir in Helpers.Directions)
                            if (lineTypes[dir] != LineType.None && lineTypes[dir.Clockwise()] != LineType.None)
                            {
                                isNode = true;
                                break;
                            }
                        if (isNode)
                            result.Items.Add(new Node { X = x, Y = y, LineTypes = lineTypes });
                    }
                    if (!visited[x][y] || edgeFromBox[x][y])
                    {
                        foreach (var dir in Helpers.Directions)
                        {
                            var l = source.Line(x, y, dir);
                            if (l != LineType.None && source.Line(x + dir.XOffset(), y + dir.YOffset(), dir.Opposite()) != l)
                                result.Items.Add(new LineEnd { X = x, Y = y, Direction = dir.Opposite(), LineType = l });
                        }
                    }
                }
            }

            // Join up all the nodes
            foreach (var item in result.Items.OfType<NonBoxItem>())
            {
                if (item.IfType((Node n) => n.LineTypes[Direction.Right] != LineType.None, (LineEnd e) => e.Direction == Direction.Right, otherwise => false))
                {
                    var minX = item is Node ? item.X + 1 : item.X;
                    var other = result.Items.OfType<Node>().Where(n => n.X >= minX && n.Y == item.Y).MinElementOrDefault(n => n.X);
                    var otherEnd = result.Items.OfType<LineEnd>().Where(e => e.Direction == Direction.Left && e.X >= item.X && e.Y == item.Y).MinElementOrDefault(e => e.X);
                    if (other != null && (otherEnd == null || otherEnd.X >= other.X))
                    {
                        item.IfType(
                            (Node n) => { n.JoinUpWith[Direction.Right] = other; },
                            (LineEnd e) => { e.JoinUpWith = other; });
                        other.JoinUpWith[Direction.Left] = item;
                    }
                    else if (otherEnd != null && (other == null || other.X > otherEnd.X))
                    {
                        item.IfType(
                            (Node n) => { n.JoinUpWith[Direction.Right] = otherEnd; },
                            (LineEnd e) => { e.JoinUpWith = otherEnd; });
                        otherEnd.JoinUpWith = item;
                    }
                    else
                        throw new InvalidOperationException("Things don’t join up.");
                }
                if (item.IfType((Node n) => n.LineTypes[Direction.Down] != LineType.None, (LineEnd e) => e.Direction == Direction.Down, otherwise => false))
                {
                    var minY = item is Node ? item.Y + 1 : item.Y;
                    var other = result.Items.OfType<Node>().Where(n => n.Y >= minY && n.X == item.X).MinElementOrDefault(n => n.Y);
                    var otherEnd = result.Items.OfType<LineEnd>().Where(e => e.Direction == Direction.Up && e.Y >= item.Y && e.X == item.X).MinElementOrDefault(e => e.Y);
                    if (other != null && (otherEnd == null || otherEnd.Y >= other.Y))
                    {
                        item.IfType(
                            (Node n) => { n.JoinUpWith[Direction.Down] = other; },
                            (LineEnd e) => { e.JoinUpWith = other; });
                        other.JoinUpWith[Direction.Up] = item;
                    }
                    else if (otherEnd != null && (other == null || other.Y > otherEnd.Y))
                    {
                        item.IfType(
                            (Node n) => { n.JoinUpWith[Direction.Down] = otherEnd; },
                            (LineEnd e) => { e.JoinUpWith = otherEnd; });
                        otherEnd.JoinUpWith = item;
                    }
                    else
                        throw new InvalidOperationException("Things don’t join up.");
                }
            }

            // We need _hasLineCache for the following
            bool[][] hasLine;
            getFileChars(result.Items, out hasLine, ignoreTextAreas: true);

            // Determine the location of text lines within every box
            foreach (var box in result.Items.OfType<Box>())
            {
                var curTextLines = new HashSet<TextLine>();
                for (int by = 1; by < box.Height; by++)
                {
                    var y = box.Y + by;
                    TextLine curTextLine = null;
                    var curLineText = new StringBuilder();
                    for (int bx = 1; bx < box.Width; bx++)
                    {
                        var x = box.X + bx;

                        if (hasLine[x][y])
                        {
                            if (curTextLine != null)
                            {
                                curTextLine.Content = curLineText.ToString();
                                curTextLines.Add(curTextLine);
                                curTextLine = null;
                                curLineText.Clear();
                            }
                        }
                        else
                        {
                            if (curTextLine == null)
                                curTextLine = new TextLine { X = x, Y = y };
                            curLineText.Append(source.Chars[y][x]);
                        }
                    }
                    if (curTextLine != null)
                    {
                        curTextLine.Content = curLineText.ToString();
                        curTextLines.Add(curTextLine);
                    }
                }

                // Group text lines by vertical adjacency
                var textAreas = new List<TextLine[]>();
                while (curTextLines.Count > 0)
                {
                    var first = curTextLines.First();
                    curTextLines.Remove(first);
                    var curGroup = new List<TextLine> { first };
                    while (true)
                    {
                        var next = curTextLines.FirstOrDefault(one => curGroup.Any(two => (one.Y == two.Y + 1 || one.Y == two.Y - 1) && one.X + one.Content.Length > two.X && one.X < two.X + two.Content.Length));
                        if (next == null)
                            break;
                        curGroup.Add(next);
                        curTextLines.Remove(next);
                    }
                    curGroup.Sort(CustomComparer<TextLine>.By(l => l.Y).ThenBy(l => l.X));
                    textAreas.Add(curGroup.ToArray());
                }
                box.TextAreas = textAreas.ToArray();
            }

            return result;
        }
    }
}
