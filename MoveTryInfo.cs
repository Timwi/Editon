using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Editon
{
    abstract class Modification
    {
        public Item Item { get; private set; }
        public Modification(Item item) { Item = item; }
        public abstract void Make();
    }

    sealed class MoveItem : Modification
    {
        public Direction Direction { get; private set; }
        public MoveItem(Item item, Direction direction) : base(item) { Direction = direction; }
        public override void Make() { Item.Move(Direction); }
    }
}
