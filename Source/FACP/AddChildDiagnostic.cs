using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace FACP
{
    // TEMPORARY DIAGNOSTIC - remove once the AddChild NullReferenceException is solved.
    //
    // "Exception setting up dynamic nodes for <pawn>: System.NullReferenceException ... at
    // Verse.PawnRenderTree.AddChild" fires intermittently on save load. AddChild only
    // dereferences two things - the child node's Props and the resolved parent's Props - and
    // every feeder in the mod list is null-safe on paper, so this logs which of the four
    // possible nulls actually happened, and what was being added, the moment before vanilla
    // crashes on it. Log-only: it never changes behaviour, so the error still reproduces.
    //
    // Same soft-Harmony rules as AuraeylSkinSync: nothing runs unless HarmonyLib is loaded,
    // and the Harmony-using code sits in its own non-inlined method. Costs one null check per
    // AddChild call, which only happens while a pawn's render tree is being (re)built.
    [StaticConstructorOnStartup]
    public static class AddChildDiagnostic
    {
        private static FieldInfo nodesByTagField;
        private static FieldInfo rootNodeField;

        static AddChildDiagnostic()
        {
            try
            {
                if (GenTypes.GetTypeInAnyAssembly("HarmonyLib.Harmony") == null)
                {
                    return;
                }

                nodesByTagField = typeof(PawnRenderTree).GetField("nodesByTag",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                rootNodeField = typeof(PawnRenderTree).GetField("rootNode",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                MethodInfo target = typeof(PawnRenderTree).GetMethod("AddChild",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (target == null)
                {
                    Log.Warning("[FACP] AddChild diagnostic: PawnRenderTree.AddChild not found; skipping.");
                    return;
                }
                PatchWithHarmony(target);
            }
            catch (Exception ex)
            {
                Log.Warning("[FACP] AddChild diagnostic failed to install: " + ex);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchWithHarmony(MethodInfo target)
        {
            HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("DanZinagri.FACP.AddChildDiagnostic");
            harmony.Patch(target,
                prefix: new HarmonyLib.HarmonyMethod(typeof(AddChildDiagnostic), nameof(Prefix))
                {
                    // Before FA's prefix, so the state is logged even when FA would have
                    // suppressed the node.
                    priority = HarmonyLib.Priority.First
                });
            Log.Message("[FACP] AddChild diagnostic installed (temporary).");
        }

        // Log-only. Mirrors AddChild's own parent resolution to predict the dereference it is
        // about to make, and says which reference is null. Never blocks the original.
        public static void Prefix(PawnRenderTree __instance, Pawn ___pawn,
            PawnRenderNode child, PawnRenderNode parent)
        {
            try
            {
                if (child == null)
                {
                    Log.Warning("[FACP diag] AddChild: CHILD IS NULL for " + PawnStr(___pawn)
                        + ", explicit parent=" + NodeStr(parent));
                    return;
                }
                if (child.Props == null)
                {
                    Log.Warning("[FACP diag] AddChild: child.Props IS NULL for " + PawnStr(___pawn)
                        + ", child=" + child.GetType().Name + DetailStr(child)
                        + ", explicit parent=" + NodeStr(parent));
                    return;
                }

                PawnRenderNode resolved = parent;
                string how = "explicit";
                if (resolved == null)
                {
                    PawnRenderNodeTagDef tag = child.Props.parentTagDef;
                    object value = null;
                    bool found = false;
                    if (tag != null && nodesByTagField != null)
                    {
                        var dict = nodesByTagField.GetValue(__instance)
                            as Dictionary<PawnRenderNodeTagDef, PawnRenderNode>;
                        if (dict != null && dict.TryGetValue(tag, out PawnRenderNode tagged))
                        {
                            value = tagged;
                            found = true;
                        }
                    }
                    if (found)
                    {
                        resolved = (PawnRenderNode)value;
                        how = "nodesByTag[" + child.Props.parentTagDef + "]";
                    }
                    else
                    {
                        resolved = (rootNodeField != null)
                            ? rootNodeField.GetValue(__instance) as PawnRenderNode : null;
                        how = "rootNode (tag=" + (child.Props.parentTagDef?.defName ?? "null") + ")";
                    }
                }

                if (resolved == null)
                {
                    Log.Warning("[FACP diag] AddChild: RESOLVED PARENT IS NULL via " + how
                        + " for " + PawnStr(___pawn) + ", child=" + child.GetType().Name
                        + DetailStr(child));
                }
                else if (resolved.Props == null)
                {
                    Log.Warning("[FACP diag] AddChild: PARENT.Props IS NULL, parent="
                        + resolved.GetType().Name + " via " + how + " for " + PawnStr(___pawn)
                        + ", child=" + child.GetType().Name + DetailStr(child));
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[FACP diag] AddChild diagnostic itself failed: " + ex);
            }
        }

        private static string PawnStr(Pawn pawn)
        {
            return pawn == null ? "null pawn" : pawn.LabelShort;
        }

        private static string NodeStr(PawnRenderNode node)
        {
            if (node == null)
            {
                return "null";
            }
            return node.GetType().Name + (node.Props == null ? " (null Props)" : "");
        }

        // What the child actually is: the gene/apparel/hediff it came from plus its texture,
        // so the culprit mod is identifiable from the log line alone.
        private static string DetailStr(PawnRenderNode child)
        {
            string s = "";
            if (child.gene != null)
            {
                s += " gene=" + child.gene.def.defName;
            }
            if (child.apparel != null)
            {
                s += " apparel=" + child.apparel.def.defName;
            }
            if (child.hediff != null)
            {
                s += " hediff=" + child.hediff.def.defName;
            }
            if (child.Props != null)
            {
                s += " tex=" + (child.Props.texPath ?? child.Props.debugLabel ?? "?");
            }
            return s;
        }
    }
}
