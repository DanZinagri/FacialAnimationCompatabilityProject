using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using FaceTypeDef = FacialAnimation.FaceTypeDef;
using FAHeadTypeDef = FacialAnimation.HeadTypeDef;
using FAMouthTypeDef = FacialAnimation.MouthTypeDef;
using FABrowTypeDef = FacialAnimation.BrowTypeDef;
using FAFaceAdjustmentDef = FacialAnimation.FaceAdjustmentDef;

namespace FACP
{
    // Every edit this mod makes to somebody else's def happens here, once, at startup.
    //
    // The XML patches only ever ADD brand-new defs; they never touch an existing one. So a
    // switched-off toggle does not have to undo anything — the removal simply never runs
    // and the source mod's def is left exactly as it shipped. That is also why an XML
    // caching mod (Gagarin, FasterGameLoading) can no longer strand a setting: the cached
    // document is identical every launch, and the part that varies with your settings is
    // this pass, which runs every time.
    //
    // Nothing here is hooked into the game loop. It runs once during the loading screen
    // and then never again.
    [StaticConstructorOnStartup]
    public static class StartupPatcher
    {
        // A gene defName that cannot exist. Parking a face type on it takes the def out of
        // circulation without deleting it: FaceTypeGenerator offers a type to a pawn either
        // when targetGeneDefs contains one of the pawn's genes, or — and this is the trap —
        // when targetGeneDefs is EMPTY, in which case it becomes a generic head for the
        // whole race. So the list has to be non-empty and unmatchable, never cleared.
        private const string DisabledMarker = "FACP_DisabledByModSettings";

        // Our own ear art. It sits in the root Textures/ folder because several gated
        // patches point at it, and on a FACP/ path so it no longer shadows VRE Lycanthrope.
        private const string CanineEars = "FACP/CanineEars/CanineEars";

        private static readonly string[] FacialDisablerFields =
        {
            "skinName", "lidName", "lidOptionsName", "eyeballName", "browName"
        };

        static StartupPatcher()
        {
            int on = 0;
            int off = 0;
            foreach (ToggleEntry entry in ToggleRegistry.Entries)
            {
                try
                {
                    if (entry.IsMode)
                    {
                        entry.appliedModeThisSession = entry.CurrentMode;
                        ApplyMode(entry, entry.appliedModeThisSession);
                        on++;
                        continue;
                    }

                    entry.appliedThisSession = FACPMod.IsEnabled(entry.key);
                    if (entry.appliedThisSession)
                    {
                        ApplyEnabled(entry.key);
                        on++;
                    }
                    else
                    {
                        Neutralize(entry);
                        ApplyDisabled(entry.key);
                        off++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("[FACP] Toggle \"" + entry.key + "\" failed to apply: " + ex);
                }
            }
            Log.Message("[FACP] Startup pass: " + on + " patches applied, " + off + " switched off, "
                + ToggleRegistry.Entries.Count + " available.");
        }

        // Minotaur is the one patch with mutually exclusive treatments rather than on/off.
        // All the face types are added by XML every launch; whichever set this mode does not
        // want gets parked on a gene that cannot exist.
        private static void ApplyMode(ToggleEntry entry, string mode)
        {
            if (entry.key != "Minotaur.BovineHead")
            {
                Log.Warning("[FACP] No mode handler for \"" + entry.key + "\".");
                return;
            }

            string[] ourHeads =
            {
                "MinotaurNormal", "MinotaurBull1", "MinotaurBull2", "MinotaurBull3", "MinotaurBull4",
                "MinotaurCow1", "MinotaurCow2", "MinotaurCow3", "MinotaurCow4"
            };
            string[] eyesOnly =
            {
                "MinotaurOriginal_RBM_HeadAv1", "MinotaurOriginal_RBM_HeadAv2", "MinotaurOriginal_RBM_HeadAv3",
                "MinotaurOriginal_RBM_HeadNr1", "MinotaurOriginal_RBM_HeadNr2", "MinotaurOriginal_RBM_HeadNr3",
                "MinotaurEyesOnlyMouthBlank"
            };

            switch (mode)
            {
                case "FacialAnimHeads":
                    // Our heads replace the source mod's entirely, so its forced heads go and
                    // our horn node is the one left in the list.
                    NeutralizeFaceTypes(entry.key, eyesOnly);
                    ClearForcedHeads("RBM_BovineHead");
                    KeepOnlyOurBovineNode(true);
                    break;

                case "EyesOnly":
                    // The source mod keeps its forced head types and its own marking node. Its
                    // head art is re-served as a Facial Animation head, because FA deletes the
                    // vanilla head draw request outright for any pawn it draws.
                    NeutralizeFaceTypes(entry.key, ourHeads);
                    KeepOnlyOurBovineNode(false);
                    break;

                case "Off":
                    // Nothing of ours applies at all.
                    NeutralizeFaceTypes(entry.key, ourHeads);
                    NeutralizeFaceTypes(entry.key, eyesOnly);
                    KeepOnlyOurBovineNode(false);
                    break;

                default:
                    // An unrecognised value in the settings file: fall back to the declared
                    // default rather than silently picking whichever branch came last.
                    Log.Warning("[FACP] Unknown mode \"" + mode + "\" for " + entry.key
                        + "; using " + entry.DefaultMode + ".");
                    ApplyMode(entry, entry.DefaultMode);
                    break;
            }
        }

        // Swaps one exact texture path wherever it appears on a gene's render nodes.
        private static void RedirectExactTexPath(string geneDefName, string fromPath, string toPath)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene == null || gene.renderNodeProperties == null)
            {
                return;
            }

