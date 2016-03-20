using System.Collections.Generic;

namespace CraftPlus
{
    public class IngredientTracker
    {
        public int Id { get; set; }
        public int QuantityRequired { get; set; }
        public int QuantityWeHave { get; set; }
        public List<Dictionary<int, int>> IngredientLocations { get; set; }

        public IngredientTracker()
        {
            IngredientLocations = new List<Dictionary<int, int>>();
        }
    }
}
