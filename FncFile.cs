using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Serialization;

namespace Editon
{
    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class FncFile
    {
        [ClassifyNotNull]
        public List<Item> Items = new List<Item>();
    }
}