            int redirected = 0;
            foreach (PawnRenderNodeProperties node in gene.renderNodeProperties)
            {
                if (node == null)
                {
                    continue;
                }
                if (node.texPath == fromPath)
                {
                    node.texPath = toPath;
                    redirected++;
                }
                if (node.texPaths == null)
                {
                    continue;
                }
                for (int i = 0; i < node.texPaths.Count; i++)
                {
                    if (node.texPaths[i] == fromPath)
                    {
                        node.texPaths[i] = toPath;
                        redirected++;
                    }
                }
            }

            if (redirected == 0)
            {
                Log.Warning("[FACP] " + geneDefName + ": no \"" + fromPath + "\" texture to redirect.");
            }
        }

        // Takes the face types a toggle's XML added back out of play.
        private static void Neutralize(ToggleEntry entry)
        {
            if (entry.faceTypeDefs == null)
            {
                return;
            }
            NeutralizeFaceTypes(entry.key, entry.faceTypeDefs);
        }

        private static void NeutralizeFaceTypes(string key, string[] defNames)
        {
            for (int i = 0; i < defNames.Length; i++)
            {
                string defName = defNames[i];
                FaceTypeDef def = DefDatabase<FAHeadTypeDef>.GetNamedSilentFail(defName);
                if (def == null)
                {
                    def = DefDatabase<FAMouthTypeDef>.GetNamedSilentFail(defName);
                }
                if (def == null)
                {
                    def = DefDatabase<FABrowTypeDef>.GetNamedSilentFail(defName);
                }

                if (def == null)
                {
                    // The source mod is present but the def is missing, so our XML add never
                    // landed. Worth saying out loud rather than silently doing nothing.
                    Log.Warning("[FACP] " + key + ": face type \"" + defName + "\" not found; nothing to switch off.");
                    continue;
                }
                def.targetGeneDefs = new List<string> { DisabledMarker };
            }
        }

