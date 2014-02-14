using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using RT.Util;
using RT.Util.ExtensionMethods;
using RT.Util.Consoles;

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
            Console.SetCursorPosition(0, Console.BufferHeight - 2);
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ◄ ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
            var range = Console.BufferWidth - 3;
            var width = range - 2 * 3;
            var from = width * Min / (Max - Min + range);
            var to = width * Max / (Max - Min + range);
            ConsoleUtil.Write((new string('▒', from) + new string('█', to - from) + new string('▒', width - to)).Color(ConsoleColor.Gray));
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ► ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
        }

        private void renderVertical()
        {
            Console.SetCursorPosition(Console.BufferWidth - 3, 0);
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ▲ ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
            var range = Console.BufferHeight - 2;
            var height = range - 2 * 1;
            var from = height * Min / (Max - Min + range);
            var to = height * Max / (Max - Min + range);
            for (int i = 0; i < height; i++)
            {
                Console.SetCursorPosition(Console.BufferWidth - 3, i + 1);
                ConsoleUtil.Write((i < from || i > to ? "▒▒▒" : "███").Color(ConsoleColor.Gray));
            }
            Console.SetCursorPosition(Console.BufferWidth - 3, Console.BufferHeight - 3);
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ▼ ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
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
