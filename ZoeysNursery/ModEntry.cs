using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace ZoeysNursery
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        public static IMonitor monitor;

        private SoundEffectsHandler soundEffectsHandler;
        private LocationHandler locationHandler;
        private const string waterfallSoundFileName = "waterfall.wav";
        private const string waterfallSoundEffectName = "zoeysNurseryWaterfall";

        /********* Public methods *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            monitor = this.Monitor;
            soundEffectsHandler = new SoundEffectsHandler(helper, monitor);
            locationHandler = new LocationHandler(monitor);

            soundEffectsHandler.createCue(waterfallSoundEffectName, waterfallSoundFileName);


            helper.Events.GameLoop.GameLaunched += this.onGameLaunched;
            helper.Events.GameLoop.UpdateTicked += this.update;
            helper.Events.GameLoop.DayStarted += this.dayStarted;
        }

        /********* Private methods *********/

        /// <summary>
        /// triggered when game is launched, loads harmony patches
        /// </summary>
        /// <param name="sender">the event sender</param>
        /// <param name="e">game launched event arguments</param>
        private void onGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            new Harmony(this.ModManifest.UniqueID).PatchAll(typeof(ModEntry).Assembly);
        }

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
            soundEffectsHandler.update(locationHandler.WaterfallPositionsByLocation, waterfallSoundEffectName);
        }
    }
}