        private static void ApplyEnabled(string key)
        {
            switch (key)
            {
                // ---- Vanilla (Biotech) ----
                case "Vanilla.Gaunt":
                case "Vanilla.HeavyJaw":
                    break; // pure additions; the XML did all of it
                case "Vanilla.PigNose":
                    ClearRenderNodes("Nose_Pig");
                    break;
                case "Vanilla.YttakinHair":
                    RemoveGeneFromXenotype("Yttakin", "Hair_BaldOnly");
                    break;

                // ---- Alpha Genes ----
                case "AlphaGenes.FoxFace":
                    ClearForcedHeads("AG_FoxFace");
                    break;
                case "AlphaGenes.AnimusEars":
                    RetextureEars("AG_AnimusEars");
                    break;
                case "AlphaGenes.Drakonori":
                    ClearForcedHeads("AG_DrakonoriHead");
                    break;
                case "AlphaGenes.RockSpurs":
                    ClearRenderNodes("AG_RockSpurs");
                    break;
                case "AlphaGenes.VenomFangs":
                    ClearRenderNodes("AG_VenomFangs");
                    break;

                // ---- Big and Small ----
                case "BigAndSmall.GhoulHead":
                    ClearModExtensions("BS_GhoulHead");
                    // The gene forces Ghoul_Normal/Heavy/Narrow/Wide; without dropping those
                    // the Facial Animation head never gets a look in.
                    ClearForcedHeads("BS_GhoulHead");
                    break;
                case "BigAndSmall.DragonHead":
                    ClearModExtensions("BS_DragonHead");
                    ClearForcedHeads("BS_DragonHead");
                    ClearRenderNodes("BS_DragonHead");
                    break;
                case "BigAndSmall.SatanHead":
                    KeepTheFace("BS_SatanHead");
                    break;
                case "BigAndSmall.StonemaskHead":
                    KeepTheFace("BS_StonemaskHead");
                    break;
                case "BigAndSmall.InsectoidFourArmed":
                    // Mostly belt-and-braces: the race is ParentName="Human", and Facial
                    // Animation adds its comps to Human in XML, so the race already inherits
                    // them. This only matters if B&S ever opts out of that inheritance.
                    AddFacialAnimationComps("BS_InsectoidHumanoid_FourArmed");
                    break;

                // ---- Other Big and Small submods ----
                case "Lamias.SnekSnoot":
                    ClearModExtensions("LoS_SnekSnoot");
                    ClearForcedHeads("LoS_SnekSnoot");
                    break;
                case "Slimes.SludgeBody":
                    ClearForcedHeads("BS_SludgeBody");
                    RemoveRenderNodeByTexPath("BS_SludgeBody", "BS_Heads/Slime/facial_animation/SlimeHead_NoEyes");
                    break;
                case "Undead.WerewolfSnoot":
                    ClearForcedHeads("BS_WerewolfSnoot");
                    // The gene sets disableFacialAnimations, which is why clearing the forced
                    // head alone never showed our snout. No facialDisabler is installed here on
                    // purpose - a null one means B&S disables nothing, whereas the Satan-style
                    // disabler would switch the head controller off and hide the snout.
                    EnableFacialAnimations("BS_WerewolfSnoot");
                    break;
                case "Undead.WerewolfForm":
                    WerewolfForm();
                    break;
                case "Yokai.LesserOni":
                    RemoveGeneFromXenotype("BS_LesserOni", "BS_FacialAnimDisabled");
                    break;

                // ---- Det's xenotypes ----
                case "Bogleg.FatSac":
                    ClearRenderNodes("DV_Jaw_FatSac");
                    RemoveGeneFromXenotype("DV_Bogleg", "Head_Gaunt");
                    break;
                case "Bogleg.Whiskers":
                    // Our art lives on its own path now, so switching this off simply leaves
                    // the gene pointing at the source mod's textures.
                    RedirectTexPaths("DV_Nose_Whiskers",
                        "Things/Pawn/Humanlike/HeadAttachments/Whiskers/", "FACP/Whiskers/");
                    break;
                case "Brawnum.Snout":
                    ClearRenderNodes("DV_Nose_Snout");
                    ClearForcedHeads("DV_Nose_Snout");
                    break;
                case "Brawnum.Bonechin":
                    RedirectTexPaths("DV_Jaw_Bonechin",
                        "Things/Pawn/Humanlike/HeadAttachments/Bonechin/", "FACP/Bonechin/");
                    break;
                case "Brawnum.BovineEars":
                    RedirectTexPaths("DV_Ears_Drooped",
                        "Things/Pawn/Humanlike/HeadAttachments/BovineEars/", "FACP/BovineEars/");
                    break;

                // ---- Venators ----
                case "Venators.DownturnedEars":
                    RedirectTexPaths("DV_Ears_Downturned",
                        "Things/Pawn/Humanlike/HeadAttachments/DownturnedEars/", "FACP/DownturnedEars/");
                    break;

                // ---- Oni ----
                case "Oni.Ears":
                    RedirectTexPaths("OX_Oni_Ear", "Things/Pawn/Parts/", "FACP/Oni/");
                    break;

                case "Keshig.SplitJaw":
                    ClearForcedHeads("DV_Jaw_Split");
                    break;
                case "Stoneborn.BushyEyebrows":
                    break; // pure addition

                // ---- Erin's xenotypes ----
                case "Auraeyl.Head":
                    ClearForcedHeads("ERN_AuraeylHead");
                    break;
                case "Expie.Head":
                case "Shisune.Head":
                    break; // pure additions
                case "Rhyaeth.FaceOverlays":
                    RedirectTexPaths("ERN_RhyaethEars",
                        "Things/Pawn/ERN_Rhyaeth/FaceOverlays/", "FACP/Rhyaeth/");
                    RedirectTexPaths("ERN_RhyaethFaceHorns",
                        "Things/Pawn/ERN_Rhyaeth/FaceOverlays/", "FACP/Rhyaeth/");
                    RedirectTexPaths("ERN_RhyaethFaceFrills",
                        "Things/Pawn/ERN_Rhyaeth/FaceOverlays/", "FACP/Rhyaeth/");
                    // Only the east view of the whiskers is ours; the north and south beside
                    // it are copies of Rhy'aeth's, because Graphic_Multi builds every rotation
                    // from one path prefix and would otherwise reuse east for all of them.
                    RedirectTexPaths("ERN_RhyaethFaceWhiskers",
                        "Things/Pawn/ERN_Rhyaeth/FaceOverlays/", "FACP/Rhyaeth/");
                    break;
                case "Rhyaeth.Head":
                    ClearForcedHeads("ERN_RhyaethHead");
                    break;

                // ---- Roo's xenotypes ----
                case "Faun.DeerHead":
                    ClearForcedHeads("RBSF_DeerHead");
                    break;
                case "Minotaur.NoseRing":
                    // Only bites in the EyesOnly mode, where the source mod's marking node is
                    // the one still drawing. Its ring entry appears several times as a weight,
                    // so every occurrence is swapped.
                    RedirectExactTexPath("RBM_BovineHead",
                        "Things/Pawn/Humanlike/Markings/Head_NoseRing/Ring",
                        "Minotaur/Head_NoseRing/normal");
                    break;

                // ---- Vanilla Races Expanded ----
                case "Lycanthrope.CanineEars":
                    RedirectTexPaths("VRE_CanineEars",
                        "Things/Pawn/Humanlike/HeadAttachments/CanineEars/", "FACP/CanineEars/");
                    break;
                case "Lycanthrope.WolfTail":
                    RedirectTexPaths("VRE_CanineTail",
                        "Things/Pawn/Humanlike/BodyAttachments/WolfTail/", "FACP/WolfTail/");
                    break;
                case "Lycanthrope.CanineNose":
                    ClearRenderNodes("VRE_CanineNose");
                    break;
                case "Phytokin.BarkSkin":
                case "Saurid.ScaleSkin":
                    break; // pure additions

                default:
                    Log.Warning("[FACP] No startup handler for toggle \"" + key + "\".");
                    break;
            }
        }

