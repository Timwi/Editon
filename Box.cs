using System;
using RT.Util.ExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Serialization;

namespace Editon
{
    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class Box
    {
        public int X, Y;
        public int Width, Height;
        public string Content;

        [ClassifyNotNull]
        public Dictionary<LineLocation, LineType> LineTypes = new Dictionary<LineLocation, LineType>(4);

        public LineType this[LineLocation loc]
        {
            get
            {
                return LineTypes.Get(loc, LineType.None);
            }
        }
    }
}
