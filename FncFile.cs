using System;
using RT.Util.ExtensionMethods;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Serialization;

namespace Editon
{
    sealed class FncFile
    {
        public List<Box> Boxes = new List<Box>();
        public SourceAsChars Source = new SourceAsChars(new string[0]);
    }
}