        // The disabled side of toggles that need more than face-type neutralizing.
        private static void ApplyDisabled(string key)
        {
            if (key == "Minotaur.BovineHead")
            {
                // Drop the node our XML appended, leaving the source mod's own head node.
                KeepOnlyOurBovineNode(false);
            }
            else if (key == "Auraeyl.Head")
            {
                // Switched off: drop our head attachment so the source mod's own head, which
                // its postfix already two-tones, is what draws.
                RemoveAuraeylHead();
            }
            else if (key == "BigAndSmall.InsectoidFourArmed")
            {
                // GraphicHelper keys its lookup on RaceName, defaulting every race to
                // DefaultFaceSizeAndPositionDef. Pointing ours at a race that cannot exist
                // drops the four-armed insectoid back to that default.
                DetachFaceAdjustment("BS_InsectoidHumanoid_FourArmed_FaceAdjustment");
            }
        }

        private static void DetachFaceAdjustment(string defName)
        {
            FAFaceAdjustmentDef adjustment = DefDatabase<FAFaceAdjustmentDef>.GetNamedSilentFail(defName);
            if (adjustment == null)
            {
                Log.Warning("[FACP] Face adjustment \"" + defName + "\" not found; nothing to switch off.");
                return;
            }
            adjustment.RaceName = DisabledMarker;
        }

