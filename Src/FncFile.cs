using System.Collections.Generic;

namespace Editon
{
    sealed class FncFile
    {
        public List<Box> Boxes = new List<Box>();
        public SourceAsChars Source = new SourceAsChars(new string[0]);
    }
}
