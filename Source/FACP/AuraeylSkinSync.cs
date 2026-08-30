using System;
using System.Reflection;
using System.Runtime.CompilerServices;
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

        private static FieldInfo primaryColorField;

        public static void Apply()
        {
            if (GenTypes.GetTypeInAnyAssembly("HarmonyLib.Harmony") == null)
            {
                Log.Message("[FACP] Auraeyl skin sync skipped: Harmony is not loaded. "
                    + "Set each pawn's skin colour to its primary fur colour by hand instead.");
                return;
            }

            Type geneType = GenTypes.GetTypeInAnyAssembly(GeneTypeName);
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

            try
            {
                PatchWithHarmony(target);
            }
            catch (Exception ex)
            {
                Log.Warning("[FACP] Auraeyl skin sync could not patch " + GeneTypeName + ".PostAdd: " + ex);
            }
        }

        // Kept separate and non-inlined so HarmonyLib is only resolved when it exists.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchWithHarmony(MethodInfo target)
        {
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("DanZinagri.FacialAnimationCompatabilityProject");
            MethodInfo postfix = typeof(AuraeylSkinSync).GetMethod(nameof(PostAddPostfix),
                BindingFlags.Public | BindingFlags.Static);
            harmony.Patch(target, null, new HarmonyLib.HarmonyMethod(postfix));
            Log.Message("[FACP] Auraeyl skin sync active: skin colour will follow the primary fur colour.");
        }

        // __instance is typed as the base Gene so this file never needs Erin's assembly at
        // compile time; the primary colour is read reflectively.
        public static void PostAddPostfix(Gene __instance)
        {
            if (primaryColorField == null || __instance == null)
            {
                return;
            }
            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.story == null)
            {
                return;
            }

            object value = primaryColorField.GetValue(__instance);
            if (value is Color primary)
            {
                pawn.story.skinColorOverride = primary;
            }
        }
    }
}
