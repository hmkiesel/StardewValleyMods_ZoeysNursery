using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HarmonyUtils;
using Microsoft.Xna.Framework;
using StardewValley.Objects;
using System.Collections.Generic;
using StardewValley;
using System;
using StardewModdingAPI;
using StardewValley.TerrainFeatures;

namespace ZoeysNursery
{

    /// <summary>
    /// Holds the transpiler against Crop.harvest.
    /// adds 20% chance of increasing quality for harvested flowers if mini jukebox is playing
    /// </summary>
    [HarmonyPatch(typeof(Crop))]
    internal static class CropQualityHandler
    {
        /// <summary>
        /// TODO: some songs have higher chance of quality increase?
        /// 20% chance of increasing flower quality if mini jukebox is playing
        /// </summary>
        /// <param name="prevQuality">the previous crop quality as calculated by Crop.harvest</param>
        /// <param name="dirt">HoeDirt object containing the crop to be harvested</param>
        public static int GetModifiedQuality(int prevQuality, HoeDirt? dirt)
        {
            if (dirt != null)
            {
                ModEntry.monitor.Log($"previous quality: {prevQuality}", LogLevel.Debug);
                bool isFlower = new StardewValley.Object(Vector2.Zero, dirt.crop.indexOfHarvest.Value, 1).Category == -80;
                ModEntry.monitor.Log($"is this crop a flower: {isFlower}", LogLevel.Debug);
                bool shouldIncreaseQuality = (
                    isFlower &&
                    prevQuality < StardewValley.Object.bestQuality &&
                    Game1.currentLocation.IsMiniJukeboxPlaying() &&
                    new Random().NextDouble() < 0.2
                );
                ModEntry.monitor.Log($"increasing quality: {shouldIncreaseQuality}", LogLevel.Debug);
                return shouldIncreaseQuality ? prevQuality + 1 : prevQuality;
            }

            ModEntry.monitor.Log("Cannot adjust crop quality, hoe dirt is null", LogLevel.Warn);
            return prevQuality;
        }

        /// <summary>
        /// TODO: make configurable via config file in case of incompatibility
        /// Transpiler patch for Crop.harvest method.
        /// Handles adjusting the quality just before creating the harvested crop object
        /// </summary>
        /// <param name="instructions">the code instructions for Crop.harvest method</param>
        /// <param name="gen">ILGenerator for generating MSIL instructions</param>
        /// <param name="original">Original method's MethodBase</param>
        [HarmonyPatch(nameof(Crop.harvest))]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
        {
            try
            {
                TranspilerHelper helper = new(original, instructions, ModEntry.monitor, gen);
                helper.FindNext(new CodeInstruction[]
                {// if (this.forageCrop) and advance past this
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, typeof(Crop).GetField(nameof(Crop.forageCrop))),
                    new(OpCodes.Call),
                    new(OpCodes.Brfalse),
                })
                .Advance(3)
                .StoreBranchDest()
                .AdvanceToStoredLabel()
                .FindNext(new CodeInstruction[]
                { // find if (this.minHarvest > 1 || this.maxHarvest < 1)
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, typeof(Crop).GetField(nameof(Crop.maxHarvest))),
                    new(OpCodes.Call),
                    new(OpCodes.Ldc_I4_1),
                    new(OpCodes.Ble_S),
                })
                .Advance(4)
                .StoreBranchDest()
                .AdvanceToStoredLabel()
                .FindNext(new CodeInstruction[]
                { // find if(this.indexOfHarvest == )
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld),
                    new(OpCodes.Call),
                    new(OpCodes.Ldc_I4, 771),
                })
                .Push() // we'll be inserting the quality just before this, so temporarily save it.
                .FindNext(new CodeInstruction[]
                {
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Stloc),
                })
                .Advance(1);

                CodeInstruction qualityStLoc = helper.CurrentInstruction.Clone();
                CodeInstruction qualityLdLoc = TranspilerHelper.ToLdLoc(helper.CurrentInstruction);

                helper.Pop()
                .GetLabels(out IList<Label>? jojaLabels, clear: true)
                .Insert(new CodeInstruction[]
                {
                    qualityLdLoc,
                    new(OpCodes.Ldarg_3), // HoeDirt soil
                    new(OpCodes.Call, typeof(CropQualityHandler).GetMethod(nameof(GetModifiedQuality))),
                    qualityStLoc,
                }, withLabels: jojaLabels)
                .FindNext(new CodeInstruction[]
                { // if (this.programColored)
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, typeof(Crop).GetField(nameof(Crop.programColored))),
                    new(OpCodes.Call),
                })
                .FindNext(new CodeInstruction[]
                {
                    new(OpCodes.Ldloc),
                    new(OpCodes.Callvirt, typeof(StardewValley.Object).GetProperty(nameof(StardewValley.Object.Quality)).GetSetMethod()),
                    new(OpCodes.Stloc, typeof(StardewValley.Object)),
                })
                .Advance(2)
                .FindNext(new CodeInstruction[]
                {// if (this.programColored), the second instance.
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldfld, typeof(Crop).GetField(nameof(Crop.programColored))),
                    new(OpCodes.Call),
                })
                .FindNext(new CodeInstruction[]
                { // Find the place where the second creation of an StardewValley.Object/ColoredSObject is saved.
                    new (OpCodes.Newobj, typeof(ColoredObject).GetConstructor(new[] { typeof(int), typeof(int), typeof(Color) } )),
                    new (OpCodes.Stloc, typeof(StardewValley.Object)),
                })
                .Advance(1);

                CodeInstruction secondSObjectLdLoc = TranspilerHelper.ToLdLoc(helper.CurrentInstruction);

                helper
                .Insert(new CodeInstruction[]
                {// Insert instructions to adjust the quality
                    secondSObjectLdLoc,
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Ldarg_3),
                    new(OpCodes.Call, typeof(CropQualityHandler).GetMethod(nameof(GetModifiedQuality))),
                    new(OpCodes.Callvirt, typeof(StardewValley.Object).GetProperty(nameof(StardewValley.Object.Quality)).GetSetMethod()),
                });

                return helper.Render();
            }
            catch (Exception ex)
            {
                ModEntry.monitor.Log($"Mod crashed while transpiling Crop.harvest:\n\n{ex}", LogLevel.Error);
            }
            return null;
        }
    }
}

