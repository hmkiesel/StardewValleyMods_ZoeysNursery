using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Network;
using StardewValley.TerrainFeatures;
using xTile.Dimensions;
using xTile.ObjectModel;
using xTile.Tiles;

namespace ZoeysNursery
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private static IMonitor monitor;
        private static List<Vector2> waterfallPositions = new List<Vector2>();

        private SoundEffectsHandler soundEffectsHandler;
        private LocationHandler locationHandler;

        /********* Public methods *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            monitor = this.Monitor;
            soundEffectsHandler = new SoundEffectsHandler(helper, monitor);
            locationHandler = new LocationHandler();

            helper.Events.GameLoop.UpdateTicked += this.update;
            helper.Events.GameLoop.DayStarted += this.dayStarted;
        }

        /********* Private methods *********/

        /// <summary>
        /// check tile properties for each location, to spawn fruit trees and store all waterfall positions.
        /// (triggered on GameLoop.DayStarted, every time a new day is started or a save is loaded)
        /// </summary>
        /// <param name="sender">the event sender</param>
        /// <param name="e">day started event arguments</param>
        private void dayStarted(object sender, DayStartedEventArgs e)
        {
            locationHandler.dayStarted(e);
        }

        /// <summary>
        /// update the sound effects volume (triggered on GameLoop.UpdateTicked, about 60 times per second)
        /// </summary>
        /// <param name="sender">the event sender</param>
        /// <param name="e">update event arguments</param>
        private void update(object sender, UpdateTickedEventArgs e)
        {
            soundEffectsHandler.update(locationHandler.WaterfallPositionsByLocation);
        }
    }
}
