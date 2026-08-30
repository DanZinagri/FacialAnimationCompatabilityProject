using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace FACP
{
    // Optional, and the only Harmony in this mod.
    //
    // Erin's Gene_SecondColor rolls primaryColor/secondaryColor in PostAdd but never applies
    // primaryColor to the pawn - only the fur colour gizmo does that, via
    // pawn.story.skinColorOverride. Their own known-issues note asks the player to match skin
    // colour to the primary fur colour by hand. This does that assignment at the moment the
    // colours are rolled, so a pawn is correct the instant it is generated.
    //
    // It costs one patched method call per gene add and nothing else - no ticking, no polling.
    //
    // Harmony is a soft dependency. Nothing here is touched unless HarmonyLib is actually
    // loaded: Apply() checks first and the Harmony-using code sits in its own non-inlined
    // method, so the JIT never has to resolve HarmonyLib when it is absent.
    public static class AuraeylSkinSync
    {
        private const string GeneTypeName = "ErinsAuraeyl.Gene_SecondColor";

        private static Type geneType;
        private static FieldInfo primaryColorField;

        public static void Apply()
        {
            if (GenTypes.GetTypeInAnyAssembly("HarmonyLib.Harmony") == null)
            {
                Log.Message("[FACP] Auraeyl skin sync skipped: Harmony is not loaded. "
                    + "Set each pawn's skin colour to its primary fur colour by hand instead.");
                return;
            }

            geneType = GenTypes.GetTypeInAnyAssembly(GeneTypeName);
            if (geneType == null)
            {
                Log.Warning("[FACP] Auraeyl skin sync: " + GeneTypeName + " not found; skipping.");
                return;
            }

            primaryColorField = geneType.GetField("primaryColor",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (primaryColorField == null)
            {
                Log.Warning("[FACP] Auraeyl skin sync: no primaryColor field on " + GeneTypeName
                    + "; the mod may have changed. Skipping.");
                return;
            }

            MethodInfo target = geneType.GetMethod("PostAdd",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (target == null)
            {
                Log.Warning("[FACP] Auraeyl skin sync: no PostAdd on " + GeneTypeName + "; skipping.");
                return;
            }

            // Notify_GenesChanged is the safety net. PostAdd alone is not enough: any later
            // gene add can run EnsureCorrectSkinColorOverride, which reassigns skinColorOverride
            // from gene defs and would drop our value; and pawn editors may build a pawn's genes
            // by a route that never calls PostAdd at all. Both fire only on gene changes.
            MethodInfo genesChanged = typeof(Pawn_GeneTracker).GetMethod("Notify_GenesChanged",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                PatchWithHarmony(target, genesChanged);
            }
            catch (Exception ex)
            {
                Log.Warning("[FACP] Auraeyl skin sync could not patch " + GeneTypeName + ".PostAdd: " + ex);
            }
        }

        // Kept separate and non-inlined so HarmonyLib is only resolved when it exists.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchWithHarmony(MethodInfo postAdd, MethodInfo genesChanged)
        {
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("DanZinagri.FacialAnimationCompatabilityProject");

            harmony.Patch(postAdd, null, new HarmonyLib.HarmonyMethod(
                typeof(AuraeylSkinSync).GetMethod(nameof(PostAddPostfix), BindingFlags.Public | BindingFlags.Static)));

            if (genesChanged != null)
            {
                harmony.Patch(genesChanged, null, new HarmonyLib.HarmonyMethod(
                    typeof(AuraeylSkinSync).GetMethod(nameof(GenesChangedPostfix), BindingFlags.Public | BindingFlags.Static)));
            }
            else
            {
                Log.Warning("[FACP] Auraeyl skin sync: Pawn_GeneTracker.Notify_GenesChanged not found; "
                    + "the colour may be overwritten when other genes are added.");
            }

            Log.Message("[FACP] Auraeyl skin sync active: skin colour will follow the primary fur colour.");
        }

        // __instance is typed as the base Gene so this file never needs Erin's assembly at
        // compile time; the primary colour is read reflectively.
        public static void PostAddPostfix(Gene __instance)
        {
            if (__instance != null)
            {
                TrySync(__instance.pawn);
            }
        }

        public static void GenesChangedPostfix(Pawn_GeneTracker __instance)
        {
            if (__instance != null)
            {
                TrySync(__instance.pawn);
            }
        }

        // Finds the pawn's active second-colour gene and mirrors its primary onto the skin.
        // Runs only on gene changes, and walks a list that is a handful of entries long.
        private static void TrySync(Pawn pawn)
        {
            if (geneType == null || primaryColorField == null
                || pawn == null || pawn.story == null || pawn.genes == null)
            {
                return;
            }

            List<Gene> genes = pawn.genes.GenesListForReading;
            for (int i = 0; i < genes.Count; i++)
            {
                Gene gene = genes[i];
                if (gene == null || !gene.Active || !geneType.IsInstanceOfType(gene))
                {
                    continue;
                }
                if (primaryColorField.GetValue(gene) is Color primary
                    && pawn.story.skinColorOverride != primary)
                {
                    pawn.story.skinColorOverride = primary;

                    // Facial Animation rolls a face type the first time its comp initialises
                    // (InitializeIfNeed -> SetRandomFaceType). If that happens before the
                    // Auraeyl gene is on the pawn, it picks from the no-gene list and lands on
                    // a generic head, which the muzzle marking is not aligned to. The fur
                    // colour gizmo ends with SetAllGraphicsDirty and that is what visibly fixes
                    // it, so do the same here - only on an actual change, so it stays a
                    // one-per-pawn cost rather than anything repeating.
                    Pawn_DrawTracker drawer = pawn.Drawer;
                    if (drawer != null && drawer.renderer != null)
                    {
                        drawer.renderer.SetAllGraphicsDirty();
                    }
                }
                return;
            }
        }
    }
}