        private static void RemoveAuraeylHead()
        {
            // The male path just identifies the node; it carries texPathFemale too, so this
            // takes the head attachment away from both genders.
            RemoveRenderNodeByTexPath("ERN_AuraeylBody", "Auraeyl/Male/normal");
        }

        // ---------------- def helpers ----------------

        private static GeneDef Gene(string defName)
        {
            GeneDef gene = DefDatabase<GeneDef>.GetNamedSilentFail(defName);
            if (gene == null)
            {
                Log.Warning("[FACP] Gene \"" + defName + "\" not found; skipping.");
            }
            return gene;
        }

        private static void ClearForcedHeads(string geneDefName)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene != null)
            {
                gene.forcedHeadTypes = null;
            }
        }

        private static void ClearRenderNodes(string geneDefName)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene != null)
            {
                gene.renderNodeProperties = null;
            }
        }

        private static void ClearModExtensions(string geneDefName)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene != null)
            {
                gene.modExtensions = null;
            }
        }

        private static void RemoveGeneFromXenotype(string xenotypeDefName, string geneDefName)
        {
            XenotypeDef xenotype = DefDatabase<XenotypeDef>.GetNamedSilentFail(xenotypeDefName);
            if (xenotype == null || xenotype.genes == null)
            {
                return;
            }
            xenotype.genes.RemoveAll(g => g != null && g.defName == geneDefName);
        }

        // Repoints a gene's render node textures at our replacement art. Only the paths that
        // actually start with the source mod's prefix are touched, so a texture we do not
        // ship a replacement for is left alone.
        private static void RedirectTexPaths(string geneDefName, string fromPrefix, string toPrefix)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene == null || gene.renderNodeProperties == null)
            {
                return;
            }

            int redirected = 0;
            foreach (PawnRenderNodeProperties node in gene.renderNodeProperties)
            {
                if (node == null)
                {
                    continue;
                }
                if (node.texPath != null && node.texPath.StartsWith(fromPrefix))
                {
                    node.texPath = toPrefix + node.texPath.Substring(fromPrefix.Length);
                    redirected++;
                }
                if (node.texPaths == null)
                {
                    continue;
                }
                for (int i = 0; i < node.texPaths.Count; i++)
                {
                    string path = node.texPaths[i];
                    if (path != null && path.StartsWith(fromPrefix))
                    {
                        node.texPaths[i] = toPrefix + path.Substring(fromPrefix.Length);
                        redirected++;
                    }
                }
            }

            if (redirected == 0)
            {
                Log.Warning("[FACP] " + geneDefName + ": no texture paths under \"" + fromPrefix
                    + "\" to redirect; the source mod may have changed them.");
            }
        }

        // Removes the whole render node, matching on one of its texture paths purely as an
        // identifier. A node carries texPath and texPathFemale together and the gender is not
        // resolved until draw time (PawnRenderNode.TexPathFor), so matching the male path
        // still removes the node for every pawn - it is not a per-gender operation.
        private static void RemoveRenderNodeByTexPath(string geneDefName, string identifyingTexPath)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene == null || gene.renderNodeProperties == null)
            {
                return;
            }
            gene.renderNodeProperties.RemoveAll(n => n != null
                && ((n.texPath == identifyingTexPath)
                    || (n.texPaths != null && n.texPaths.Contains(identifyingTexPath))));
        }

        // ---------------- per-toggle work ----------------

        private static void RetextureEars(string geneDefName)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene == null || gene.renderNodeProperties == null)
            {
                return;
            }
            foreach (PawnRenderNodeProperties node in gene.renderNodeProperties)
            {
                node.texPath = CanineEars;
                node.colorType = PawnRenderNodeProperties.AttachmentColorType.Hair;
                node.useSkinShader = false;
            }
        }

        // Stops a Big and Small head gene switching facial animations off, and lifts its
        // overlay above the animated face.
        private static void KeepTheFace(string geneDefName)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene == null)
            {
                return;
            }

            if (gene.renderNodeProperties != null)
            {
                foreach (PawnRenderNodeProperties node in gene.renderNodeProperties)
                {
                    SetDefaultLayer(node.drawData, 50f);
                }
            }

            EnableFacialAnimations(geneDefName);

            if (gene.modExtensions == null)
            {
                return;
            }
            foreach (DefModExtension extension in gene.modExtensions)
            {
                if (extension != null && extension.GetType().Name == "PawnExtension")
                {
                    RetainFacialDisabler(extension);
                }
            }
        }

        // Clears Big and Small's opt-out so Facial Animation is allowed to draw on this gene
        // at all. B&S reads it as allPawnExtensions.Any(x => x.disableFacialAnimations), and
        // the field is a plain bool, so setting it false is the same as never setting it.
        // Deliberately touches nothing but the extension - genes like BS_WerewolfSnoot carry
        // legitimate render nodes (body fur) that must be left alone.
        private static void EnableFacialAnimations(string geneDefName)
        {
            GeneDef gene = Gene(geneDefName);
            if (gene == null || gene.modExtensions == null)
            {
                return;
            }

            bool found = false;
            foreach (DefModExtension extension in gene.modExtensions)
            {
                if (extension == null || extension.GetType().Name != "PawnExtension")
                {
                    continue;
                }
                found = true;
                SetFieldIfPresent(extension, "disableFacialAnimations", false);
            }

            if (!found)
            {
                Log.Warning("[FACP] " + geneDefName + ": no BigAndSmall.PawnExtension to re-enable "
                    + "facial animations on; the gene may have changed.");
            }
        }

        private static void WerewolfForm()
        {
            HediffDef hediff = DefDatabase<HediffDef>.GetNamedSilentFail("BS_WerewolfForm");
            if (hediff == null)
            {
                Log.Warning("[FACP] Hediff \"BS_WerewolfForm\" not found; skipping.");
                return;
            }

            {
                foreach (PawnRenderNodeProperties node in hediff.RenderNodeProperties)
                {
                    if (node != null && node.texPath == "BS_HeadAttachments/BS_GenericAnimalPersonEars")
                    {
                        node.texPath = CanineEars;
                    }
                }
            }

            if (hediff.comps == null)
            {
                return;
            }
            foreach (HediffCompProperties comp in hediff.comps)
            {
                if (comp != null && comp.GetType().Name == "CompProperties_ColorAndFur")
                {
                    SetFieldIfPresent(comp, "disableFacialAnims", false);
                }
            }
        }

        // Our blank-horn node and the source mod's own head node cannot both draw. Whichever
        // side is switched off gets dropped from the list; nothing is destroyed either way.
        private static void KeepOnlyOurBovineNode(bool keepOurs)
        {
            GeneDef gene = Gene("RBM_BovineHead");
            if (gene == null || gene.renderNodeProperties == null)
            {
                return;
            }
            // Guard: if our node never landed, "remove everything that isn't ours" would
            // strip the gene's own head node and leave it with nothing to draw.
            bool ourNodePresent = gene.renderNodeProperties.Exists(IsOurBovineNode);
            bool theirNodePresent = gene.renderNodeProperties.Exists(n => !IsOurBovineNode(n));
            if (!ourNodePresent || !theirNodePresent)
            {
                Log.Warning("[FACP] Minotaur.BovineHead: expected both our node and the source mod's "
                    + "node on RBM_BovineHead (ours=" + ourNodePresent + ", theirs=" + theirNodePresent
                    + "); leaving the list alone.");
                return;
            }
            gene.renderNodeProperties.RemoveAll(n => IsOurBovineNode(n) != keepOurs);
        }

        private static bool IsOurBovineNode(PawnRenderNodeProperties node)
        {
            return node != null && node.texPaths != null
                && node.texPaths.Contains("Minotaur/Head_Blank/normal");
        }

        private static void AddFacialAnimationComps(string thingDefName)
        {
            ThingDef race = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
            if (race == null)
            {
                Log.Warning("[FACP] ThingDef \"" + thingDefName + "\" not found; skipping.");
                return;
            }

            string[] compTypeNames =
            {
                "FacialAnimation.DrawFaceGraphicsComp",
                "FacialAnimation.HeadControllerComp",
                "FacialAnimation.EyeballControllerComp",
                "FacialAnimation.LidControllerComp",
                "FacialAnimation.BrowControllerComp",
                "FacialAnimation.MouthControllerComp",
                "FacialAnimation.SkinControllerComp",
                "FacialAnimation.FacialAnimationControllerComp"
            };

            if (race.comps == null)
            {
                race.comps = new List<CompProperties>();
            }
            foreach (string compTypeName in compTypeNames)
            {
                Type compType = GenTypes.GetTypeInAnyAssembly(compTypeName);
                if (compType == null)
                {
                    Log.Warning("[FACP] Comp class \"" + compTypeName + "\" not found; skipping.");
                    continue;
                }
                Type captured = compType;
                if (!race.comps.Exists(c => c != null && c.compClass == captured))
                {
                    race.comps.Add(new CompProperties { compClass = captured });
                }
            }
        }

        // ---------------- reflection helpers ----------------

        // DrawData.defaultData is a private struct field, so it has to be boxed, edited and
        // written back. Failing to find it is not fatal — the overlay just keeps its layer.
        private static void SetDefaultLayer(DrawData drawData, float layer)
        {
            if (drawData == null)
            {
                return;
            }
            FieldInfo field = typeof(DrawData).GetField("defaultData",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Log.Warning("[FACP] DrawData.defaultData not found; overlay layer left alone.");
                return;
            }
            object boxed = field.GetValue(drawData);
            FieldInfo layerField = boxed.GetType().GetField("layer");
            if (layerField == null)
            {
                return;
            }
            layerField.SetValue(boxed, new float?(layer));
            field.SetValue(drawData, boxed);
        }

        // Big and Small's FacialAnimDisabler defaults every name to "NOT_", and the consumer
        // reads it as debugEnabled = !name.Contains("NOT_") - so a field left alone means that
        // part stays switched off. Setting these five re-enables skin, lids, lid options,
        // eyeballs and brows, while headName and mouthName stay "NOT_" on purpose: these genes
        // draw their own head overlay over the face, so FA's head and mouth should not also
        // draw. Do not call this for a gene whose whole point is a Facial Animation head.
        private static void RetainFacialDisabler(object pawnExtension)
        {
            FieldInfo field = pawnExtension.GetType().GetField("facialDisabler");
            if (field == null)
            {
                return;
            }
            object disabler = field.GetValue(pawnExtension);
            if (disabler == null)
            {
                try
                {
                    disabler = Activator.CreateInstance(field.FieldType);
                }
                catch (Exception)
                {
                    return;
                }
                field.SetValue(pawnExtension, disabler);
            }
            for (int i = 0; i < FacialDisablerFields.Length; i++)
            {
                SetFieldIfPresent(disabler, FacialDisablerFields[i], "retain");
            }
        }

        private static void SetFieldIfPresent(object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }
            FieldInfo field = target.GetType().GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }
}
