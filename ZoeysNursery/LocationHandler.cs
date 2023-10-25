using System;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewModdingAPI;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ZoeysNursery
{
    /// <summary> processes custom locations/maps </summary>
    public class LocationHandler
    {
        private const String customPathLayer = "CustomPaths";
        private IMonitor monitor;
        public Dictionary<String, List<Vector2>> WaterfallPositionsByLocation { get; } = new Dictionary<string, List<Vector2>>();

        public LocationHandler(IMonitor monitor)
        {
            this.monitor = monitor;
        }

        /// <summary>
        /// check tile properties for each location, to spawn fruit trees and store all waterfall positions.
        /// (triggered on GameLoop.DayStarted, every time a new day is started or a save is loaded)
        /// </summary>
        /// <param name="sender">the event sender</param>
        /// <param name="e">day started event arguments</param>
        public void dayStarted(DayStartedEventArgs e)
        {
            List<Vector2> waterfallPositions = new List<Vector2>();
            IEnumerable<GameLocation> filteredLocations = Game1.locations.Where(location =>
                location != null && location.Name.Contains("ZoeysNursery") && location.Map.GetLayer(customPathLayer) != null
            );

            foreach (GameLocation location in filteredLocations)
            {
                for (int x = 0; x < location.Map.GetLayer(customPathLayer).TileWidth; x++)
                {
                    for (int y = 0; y < location.Map.GetLayer(customPathLayer).TileHeight; y++)
                    {
                        Vector2 tilePosition = new Vector2(x, y);

                        // check if a fruit tree should spawn here
                        string treeSpawn = location.doesTileHaveProperty(x, y, "spawnTree", customPathLayer);
                        if (treeSpawn != null && location.GetFurnitureAt(tilePosition) == null && !location.terrainFeatures.ContainsKey(tilePosition) && !location.objects.ContainsKey(tilePosition))
                        {
                            // spawn tree at this tile
                            int treeIndex = 0;
                            switch (treeSpawn)
                            {
                                case "banana":
                                    treeIndex = 69;
                                    break;
                                case "cherry":
                                    treeIndex = 628;
                                    break;
                                case "apricot":
                                    treeIndex = 629;
                                    break;
                                case "orange":
                                    treeIndex = 630;
                                    break;
                                case "peach":
                                    treeIndex = 631;
                                    break;
                                case "pomegranate":
                                    treeIndex = 632;
                                    break;
                                case "apple":
                                    treeIndex = 633;
                                    break;
                                case "mango":
                                    treeIndex = 835;
                                    break;
                            }

                            FruitTree tree = new FruitTree(treeIndex, FruitTree.treeStage);
                            tree.daysUntilMature.Value = 0;

                            location.terrainFeatures.Add(tilePosition, tree);
                        }

                        // check if a waterfall is here
                        string localSound = location.doesTileHaveProperty(x, y, "localSound", customPathLayer);
                        if (localSound != null && localSound == "zoeysNurseryWaterfall")
                        {
                            //TODO: make (dynamicWaterfallVolume) an optional config, if false, set location.localSound(localSound) instead
                            waterfallPositions.Add(tilePosition * 64f);
                        }
                    }
                }

                // store the waterfall coordinates for this location
                WaterfallPositionsByLocation[location.Name] = waterfallPositions;
            }
        }
    }
}

