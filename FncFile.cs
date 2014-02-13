using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RT.Util.Serialization;

namespace Editon
{
    [ClassifyIgnoreIfDefault, ClassifyIgnoreIfEmpty]
    sealed class FncFile : IClassifyObjectProcessor
    {
        public List<Box> Boxes = new List<Box>();
        public List<HLine> HLines = new List<HLine>();
        public List<VLine> VLines = new List<VLine>();

        void IClassifyObjectProcessor.BeforeSerialize() { }
        void IClassifyObjectProcessor.AfterDeserialize()
        {
            if (Boxes == null)
                Boxes = new List<Box>();
            if (HLines == null)
                HLines = new List<HLine>();
            if (VLines == null)
                VLines = new List<VLine>();
        }
    }
}
