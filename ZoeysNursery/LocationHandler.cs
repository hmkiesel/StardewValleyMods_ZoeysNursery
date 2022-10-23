﻿using System;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;

namespace ZoeysNursery
{
    public class LocationHandler
    {
        public Dictionary<String, List<Vector2>> WaterfallPositionsByLocation { get; } = new Dictionary<string, List<Vector2>>();

        public LocationHandler()
        {
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
                location != null && location.Name.Contains("ZoeysNursery") && location.Map.GetLayer("CustomPaths") != null
            );

            foreach (GameLocation location in filteredLocations)
            {
                for (int x = 0; x < location.Map.GetLayer("CustomPaths").TileWidth; x++)
                {
                    for (int y = 0; y < location.Map.GetLayer("CustomPaths").TileHeight; y++)
                    {
                        Vector2 tilePosition = new Vector2(x, y);

                        // check if a fruit tree should spawn here
                        string treeSpawn = location.doesTileHaveProperty(x, y, "spawnTree", "CustomPaths");
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
                        string localSound = location.doesTileHaveProperty(x, y, "localSound", "CustomPaths");
                        if (localSound != null && localSound == "zoeysNurseryWaterfall")
                        {
                            //TODO: make (dynamicWaterfallVolume) an optional config, if false, set location.localSound(localSound) instead
                            waterfallPositions.Add(tilePosition);
                        }
                    }
                }

                // store the waterfall coordinates for this location
                WaterfallPositionsByLocation[location.Name] = waterfallPositions;
            }
        }
    }
}

