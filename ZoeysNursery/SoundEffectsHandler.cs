using System;
using Microsoft.Xna.Framework.Audio;
using StardewValley;
using System.IO;
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using System.Collections.Generic;

namespace ZoeysNursery
{
    public class SoundEffectsHandler
    {
        private IModHelper helper;
        private IMonitor monitor;
        private ICue waterfallCue;
        private static float volumeOverrideForLocChange;
        private static float shortestDistanceForCue;
        private static int updateTimer = 100;
        private static int farthestSoundDistance = 4024;

        public SoundEffectsHandler(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
            this.waterfallCue = createWaterfallCue();
        }

        /// <summary>
        /// updates the volume of the waterfall sound effect based on the distance between the player and the closest waterfall.
        /// </summary>
        public void update(Dictionary<String, List<Vector2>> waterfallPositionsByLocation)
        {
            if (!Context.IsWorldReady || waterfallCue == null || !waterfallPositionsByLocation.ContainsKey(Game1.player.currentLocation.Name))
            {
                waterfallCue.Pause();
                return;
            }

            List<Vector2> waterfallPositions = waterfallPositionsByLocation.GetValueOrDefault(Game1.player.currentLocation.Name, new List<Vector2>());

            GameTime time = Game1.currentGameTime;
            int elapsedMillis = time.ElapsedGameTime.Milliseconds;

            if (volumeOverrideForLocChange < 1f)
            {
                volumeOverrideForLocChange += elapsedMillis * 0.0003f;
            }

            updateTimer -= elapsedMillis;

            if (updateTimer > 0)
            {
                return;
            }

            shortestDistanceForCue = 9999999f;
            Vector2 standingPosition = Game1.player.getStandingPosition();

            foreach (Vector2 position in waterfallPositions)
            {
                float distance = Vector2.Distance(position, standingPosition);
                if (shortestDistanceForCue > distance)
                {
                    shortestDistanceForCue = distance;
                }
            }

            if (volumeOverrideForLocChange >= 0f)
            {
                if (shortestDistanceForCue <= (float)farthestSoundDistance)
                {
                    float volumeOverride = Math.Min(volumeOverrideForLocChange, Math.Min(1f, 1f - shortestDistanceForCue / (float)farthestSoundDistance));
                    if (waterfallCue != null)
                    {
                        waterfallCue.Volume = volumeOverride * 100f * Math.Min(Game1.ambientPlayerVolume, Game1.options.ambientVolumeLevel);
                        waterfallCue.Resume();
                    }
                }
                else if (waterfallCue != null)
                {
                    waterfallCue.Pause();
                }
            }
            updateTimer = 100;
        }

        /// <summary>
        /// creates a waterfall sound affect cue and adds it to the game's soundbank
        /// </summary>
        /// <returns>the generated cue</returns>
        private ICue createWaterfallCue()
        {
            CueDefinition waterfallCueDefinition = new CueDefinition();

            // Adding the name for the cue, which will be
            // the name of the audio to play when using sound functions.
            waterfallCueDefinition.name = "zoeysNurseryWaterfall";

            // If this sound is played multiple times in quick succession,
            // only one sound instance will play at a time.
            waterfallCueDefinition.instanceLimit = 1;
            waterfallCueDefinition.limitBehavior = CueDefinition.LimitBehavior.ReplaceOldest;

            // Get the audio file and add it to a SoundEffect.
            SoundEffect waterfallAudio;
            string filePathCombined = Path.Combine(helper.DirectoryPath, "assets", "waterfall.wav");

            using (var stream = new System.IO.FileStream(filePathCombined, System.IO.FileMode.Open))
            {
                waterfallAudio = SoundEffect.FromStream(stream);
            }

            // Setting the sound effect to the new cue.
            waterfallCueDefinition.SetSound(waterfallAudio, Game1.audioEngine.GetCategoryIndex("Ambient"), true);

            // Adding the cue to the sound bank.
            Game1.soundBank.AddCue(waterfallCueDefinition);
            ICue waterfallCue = Game1.soundBank.GetCue("zoeysNurseryWaterfall");

            // trigger play once, so that we can use 'resume' on this sound later
            waterfallCue.Play();
            waterfallCue.Pause();

            return waterfallCue;
        }
    }
}

