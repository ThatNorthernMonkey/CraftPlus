using System.Collections.Generic;
using Storm.ExternalEvent;
using Storm.StardewValley.Event;
using Storm.StardewValley.Wrapper;
using Storm.StardewValley.Accessor;
using System;
using Microsoft.Xna.Framework.Input;
using Storm.Collections;
using System.Linq;
using Microsoft.Xna.Framework;

namespace CraftPlus
{
    [Mod]
    public class CraftPlus
    {
        private static int ChestCount = 0;
        private List<InventoryTracker> Inventories { get; set; }
        private StaticContext Root { get; set; }
        private bool HasReloadedStorage { get; set; }
        private bool InitialItemsAdded { get; set; }
        private List<IngredientTracker> Ingredients { get; set; }
        private Dictionary<string, bool> CachedIngredients { get; set; }
        private List<InventoryTracker> CurrentIngredientInventories { get; set; }
        /*

        Scan the inventory of all chests in the farm, farmhouse, greenhouse and animal buildings. 
        Add them to a List<Item>, add the list of items to a Dictionary<Vector2, List<Item> so we know
        which chest to remove things from.

        doesFarmerHaveIngredientsInInventory(List<item>)

        This is where we check the list. Can we catch the event pre, rewrite and return early with our list?

        consumeIngredients() : void

        We will remove the items from the chests in this event.

        createItem() : Item   this might be a problem, the whole Proxy / Delegate crash issue.

        */

        public CraftPlus()
        {
            CachedIngredients = new Dictionary<string, bool>();
            Inventories = new List<InventoryTracker>();
            HasReloadedStorage = false;
            InitialItemsAdded = false;
            Ingredients = new List<IngredientTracker>();
            CurrentIngredientInventories = new List<InventoryTracker>();
        }

        [Subscribe]
        public void AfterGameUpdates(PostUpdateEvent @e)
        {
            if (@e.Root.HasLoadedGame)
            {
                Root = @e.Root;
            }
        }

        [Subscribe]
        public void AfterConsumingIngredient(OnConsumeCraftingIngredientEvent @e)
        {
            if(!@e.Recipe.IsCookingRecipe)
            {
                FindIngredientsInInventoryTracker(@e.Recipe.RecipeList);
                UpdateItemsInInventoriesAfterCrafting(@e.Recipe.RecipeList);
                Update();

                @e.ReturnEarly = true;
            }
        }

        [Subscribe]
        public void DoWeHaveTheIngredients(PreCraftingRecipeHaveIngredientCheckEvent @e)
        {

            if(!@e.Recipe.IsCookingRecipe)
            {
                if (CachedIngredients.ContainsKey(@e.Recipe.Name))
                {
                    @e.ReturnValue = CachedIngredients[@e.Recipe.Name];
                    @e.ReturnEarly = true;
                }
                else
                {
                    if (CheckInventoriesForIngredients(@e.Recipe.RecipeList))
                    {
                        if (CachedIngredients.ContainsKey(@e.Recipe.Name))
                        {
                            CachedIngredients.Remove(@e.Recipe.Name);
                            CachedIngredients.Add(@e.Recipe.Name, true);
                        }
                        else
                        {
                            CachedIngredients.Add(@e.Recipe.Name, true);
                        }

                        @e.ReturnValue = true;
                        @e.ReturnEarly = true;
                    }
                    else
                    {
                        if (CachedIngredients.ContainsKey(@e.Recipe.Name))
                        {
                            CachedIngredients.Remove(@e.Recipe.Name);
                            CachedIngredients.Add(@e.Recipe.Name, false);
                        }
                        else
                        {
                            CachedIngredients.Add(@e.Recipe.Name, false);
                        }

                        @e.ReturnValue = false;
                        @e.ReturnEarly = true;
                    }
                }
            }
            // We need to cache the results, or the Draw() method checks 30+ times a second. Rip CPU.
        }

