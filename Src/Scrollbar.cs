﻿using System;
using RT.Util.Consoles;
using RT.Util.ExtensionMethods;

namespace Editon
{
    sealed class Scrollbar
    {
        public Scrollbar(bool isVertical)
        {
            IsVertical = isVertical;
            Invalidated = true;
        }

        private int _min;
        public int Min
        {
            get { return _min; }
            set
            {
                if (value > _value)
                    throw new ArgumentOutOfRangeException("value", "Cannot set Min to a value greater than the current value of the scrollbar ({0}).".Fmt(Value));
                _min = value;
                Invalidated = true;
            }
        }

        private int _max;
        public int Max
        {
            get { return _max; }
            set
            {
                if (value < _value)
                    throw new ArgumentOutOfRangeException("value", "Cannot set Max to a value smaller than the current value of the scrollbar ({0}).".Fmt(Value));
                _max = value;
                Invalidated = true;
            }
        }

        private int _value;
        public int Value
        {
            get { return _value; }
            set
            {
                if (value < Min && value > Max)
                    throw new ArgumentOutOfRangeException("value", "The specified value is out of the current range ({0} to {1}).".Fmt(Min, Max));
                _value = value;
                Invalidated = true;
            }
        }

        public bool IsVertical { get; private set; }
        public bool Invalidated { get; private set; }

        private void renderHorizontal()
        {
            Console.SetCursorPosition(0, Console.BufferHeight - EditonProgram.EditorBottom);
            ConsoleUtil.Write("▌◄▐".Color(ConsoleColor.Black, ConsoleColor.Gray));
            var range = EditonProgram.EditorWidth;
            var max = Math.Max(_max, _value + range);
            var width = range - 2 * 3;
            var from = width * _value / (max - _min);
            var to = width * (_value + range) / (max - _min);
            ConsoleUtil.Write((new string('▒', from) + new string('█', Math.Min(to - from, width)) + new string('▒', Math.Max(0, width - to))).Color(ConsoleColor.Gray));
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write("▌►▐".Color(ConsoleColor.Black, ConsoleColor.Gray));
            Console.BackgroundColor = ConsoleColor.Black;
        }

        private void renderVertical()
        {
            Console.SetCursorPosition(Console.BufferWidth - EditonProgram.EditorRight, EditonProgram.EditorTop);
            ConsoleUtil.Write(" ▲ ".Color(ConsoleColor.Black, ConsoleColor.Gray));
            var range = EditonProgram.EditorHeight;
            var max = Math.Max(_max, _value + range);
            var height = range - 2 * 1;
            var from = height * _value / (max - _min);
            var to = height * (_value + range) / (max - _min);
            for (int i = 0; i < height; i++)
            {
                Console.SetCursorPosition(Console.BufferWidth - EditonProgram.EditorRight, i + EditonProgram.EditorTop + 1);
                ConsoleUtil.Write((i < from || i > to ? "▒▒▒" : "███").Color(ConsoleColor.Gray));
            }
            Console.SetCursorPosition(Console.BufferWidth - EditonProgram.EditorRight, Console.BufferHeight - EditonProgram.EditorBottom - 1);
            ConsoleUtil.Write(" ▼ ".Color(ConsoleColor.Black, ConsoleColor.Gray));
        }

        public void Render()
        {
            if (!Invalidated)
                return;
            Invalidated = false;
            if (IsVertical)
                renderVertical();
            else
                renderHorizontal();
        }
    }
}
