using System;
using Microsoft.Xna.Framework.Audio;
using StardewValley;
using System.IO;
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ZoeysNursery
{
    /// <summary> builds cues for custom sound effects and handles dynamic volume for spatial sound effects </summary>
    public class SoundEffectsHandler
    {
        private IModHelper helper;
        private IMonitor monitor;
        private Dictionary<String, ICue> soundEffectCuesByName = new();
        private static float volumeOverrideForLocChange;
        private static float shortestDistanceForCue;
        private static int updateTimer = 100;
        private static int farthestSoundDistance = 4024;

        public SoundEffectsHandler(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
        }

        /// <summary>
        /// updates the volume of the sound effect based on the distance between the player and the closest sound effect source.
        /// </summary>
        /// <param name="soundEffectPositionsByLocation">dictionary mapping location names to a list of sound effect positions</param>
        /// <param name="soundEffectName">name of the sound effect cue</param>
        public void update(Dictionary<String, List<Vector2>> soundEffectPositionsByLocation, String soundEffectName)
        {
            if (!soundEffectCuesByName.TryGetValue(soundEffectName, out ICue soundEffectCue))
            {
                throw new ArgumentException($"invalid key - there is no sound effect cue available with name {soundEffectName}");
            }

            if (!Context.IsWorldReady || soundEffectCue == null || !soundEffectPositionsByLocation.ContainsKey(Game1.player.currentLocation.Name))
            {
                soundEffectCue.Pause();
                return;
            }

            List<Vector2> soundEffectPositions = soundEffectPositionsByLocation.GetValueOrDefault(Game1.player.currentLocation.Name, new List<Vector2>());

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

            foreach (Vector2 position in soundEffectPositions)
            {
                float distance = Vector2.Distance(position, standingPosition);
                if (shortestDistanceForCue > distance)
                {
                    shortestDistanceForCue = distance;
                }
            }

            if (volumeOverrideForLocChange >= 0f && soundEffectCue != null)
            {
                if (shortestDistanceForCue <= farthestSoundDistance)
                {
                    float volumeOverride = Math.Min(volumeOverrideForLocChange, Math.Min(1f, 1f - shortestDistanceForCue /farthestSoundDistance));
                    soundEffectCue.Volume = volumeOverride * 100f * Math.Min(Game1.ambientPlayerVolume, Game1.options.ambientVolumeLevel);
                    soundEffectCue.Resume();
                }
                else
                {
                    soundEffectCue.Pause();
                }
            }
            updateTimer = 100;
        }

        /// <summary>
        /// creates a sound affect cue and adds it to the game's soundbank
        /// </summary>
        /// <param name="soundEffectName">name for the sound effect cue</param>
        /// <param name="soundEffectFileName">name of the audio file in assets/ directory</param>
        public void createCue(String soundEffectName, String soundEffectFileName)
        {
            if (soundEffectCuesByName.ContainsKey(soundEffectName))
            {
                throw new ArgumentException($"duplicate key - there is already a sound effect with name {soundEffectName}");
            }

            CueDefinition cueDefinition = new CueDefinition();

            // Adding the name for the cue, which will be
            // the name of the audio to play when using sound functions.
            cueDefinition.name = soundEffectName;

            // If this sound is played multiple times in quick succession,
            // only one sound instance will play at a time.
            cueDefinition.instanceLimit = 1;
            cueDefinition.limitBehavior = CueDefinition.LimitBehavior.ReplaceOldest;

            // Get the audio file and add it to a SoundEffect.
            SoundEffect soundEffectAudio;
            string filePathCombined = Path.Combine(helper.DirectoryPath, "assets", soundEffectFileName);

            using (var stream = new FileStream(filePathCombined, FileMode.Open))
            {
                soundEffectAudio = SoundEffect.FromStream(stream);
            }

            // Setting the sound effect to the new cue.
            cueDefinition.SetSound(soundEffectAudio, Game1.audioEngine.GetCategoryIndex("Ambient"), true);

            // Add the cue to the sound bank.
            Game1.soundBank.AddCue(cueDefinition);
            ICue soundCue = Game1.soundBank.GetCue(soundEffectName);

            // trigger play once, so that we can use 'resume' on this sound later
            soundCue.Play();
            soundCue.Pause();

            soundEffectCuesByName.Add(soundEffectName, soundCue);
        }
    }
}

