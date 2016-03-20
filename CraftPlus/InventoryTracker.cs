using Microsoft.Xna.Framework;
using Storm.StardewValley.Wrapper;
using System.Collections.Generic;

namespace CraftPlus
{
    public class InventoryTracker
    {
        public int Id { get; set; }
        public Vector2 Position { get; set; }
        public string Location { get; set; }
        public List<ObjectItem> Items { get; set; }

        public InventoryTracker()
        {
            Items = new List<ObjectItem>();
        }
    }
}
