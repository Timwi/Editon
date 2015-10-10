using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace Editon
{
    partial class EditonProgram
    {
        public static FncFile Parse(string sourceFile)
        {
            var sourceText = File.ReadAllText(sourceFile);
            var boxes = new List<Box>();

            // Turn into array of characters
            var lines = (sourceText.Replace("\r", "") + "\n\n").Split('\n');
            var source = new SourceAsChars(lines);

            if (lines.Length == 0)
                return new FncFile { Boxes = boxes, Source = source };

            var visited = new Dictionary<int, HashSet<int>>();

            // Find boxes
            for (int y = 0; y < source.NumLines; y++)
            {
                for (int x = 0; x < source[y].Length; x++)
                {
                    // Go looking for a box only if this is a top-left corner of a box
                    if (source.TopLine(x, y) != LineType.None || source.LeftLine(x, y) != LineType.None || source.RightLine(x, y) == LineType.None || source.BottomLine(x, y) == LineType.None)
                        continue;

                    if (visited.Contains(x, y))
                        continue;

                    // Find width of box by walking along top edge
                    var top = source.RightLine(x, y);
                    var index = x + 1;
                    while (index < source[y].Length && source.RightLine(index, y) == top)
                        index++;
                    if (index == source[y].Length || source.BottomLine(index, y) == LineType.None || source.TopLine(index, y) != LineType.None || source.RightLine(index, y) != LineType.None)
                        continue;
                    var width = index - x;

                    // Find height of box by walking along left edge
                    var left = source.BottomLine(x, y);
                    index = y + 1;
                    while (index < source.NumLines && source.BottomLine(x, index) == left)
                        index++;
                    if (index == source.NumLines || source.RightLine(x, index) == LineType.None || source.LeftLine(x, index) != LineType.None || source.BottomLine(x, index) != LineType.None)
                        continue;
                    var height = index - y;

                    // Verify the bottom edge
                    var bottom = source.RightLine(x, y + height);
                    index = x + 1;
                    while (index < source[y].Length && source.RightLine(index, y + height) == bottom)
                        index++;
                    if (index == source[y].Length || source.TopLine(index, y + height) == LineType.None || source.BottomLine(index, y + height) != LineType.None || source.RightLine(index, y + height) != LineType.None)
                        continue;
                    if (index - x != width)
                        continue;

                    // Verify the right edge
                    var right = source.BottomLine(x + width, y);
                    index = y + 1;
                    while (index < source.NumLines && source.BottomLine(x + width, index) == right)
                        index++;
                    if (index == source.NumLines || source.LeftLine(x + width, index) == LineType.None || source.RightLine(x + width, index) != LineType.None || source.BottomLine(x + width, index) != LineType.None)
                        continue;
                    if (index - y != height)
                        continue;

                    // If all edges are single lines, this is not a box
                    if (top == LineType.Single && right == LineType.Single && bottom == LineType.Single && left == LineType.Single)
                        continue;

                    for (int xx = 0; xx <= width; xx++)
                    {
                        visited.AddSafe(x + xx, y);
                        visited.AddSafe(x + xx, y + height);
                    }
                    for (int yy = 0; yy <= height; yy++)
                    {
                        visited.AddSafe(x, y + yy);
                        visited.AddSafe(x + width, y + yy);
                    }

                    boxes.Add(new Box
                    {
                        X = x,
                        Y = y,
                        Width = width,
                        Height = height,
                        LineTypes = Helpers.MakeDictionary(top, right, bottom, left)
                    });
                }
            }

            // Determine the location of text lines within every box
            foreach (var box in boxes)
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

                        if (source.AnyLine(x, y))
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
                            curLineText.Append(source[y][x]);
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

            return new FncFile { Boxes = boxes, Source = source };
        }
    }
}
