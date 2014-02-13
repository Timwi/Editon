using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editon
{
    sealed class HLine
    {
        public int X1, X2, Y;
        public LineType LineType;
    }
    sealed class VLine
    {
        public int X, Y1, Y2;
        public LineType LineType;
    }
}