        [Subscribe]
        public void UpdateInventoriesWhenInventoryOpened(KeyPressedEvent @e)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.E))
            {
                Update();
            }
        }

        [Subscribe]
        public void WhenAddItemToInventoryUpdateTracker(AddItemToInventoryEvent @e)
        {
            Update();
        }

        [Subscribe]
        public void WhenAddItemToChestUpdateTracker(AddItemToChestEvent @e)
        {
            Update();
        }

        private bool CheckInventoriesForIngredients(ProxyDictionary<int, int> ingredients)
        {
            var ingList = new List<IngredientTracker>();
            var haveIngredients = true;

            // Iterate through the ingredients index
            foreach (var id in ingredients.Keys)
            {
                var ing = new IngredientTracker { Id = id, QuantityRequired = ingredients[id] };

                var chestItems = Inventories.SelectMany(x => x.Items)
                                            .Where(under => under.Underlying.GetType().ToString() == "StardewValley.Object");
                foreach (var inv in Inventories)
                {
                    foreach (var item in inv.Items)
                    {
                        if (item.As<ObjectAccessor, ObjectItem>().ParentSheetIndex == id)
                        {
                            ing.QuantityWeHave += item.As<ObjectAccessor, ObjectItem>().Stack;
                        }

                        //Catch all Fish (Any)
                        else if (item.As<ObjectAccessor, ObjectItem>().Category == id)
                        {
                            if (item.As<ObjectAccessor, ObjectItem>().Category == id)
                            {
                                ing.QuantityWeHave += item.As<ObjectAccessor, ObjectItem>().Stack;

                            }
                        }

                        ingList.Add(ing);

                    }
                }
            }

            foreach (var ing in ingList)
            {
                if (ing.QuantityRequired > ing.QuantityWeHave)
                {
                    haveIngredients = false;
                }
            }

            return haveIngredients;
        }

        private void ResetCache()
        {
            CachedIngredients = null;
            CachedIngredients = new Dictionary<string, bool>();
        }

        private void ResetInventoryTracker()
        {
            Inventories = null;
            Inventories = new List<InventoryTracker>();
            ChestCount = 0;
        }

        private void UpdateItemsInInventoriesAfterCrafting(ProxyDictionary<int, int> ingredients)
        {
            // Id : Quantity                    
            var inventories = GetAllInventories();
            var ingList = new List<IngredientTracker>();
            var quantTotal = 0;
            var hasUsedAllInventory = false;

            foreach (var key in ingredients.Keys)
            {
                ingList.Add(new IngredientTracker { Id = key, QuantityRequired = ingredients[key] });
                quantTotal += ingredients[key];
            }

            try
            {
                if (CurrentIngredientInventories.Count > 0)
                {
                    while (quantTotal != 0)
                    {

                        if (ingList.Exists(x => x.Id == -4))
                        {
                            var emptyFish = ingList.Select(x => x)
                                                   .Where(x => x.Id == -4)
                                                   .Where(x => x.QuantityRequired == 0);

                            if (emptyFish != null)
                            {
                                foreach (var fish in emptyFish)
                                {
                                    ingredients.Remove(fish.Id);
                                }

                                FindIngredientsInInventoryTracker(ingredients);
                            }
                        }

                        // Make sure if we have fulfilled the required quantity, we remove from the ingredient list.
                        if (ingList.Exists(x => x.QuantityRequired == 0))
                        {
                            ingList.RemoveAll(x => x.QuantityRequired == 0);
                        }

                        foreach (var ing in ingList)
                        {

                            foreach (var inv in CurrentIngredientInventories)
                            {
                                var selectedInv = inventories.FirstOrDefault(x => x.As<ChestAccessor, Chest>().BoundingBox.X == inv.Position.X &&
                                                                                  x.As<ChestAccessor, Chest>().BoundingBox.Y == inv.Position.Y);

                                if (selectedInv != null)
                                {
                                    foreach (var item in selectedInv.As<ChestAccessor, Chest>().Items)
                                    {
                                        //if (item.Underlying.GetType().ToString() == "StardewValley.Object")
                                        if (item.Is<ObjectAccessor>())
                                        {

                                            //Check characters inventory first
                                            var charItems = Root.Player.Items;

                                            if (!hasUsedAllInventory)
                                            {
                                                for (int i = 0; i < charItems.Count; i++)
                                                {
                                                    if (charItems[i] != null)
                                                    {
                                                        if (charItems[i].Is<ObjectAccessor>())
                                                        {
                                                            if (ing.Id == charItems[i].As<ObjectAccessor, ObjectItem>().ParentSheetIndex && ing.QuantityRequired > 0)
                                                            {
                                                                // This particular item has more than enough quantity in its stack for this recipe.
                                                                if (charItems[i].As<ObjectAccessor, ObjectItem>().Stack > ing.QuantityRequired)
                                                                {
                                                                    charItems[i].As<ObjectAccessor, ObjectItem>().Stack -= ing.QuantityRequired;
                                                                    quantTotal -= ing.QuantityRequired;
                                                                    ing.QuantityRequired = 0;

                                                                }

                                                                // This particular item doesnt have the required stack for this recipe. Remove it.
                                                                else if (charItems[i].As<ObjectAccessor, ObjectItem>().Stack <= ing.QuantityRequired && ing.QuantityRequired > 0)
                                                                {
                                                                    ing.QuantityRequired -= charItems[i].As<ObjectAccessor, ObjectItem>().Stack;
                                                                    quantTotal -= charItems[i].As<ObjectAccessor, ObjectItem>().Stack;
                                                                    charItems[i] = null;
                                                                    hasUsedAllInventory = true;
                                                                    Update();
                                                                }
                                                            }

                                                            //Fish (Any)
                                                            else if (ing.Id == charItems[i].As<ObjectAccessor, ObjectItem>().Category && ing.QuantityRequired > 0)
                                                            {
                                                                // This particular item has more than enough quantity in its stack for this recipe.
                                                                if (charItems[i].As<ObjectAccessor, ObjectItem>().Stack > ing.QuantityRequired)
                                                                {
                                                                    charItems[i].As<ObjectAccessor, ObjectItem>().Stack -= ing.QuantityRequired;
                                                                    quantTotal -= ing.QuantityRequired;
                                                                    ing.QuantityRequired = 0;

                                                                }

                                                                // This particular item doesnt have the required stack for this recipe. Remove it.
                                                                else if (charItems[i].As<ObjectAccessor, ObjectItem>().Stack <= ing.QuantityRequired && ing.QuantityRequired > 0)
                                                                {
                                                                    ing.QuantityRequired -= charItems[i].As<ObjectAccessor, ObjectItem>().Stack;
                                                                    quantTotal -= charItems[i].As<ObjectAccessor, ObjectItem>().Stack;
                                                                    charItems[i] = null;
                                                                    hasUsedAllInventory = true;
                                                                    Update();
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }


                                            if (ing.Id == item.As<ObjectAccessor, ObjectItem>().ParentSheetIndex && ing.QuantityRequired > 0)
                                            {
                                                // This particular item has more than enough quantity in its stack for this recipe.
                                                if (item.As<ObjectAccessor, ObjectItem>().Stack > ing.QuantityRequired)
                                                {
                                                    quantTotal -= ing.QuantityRequired;
                                                    item.As<ObjectAccessor, ObjectItem>().Stack -= ing.QuantityRequired;
                                                    ing.QuantityRequired = 0;
                                                }

                                                // This particular item doesnt have the required stack for this recipe. Remove it.
                                                else if (item.As<ObjectAccessor, ObjectItem>().Stack <= ing.QuantityRequired && ing.QuantityRequired > 0)
                                                {

                                                    ing.QuantityRequired -= item.As<ObjectAccessor, ObjectItem>().Stack;
                                                    quantTotal -= item.As<ObjectAccessor, ObjectItem>().Stack;
                                                    selectedInv.As<ChestAccessor, Chest>().Items.Remove(item);
                                                    Update();
                                                }
                                            }

                                            // Fish (Any)
                                            else if (ing.Id == item.As<ObjectAccessor, ObjectItem>().Category && ing.QuantityRequired > 0)
                                            {
                                                // This particular item has more than enough quantity in its stack for this recipe.
                                                if (item.As<ObjectAccessor, ObjectItem>().Stack > ing.QuantityRequired)
                                                {
                                                    quantTotal -= ing.QuantityRequired;
                                                    item.As<ObjectAccessor, ObjectItem>().Stack -= ing.QuantityRequired;
                                                    ing.QuantityRequired = 0;

                                                }

                                                // This particular item doesnt have the required stack for this recipe. Remove it.
                                                else if (item.As<ObjectAccessor, ObjectItem>().Stack <= ing.QuantityRequired && ing.QuantityRequired > 0)
                                                {
                                                    ing.QuantityRequired -= item.As<ObjectAccessor, ObjectItem>().Stack;
                                                    quantTotal -= item.As<ObjectAccessor, ObjectItem>().Stack;
                                                    selectedInv.As<ChestAccessor, Chest>().Items.Remove(item);
                                                    Update();
                                                }
                                            }


                                            if (quantTotal == 0)
                                            {
                                                goto QuantityZero;
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        QuantityZero:
                        continue;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
            }
        }

        private void FindIngredientsInInventoryTracker(ProxyDictionary<int, int> ingredients)
        {
            CurrentIngredientInventories = null;
            CurrentIngredientInventories = new List<InventoryTracker>();

            if (Inventories != null)
            {
                foreach (var inv in Inventories)
                {
                    foreach (var ing in ingredients.Keys)
                    {
                        var items = inv.Items.Select(x => x.As<ObjectAccessor, ObjectItem>())
                                             .Where(x => x.As<ObjectAccessor, ObjectItem>().ParentSheetIndex == ing);

                        var fish = inv.Items.Select(x => x.As<ObjectAccessor, ObjectItem>())
                                             .Where(x => x.As<ObjectAccessor, ObjectItem>().Category == ing);

                        if (items.Count() > 0 || fish.Count() > 0 && !CurrentIngredientInventories.Contains(inv))
                        {
                            CurrentIngredientInventories.Add(inv);
                        }
                    }
                }
            }
        }

        private List<ObjectItem> GetAllInventories()
        {

            var listOfChests = new List<ObjectItem>();
            var locs = Root.Locations;

            try
            {
                //Iterate through the locations
                foreach (var loc in locs)
                {
                    var locObjs = loc.Objects;

                    //Iterate through the objects in the locations
                    foreach (var key in locObjs.Keys)
                    {
                        if (locObjs[key] != null)
                        {
                            if (locObjs[key].Name == "Chest" && locObjs[key].As<ChestAccessor, Chest>().Items.Count > 0)
                            {
                                listOfChests.Add(locObjs[key].As<ChestAccessor, Chest>());
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {

            }

            return listOfChests;
        }

        private void Update()
        {

            //Null out my trackers and create new ones

            ResetInventoryTracker();


            var loc = Root.Locations;
            if (!HasReloadedStorage)
            {
                for (int i = 0; i < loc.Count; i++)
                {
                    if (loc[i].Name == "Farm" || loc[i].Name == "FarmHouse" || loc[i].Name == "Greenhouse")
                    {
                        AddItemsToInventoryTracker(loc[i]);
                    }
                }
            }

            ResetCache();
        }

        private void AddItemsToInventoryTracker(GameLocation loc)
        {
            var locObjs = loc.Objects;

            foreach (var key in locObjs.Keys)
            {
                if (locObjs[key] != null)
                {
                    if (locObjs[key].Name == "Chest" && locObjs[key].As<ChestAccessor, Chest>().Items.Count > 0)
                    {
                        var chest = new InventoryTracker();
                        chest.Id = ChestCount + 1;
                        ChestCount++;
                        chest.Position = new Vector2 { X = locObjs[key].As<ChestAccessor, Chest>().BoundingBox.X, Y = locObjs[key].As<ChestAccessor, Chest>().BoundingBox.Y };
                        chest.Location = loc.Name;

                        for (int i = 0; i < locObjs[key].As<ChestAccessor, Chest>().Items.Count; i++)
                        {
                            var item = locObjs[key].As<ChestAccessor, Chest>().Items[i];

                            if (item != null && item.Underlying.GetType().ToString() == "StardewValley.Object")
                            {
                                chest.Items.Add(item.As<ObjectAccessor, ObjectItem>());
                            }
                        }

                        if (!Inventories.Contains(chest))
                        {
                            Inventories.Add(chest);
                        }
                        else
                        {
                            Inventories.Remove(chest);
                            Inventories.Add(chest);
                        }
                    }
                }
            }

            //Add character inventory too.
            var playerInv = new InventoryTracker { Location = "Character" };

            //First, check if it exists and remove it if it does.
            if (Inventories.Exists(c => c.Location == "Character"))
            {
                int index = Inventories.FindIndex(c => c.Location == "Character");
                Inventories.RemoveAt(index);
            }

            // Get all items in players inventory and add them to the List<ObjectItem>
            for (int i = 0; i < Root.Player.Items.Count; i++)
            {
                if (Root.Player.Items[i] != null && Root.Player.Items[i].Underlying.GetType().ToString() == "StardewValley.Object")
                {
                    playerInv.Items.Add(Root.Player.Items[i].As<ObjectAccessor, ObjectItem>());
                }
            }

            Inventories.Add(playerInv);
        }
    }
}
