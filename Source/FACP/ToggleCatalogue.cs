using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FACP
{
    public enum DlcGate
    {
        None,
        Biotech,
        Anomaly
    }

    // The authoritative list of every toggle this mod ships, gated the same way
    // LoadFolders.xml gates the folder each patch lives in.
    //
    // This is declared here rather than gathered from the patch operations as they run,
    // because XML-caching mods (Gargarin, FasterGameLoading and friends) serve a
    // pre-combined document and skip ApplyPatches entirely on a cached launch — so the
    // operations may never execute. Presence of the source mod is the same thing
    // LoadFolders keys on, so deciding from that gives the identical answer without
    // depending on anything having run.
    //
    // Declaration order is display order; categories appear in the order first seen here.
    public static class ToggleCatalogue
    {
        public const string BigAndSmallCore = "RedMattis.BigSmall.Core";

        // Face types each toggle's XML adds, by key. These defs always exist; StartupPatcher
        // parks the ones belonging to a switched-off toggle on a gene that cannot exist.
        // Toggles absent from this table add no face types at all.
        private static readonly Dictionary<string, string[]> AddedFaceTypes = new Dictionary<string, string[]>
        {
            { "Vanilla.Gaunt", new[] { "Gaunt" } },
            { "Vanilla.HeavyJaw", new[] { "HeavyJaw" } },
            { "Vanilla.PigNose", new[] { "Nose_Pig" } },
            { "AlphaGenes.FoxFace", new[] { "AG_FoxFace" } },
            { "AlphaGenes.Drakonori", new[] { "AG_DrakonoriHead", "AG_DrakonoriHeadMouthBlank" } },
            { "AlphaGenes.RockSpurs", new[] { "AG_RockSpursHead" } },
            { "BigAndSmall.GhoulHead", new[] { "BS_GhoulHead" } },
            { "BigAndSmall.DragonHead", new[] { "BS_DragonHead", "BS_DragonHeadMouthBlank" } },
            { "Lamias.SnekSnoot", new[] { "LoS_SnekHead", "LoS_SnekHeadMouthBlank" } },
            { "Undead.WerewolfSnoot", new[] { "BS_WerewolfSnoot" } },
            { "Bogleg.FatSac", new[] { "DV_Jaw_FatSac" } },
            { "Brawnum.Snout", new[] { "DV_Nose_Snout_Head" } },
            { "Keshig.SplitJaw", new[] { "DV_Jaw_Split_Head", "DV_Jaw_Split_Jaw" } },
            { "Stoneborn.BushyEyebrows", new[] { "Stoneborn_BlankBrow" } },
            { "Auraeyl.Head", new[] { "ERN_AuraeylHead" } },
            { "Expie.Head", new[] { "ERN_ExpieHead" } },
            { "Shisune.Head", new[] { "ERN_ShisuneHead" } },
            { "Rhyaeth.Head", new[] { "ERN_RhyaethHead", "ERN_RhyaethMouthBlank" } },
            { "Faun.DeerHead", new[] { "RBSF_AverageHead1", "RBSF_AverageHead2", "RBSF_AverageHead3", "RBSF_AverageHead4" } },
            { "Minotaur.BovineHead", new[] { "MinotaurNormal", "MinotaurBull1", "MinotaurBull2", "MinotaurBull3", "MinotaurBull4", "MinotaurCow1", "MinotaurCow2", "MinotaurCow3", "MinotaurCow4" } },
            { "Lycanthrope.CanineNose", new[] { "CanineNose" } },
            { "Phytokin.BarkSkin", new[] { "BarkSkin" } },
            { "Saurid.ScaleSkin", new[] { "VRESaurids_ScaleSkin" } }
        };

        private static List<ToggleEntry> entries;

        public static List<ToggleEntry> All
        {
            get
            {
                if (entries == null)
                {
                    entries = Build();
                }
                return entries;
            }
        }

        private static List<ToggleEntry> Build()
        {
            List<ToggleEntry> list = new List<ToggleEntry>();

            // ---- Vanilla (Biotech) ----
            const string vanilla = "Vanilla (Biotech)";
            Add(list, "Vanilla.Gaunt", "Gaunt head", vanilla,
                "Gives the Head_Gaunt gene (wasters and friends) a Facial Animation head type instead of a vanilla one.",
                null, DlcGate.Biotech);
            Add(list, "Vanilla.HeavyJaw", "Heavy jaw head", vanilla,
                "Gives the Jaw_Heavy gene (neanderthals and friends) a Facial Animation head type instead of a vanilla one.",
                null, DlcGate.Biotech);
            Add(list, "Vanilla.PigNose", "Pig nose head", vanilla,
                "Bakes the Nose_Pig gene's snout into a Facial Animation head instead of drawing it as a render node over the face.",
                null, DlcGate.Biotech);
            Add(list, "Vanilla.YttakinHair", "Yttakin - allow hair", vanilla,
                "Drops Hair_BaldOnly from the Yttakin xenotype so they can roll hair styles.",
                null, DlcGate.Biotech, modifiesGenes: true);

            // ---- Alpha Genes ----
            const string alphaGenes = "Alpha Genes";
            const string alphaGenesId = "sarg.alphagenes";
            Add(list, "AlphaGenes.FoxFace", "Fox face", alphaGenes,
                "Replaces AG_FoxFace's forced vanilla head types with a Facial Animation canine-snout head.",
                alphaGenesId);
            Add(list, "AlphaGenes.AnimusEars", "Animus ears", alphaGenes,
                "Swaps AG_AnimusEars over to this mod's canine ear texture, hair-coloured and off the skin shader.",
                alphaGenesId);
            Add(list, "AlphaGenes.Drakonori", "Drakonori head", alphaGenes,
                "Gives AG_DrakonoriHead the shared dragon-snout Facial Animation head, with a blank mouth so the snout reads as one piece.",
                alphaGenesId);
            Add(list, "AlphaGenes.RockSpurs", "Rock spurs head", alphaGenes,
                "Bakes AG_RockSpurs' facial rock growths into a Facial Animation head instead of overlaying them as a render node.",
                alphaGenesId);
            Add(list, "AlphaGenes.VenomFangs", "Venom fangs - hide overlay", alphaGenes,
                "Drops AG_VenomFangs' render node, which otherwise draws fangs across a Facial Animation mouth.",
                alphaGenesId);

            // ---- Big and Small ----
            const string bigAndSmall = "Big and Small - Genes & More";
            Add(list, "BigAndSmall.GhoulHead", "Ghoul head", bigAndSmall,
                "Gives BS_GhoulHead a Facial Animation head type.",
                BigAndSmallCore, DlcGate.Anomaly);
            Add(list, "BigAndSmall.DragonHead", "Dragonhead broodmother", bigAndSmall,
                "Gives BS_DragonHead the shared dragon-snout Facial Animation head plus a blank mouth, in place of its own render node.",
                BigAndSmallCore);
            Add(list, "BigAndSmall.SatanHead", "Satan head - keep the face", bigAndSmall,
                "Stops BS_SatanHead disabling facial animations, and lifts its overlay to layer 50 so it draws above the animated face.",
                BigAndSmallCore);
            Add(list, "BigAndSmall.StonemaskHead", "Authority stonemask - keep the face", bigAndSmall,
                "Stops BS_StonemaskHead disabling facial animations, and lifts its overlay to layer 50 so it draws above the animated face.",
                BigAndSmallCore);
            Add(list, "BigAndSmall.InsectoidFourArmed", "Four-armed insectoid - face support", bigAndSmall,
                "Makes sure the four-armed insectoid race has the Facial Animation comps, sizes its face to its head, "
                + "and labels its eyes as compound eyes. Switched off, the race falls back to Facial Animation's "
                + "default face size and position. The race normally inherits Facial Animation from Human, so "
                + "switching this off may not remove the face itself.",
                BigAndSmallCore);

            // ---- Lamias and Other Snakes ----
            Add(list, "Lamias.SnekSnoot", "Snake snout head", "Big and Small - Lamias and other Snake-People",
                "Gives LoS_SnekSnoot the shared dragon-snout Facial Animation head plus a blank mouth, in place of its forced head types.",
                "RedMattis.LamiasAndOtherSnakes");

            // ---- Slimes ----
            Add(list, "Slimes.SludgeBody", "Sludge body - use normal heads", "Big and Small - Slimes",
                "Drops BS_SludgeBody's forced head types and its eyeless slime head node, so slimes render an ordinary animated face.",
                "RedMattis.BSSlimes");

            // ---- Undead ----
            const string undead = "Big and Small - Vampires and the Undead";
            const string undeadId = "RedMattis.Undead";
            Add(list, "Undead.WerewolfSnoot", "Werewolf snout head", undead,
                "Gives BS_WerewolfSnoot the shared canine-snout Facial Animation head in place of its forced head types.",
                undeadId);
            Add(list, "Undead.WerewolfForm", "Werewolf form - keep the face", undead,
                "Stops the werewolf transformation hediff disabling facial animations, and swaps its ears over to this mod's canine ear texture.",
                undeadId);

            // ---- Yokai ----
            Add(list, "Yokai.LesserOni", "Lesser oni - enable faces", "Big and Small - Yokai",
                "Drops BS_FacialAnimDisabled from the lesser oni xenotype so they get an animated face.",
                "RedMattis.Yokai", DlcGate.None, modifiesGenes: true);

            // ---- Boglegs ----
            Add(list, "Bogleg.FatSac", "Fat sac jaw head", "Boglegs",
                "Bakes DV_Jaw_FatSac's throat sac into a Facial Animation head, and drops Head_Gaunt from the bogleg xenotype so it does not fight the new head.",
                "det.boglegs", DlcGate.None, modifiesGenes: true);

            Add(list, "Bogleg.Whiskers", "Barbels - replacement art", "Boglegs",
                "Points the barbel gene at this mod's upscaled, realigned barbel textures. Switched off, "
                + "Boglegs draws its own art, which the replacement is deliberately not aligned to.",
                "det.boglegs");

            // ---- Brawnum ----
            Add(list, "Brawnum.Snout", "Snout head", "Brawnum",
                "Bakes DV_Nose_Snout into a Facial Animation head instead of overlaying it as a render node.",
                "det.brawnum");

            Add(list, "Brawnum.Bonechin", "Bone chin - replacement art", "Brawnum",
                "Points the bone chin gene at this mod's realigned chin textures. Switched off, Brawnum "
                + "draws its own art, which the replacement is deliberately not aligned to.",
                "det.brawnum");
            Add(list, "Brawnum.BovineEars", "Bovine ears - replacement art", "Brawnum",
                "Points the drooped ear gene at this mod's realigned ear texture. Switched off, Brawnum "
                + "draws its own art, which the replacement is deliberately not aligned to.",
                "det.brawnum");

            // ---- Venators ----
            Add(list, "Venators.DownturnedEars", "Downturned ears - replacement art", "Venators",
                "Points the downturned ear gene at this mod's realigned ear textures. Switched off, "
                + "Venators draws its own art, which the replacement is deliberately not aligned to.",
                "det.venators");

            // ---- Oni ----
            Add(list, "Oni.Ears", "Oni ears - replacement art", "Oni",
                "Points the oni ear gene at this mod's realigned ear textures. Switched off, Oni Xenotype "
                + "draws its own art, which the replacement is deliberately not aligned to.",
                "eclair155.onixeno");

            // ---- Keshig ----
            Add(list, "Keshig.SplitJaw", "Split jaw head and mouth", "Keshig",
                "Replaces DV_Jaw_Split's forced head types with a Facial Animation head plus a matching split-jaw mouth type.",
                "det.keshig");

            // ---- Stoneborn ----
            Add(list, "Stoneborn.BushyEyebrows", "Bushy eyebrows - blank brow", "Stoneborn",
                "Adds a zero-probability blank brow type for DV_BushyEyebrows, so the gene's own brow art is not doubled up by an animated one.",
                "det.stoneborn");

            // ---- Auraeyl ----
            Add(list, "Auraeyl.Head", "Auraeyl head", "Auraeyl",
                "Swaps the Auraeyl head for this mod's version, drawn under a transparent Facial Animation "
                + "head so the animated eyes and mouth sit on top of it. Keeps the pawn's primary and "
                + "secondary fur colours, matching the body from the moment they spawn.",
                "Erin.Auraeyl");

            Add(list, "Auraeyl.SyncSkinColour", "Auraeyl skin follows fur colour", "Auraeyl",
                "Applies the pawn's primary fur colour to its skin the moment the gene rolls its "
                + "colours, so the head and eyelid are right from the start instead of needing the fur "
                + "colour gizmo. Needs Harmony; without it this does nothing and the colour has to be "
                + "set by hand, as Erin's mod documents.",
                "Erin.Auraeyl");

            // ---- Expie ----
            Add(list, "Expie.Head", "Expie head", "Expie",
                "Adds a Facial Animation head type for ERN_ExpieHead.",
                "erin.expie");

            // ---- Shisune ----
            Add(list, "Shisune.Head", "Shisune head", "Shisune",
                "Adds a Facial Animation head type for ERN_ShisuneHead.",
                "Erin.Shisune");

            // ---- Rhyaeth ----
            Add(list, "Rhyaeth.Head", "Rhyaeth head", "Rhyaeth",
                "Gives ERN_RhyaethHead the shared dragon-snout Facial Animation head plus a blank mouth, in place of its forced head types.",
                "erin.rhyaeth");
            Add(list, "Rhyaeth.FaceOverlays", "Face overlays - replacement art", "Rhyaeth",
                "Points the ear, horn, frill and whisker genes at this mod's realigned overlay art. "
                + "Switched off, Rhy'aeth draws its own.",
                "erin.rhyaeth");

            // ---- Faun ----
            Add(list, "Faun.DeerHead", "Deer heads", "Faun",
                "Replaces RBSF_DeerHead's forced head types with four Facial Animation deer heads, rolled evenly.",
                "V.Rooboid.Faun");

            // ---- Minotaur ----
            AddMode(list, "Minotaur.BovineHead", "Bovine heads", "Minotaur", "tug.Minotaur",
                new[] { "FacialAnimHeads", "EyesOnly", "Off" },
                new[]
                {
                    "NL Humanoid Bovine Heads",
                    "Original heads, animated eyes only",
                    "Off"
                },
                new[]
                {
                    "Swaps the forced head types for nine Facial Animation bovine heads and this mod's "
                        + "horn attachment, giving a fully animated face.",
                    "Keeps the source mod's own bovine heads and markings, and lets Facial Animation draw "
                        + "only the eyes, lids and brows over them. The animated head and mouth are blanked, "
                        + "since neither would line up with the bovine muzzle.",
                    "Minotaurs render exactly as their own mod draws them, with no animated face."
                });
            Add(list, "Minotaur.NoseRing", "Nose ring - replacement art", "Minotaur",
                "Points the source mod's nose ring marking at this mod's ring texture. Only has an effect in "
                + "the \"original heads\" mode, where the source mod's own markings are the ones drawing.",
                "tug.Minotaur");


            // ---- Vanilla Races Expanded ----
            Add(list, "Lycanthrope.CanineNose", "Canine nose head", "VRE - Lycanthrope",
                "Bakes VRE_CanineNose into the shared canine-snout Facial Animation head instead of overlaying it as a render node.",
                "vanillaracesexpanded.lycanthrope");
            Add(list, "Lycanthrope.CanineEars", "Canine ears - replacement art", "VRE - Lycanthrope",
                "Points VRE's own canine ear gene at this mod's ear texture. Switched off, Lycanthrope draws "
                + "its own ears; the Alpha Genes and werewolf patches keep using ours either way.",
                "vanillaracesexpanded.lycanthrope");
            Add(list, "Lycanthrope.WolfTail", "Wolf tail - replacement art", "VRE - Lycanthrope",
                "Points the canine tail gene at this mod's realigned tail art. Switched off, Lycanthrope "
                + "draws its own.",
                "vanillaracesexpanded.lycanthrope");
            Add(list, "Phytokin.BarkSkin", "Barkskin head", "VRE - Phytokin",
                "Adds a Facial Animation head type for VRE_BarkSkin.",
                "vanillaracesexpanded.phytokin");
            Add(list, "Saurid.ScaleSkin", "Scaled skin head", "VRE - Saurid",
                "Adds a Facial Animation head type for VRESaurids_ScaleSkin.",
                "vanillaracesexpanded.saurid");

            return list;
        }

        // A patch whose options are mutually exclusive. modeKeys[0] is the default.
        private static void AddMode(List<ToggleEntry> list, string key, string label, string category,
            string packageId, string[] modeKeys, string[] modeLabels, string[] modeDescriptions,
            DlcGate dlc = DlcGate.None)
        {
            Add(list, key, label, category, null, packageId, dlc);
            ToggleEntry entry = list[list.Count - 1];
            entry.modeKeys = modeKeys;
            entry.modeLabels = modeLabels;
            entry.modeDescriptions = modeDescriptions;
        }

        private static void Add(List<ToggleEntry> list, string key, string label, string category,
            string description, string packageId, DlcGate dlc = DlcGate.None, bool modifiesGenes = false)
        {
            list.Add(new ToggleEntry
            {
                key = key,
                label = label,
                category = category,
                description = description,
                packageId = packageId,
                dlc = dlc,
                modifiesGenes = modifiesGenes
            });

            string[] faceTypes;
            if (AddedFaceTypes.TryGetValue(key, out faceTypes))
            {
                list[list.Count - 1].faceTypeDefs = faceTypes;
            }
        }

        // Mirrors LoadFolders' IfModActive, including its handling of the _steam packageId
        // suffix a local-plus-Workshop install produces.
        public static bool IsAvailable(ToggleEntry entry)
        {
            switch (entry.dlc)
            {
                case DlcGate.Biotech:
                    if (!ModsConfig.BiotechActive)
                    {
                        return false;
                    }
                    break;
                case DlcGate.Anomaly:
                    if (!ModsConfig.AnomalyActive)
                    {
                        return false;
                    }
                    break;
            }

            if (string.IsNullOrEmpty(entry.packageId))
            {
                return true;
            }
            return ModLister.GetActiveModWithIdentifier(entry.packageId, ignorePostfix: true) != null;
        }
    }
}
