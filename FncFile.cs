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
        public List<Box> Boxes = new List<Box>();

        [ClassifyNotNull]
        public List<HLine> HLines = new List<HLine>();

        [ClassifyNotNull]
        public List<VLine> VLines = new List<VLine>();
    }
}
