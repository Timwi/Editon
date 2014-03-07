using System;
using System.Collections.Generic;
using RT.Util.Collections;
using RT.Util.ExtensionMethods;
using RT.Util.Serialization;

namespace Editon
{
    abstract class Item
    {
        public int X, Y;

        public virtual void Move(Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: Y--; break;
                case Direction.Right: X++; break;
                case Direction.Down: Y++; break;
                case Direction.Left: X--; break;
            }
        }

        public abstract bool ContainsX(int x);
        public abstract bool ContainsY(int y);
        public abstract bool StoppableAtX(int x);
        public abstract bool StoppableAtY(int y);
        public virtual int X2 { get { return X + 1; } }
        public virtual int Y2 { get { return Y + 1; } }
    }

    sealed class Box : Item
    {
        public int Width, Height;
        public TextLine[][] TextAreas;

        [ClassifyNotNull]
        public AutoDictionary<Direction, LineType> LineTypes = Helpers.MakeDictionary(LineType.Single, LineType.Single, LineType.Single, LineType.Single);

        public LineType this[Direction loc] { get { return LineTypes[loc]; } }

        public bool Contains(int x, int y)
        {
            return x >= X && x <= X + Width && y >= Y && y <= Y + Height;
        }

        public override bool ContainsX(int x) { return x >= X && x <= X + Width; }
        public override bool ContainsY(int y) { return y >= Y && y <= Y + Height; }
        public override bool StoppableAtX(int x) { return x >= X && x <= X + Width; }
        public override bool StoppableAtY(int y) { return y >= Y && y <= Y + Height; }

        public override void Move(Direction direction)
        {
            EditonProgram.Invalidate(this);
            var ox = 0;
            var oy = 0;
            switch (direction)
            {
                case Direction.Up: oy = -1; break;
                case Direction.Right: ox = 1; break;
                case Direction.Down: oy = 1; break;
                case Direction.Left: ox = -1; break;
            }
            X += ox;
            Y += oy;
            for (int i = 0; i < TextAreas.Length; i++)
                for (int j = 0; j < TextAreas[i].Length; j++)
                {
                    TextAreas[i][j].X += ox;
                    TextAreas[i][j].Y += oy;
                }
            EditonProgram.Invalidate(this);
        }

        private Action getUndo()
        {
            var x = X;
            var y = Y;
            var width = Width;
            var height = Height;
            var textXs = TextAreas.Select(lines => lines.Select(line => line.X).ToArray()).ToArray();
            var textYs = TextAreas.Select(lines => lines.Select(line => line.Y).ToArray()).ToArray();
            return () =>
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                for (int i = 0; i < TextAreas.Length; i++)
                    for (int j = 0; j < TextAreas[i].Length; j++)
                    {
                        TextAreas[i][j].X = textXs[i][j];
                        TextAreas[i][j].Y = textYs[i][j];
                    }
            };
        }

        public override int X2 { get { return X + Width + 1; } }
        public override int Y2 { get { return Y + Height + 1; } }
    }

    abstract class NonBoxItem : Item { }

    sealed class Node : NonBoxItem
    {
        public AutoDictionary<Direction, LineType> LineTypes = new AutoDictionary<Direction, LineType>();
        public AutoDictionary<Direction, Item> JoinUpWith = new AutoDictionary<Direction, Item>();
        public override bool ContainsX(int x) { return x == X; }
        public override bool ContainsY(int y) { return y == Y; }
        public override bool StoppableAtX(int x)
        {
            return
                x == X ||
                (LineTypes[Direction.Right] != LineType.None && x >= X && x <= JoinUpWith[Direction.Right].X) ||
                (LineTypes[Direction.Left] != LineType.None && x <= X && x >= JoinUpWith[Direction.Left].X);
        }
        public override bool StoppableAtY(int y)
        {
            return
                y == Y ||
                (LineTypes[Direction.Down] != LineType.None && y >= Y && y <= JoinUpWith[Direction.Down].Y) ||
                (LineTypes[Direction.Up] != LineType.None && y <= Y && y >= JoinUpWith[Direction.Up].Y);
        }
    }

    sealed class LineEnd : NonBoxItem
    {
        public Direction Direction;
        public LineType LineType;
        public Item JoinUpWith;
        public override bool ContainsX(int x) { return x == X; }
        public override bool ContainsY(int y) { return y == Y; }
        public override bool StoppableAtX(int x)
        {
            return
                x == X ||
                (Direction == Direction.Right && x >= X && x <= JoinUpWith.X) ||
                (Direction == Direction.Left && x <= X && x >= JoinUpWith.X);
        }
        public override bool StoppableAtY(int y)
        {
            return
                y == Y ||
                (Direction == Direction.Down && y >= Y && y <= JoinUpWith.Y) ||
                (Direction == Direction.Up && y <= Y && y >= JoinUpWith.Y);
        }
    }

    sealed class TextLine
    {
        public int X;
        public int Y;
        public string Content;
    }
}
