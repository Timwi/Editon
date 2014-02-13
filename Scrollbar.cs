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
        private int _min;
        public int Min
        {
            get { return _min; }
            set
            {
                if (value > _value)
                    throw new ArgumentOutOfRangeException("value", "Cannot set Min to a value greater than the current value of the scrollbar ({0}).".Fmt(Value));
                _min = value;
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
            }
        }

        public void RenderHorizontal()
        {
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ◄ ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
            var range = Console.BufferWidth - 3;
            var width = Console.BufferWidth - 3 - 2 * 3;
            var from = width * Min / (Max - Min + range);
            var to = width * Max / (Max - Min + range);
            ConsoleUtil.Write(
                new string('▒', from).Color(ConsoleColor.Gray) +
                new string('█', to - from).Color(ConsoleColor.Gray) +
                new string('▒', width - to).Color(ConsoleColor.Gray));
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ► ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
        }

        public void RenderVertical()
        {
            Console.SetCursorPosition(Console.BufferWidth - 3, 0);
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ▲ ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
            var range = Console.BufferHeight - 1;
            var height = Console.BufferHeight - 1 - 2 * 1;
            var from = height * Min / (Max - Min + range);
            var to = height * Max / (Max - Min + range);
            for (int i = 0; i < height; i++)
            {
                Console.SetCursorPosition(Console.BufferWidth - 3, i + 1);
                ConsoleUtil.Write(i < from || i > to
                    ? "▒▒▒".Color(ConsoleColor.Gray)
                    : "███".Color(ConsoleColor.Gray));
            }
            Console.SetCursorPosition(Console.BufferWidth - 3, Console.BufferHeight - 2);
            Console.BackgroundColor = ConsoleColor.Gray;
            ConsoleUtil.Write(" ▼ ".Color(ConsoleColor.Black));
            Console.BackgroundColor = ConsoleColor.Black;
        }
    }
}
