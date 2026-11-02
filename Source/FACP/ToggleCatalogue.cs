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
    //
    // No player-facing text lives here. Labels, descriptions and category names are keyed
    // translations in Languages/English/Keyed/FACP_Toggles.xml, looked up by toggle key when
    // the settings window draws - they cannot be read any earlier, because keyed files load
    // after the patches that consult this list.
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
            { "BigAndSmall.OgreHeadUncanny", new[] { "FACP_OgreUncannyNormal", "FACP_OgreUncannySquare", "FACP_OgreUncannyPointy" } },
            { "BigAndSmall.OgreHeadLumpy", new[] { "FACP_OgreLumpyA", "FACP_OgreLumpyB", "FACP_OgreLumpyC", "FACP_OgreLumpyD" } },
            { "Lamias.SnekSnoot", new[] { "LoS_SnekHead", "LoS_SnekHeadMouthBlank" } },
            { "Undead.WerewolfSnoot", new[] { "BS_WerewolfSnoot" } },
            { "Bogleg.FatSac", new[] { "DV_Jaw_FatSac" } },
            { "Brawnum.Snout", new[] { "DV_Nose_Snout_Head" } },
            { "Keshig.SplitJaw", new[] { "DV_Jaw_Split_Head", "DV_Jaw_Split_Jaw" } },
            { "Stoneborn.BushyEyebrows", new[] { "Stoneborn_BlankBrow" } },
            { "Auraeyl.Head", new[] { "ERN_AuraeylHead" } },
            { "Auronya.Head", new[] { "ERN_AuronyaHead" } },
            { "Fenrisulfr.Head", new[] { "ERN_FenrisulfrHead" } },
            { "Expie.Head", new[] { "ERN_ExpieHead" } },
            { "Shisune.Head", new[] { "ERN_ShisuneHead" } },
            { "Rhyaeth.Head", new[] { "ERN_RhyaethHead", "ERN_RhyaethMouthBlank" } },
            { "Faun.DeerHead", new[] { "RBSF_AverageHead1", "RBSF_AverageHead2", "RBSF_AverageHead3", "RBSF_AverageHead4" } },
            { "Minotaur.BovineHead", new[] { "MinotaurNormal", "MinotaurBull1", "MinotaurBull2", "MinotaurBull3", "MinotaurBull4", "MinotaurCow1", "MinotaurCow2", "MinotaurCow3", "MinotaurCow4" } },
            { "Lycanthrope.CanineNose", new[] { "CanineNose" } },
            { "Sanguophage.EarlessHead", new[] { "FACP_SanguophageEarless" } },
            { "Starlet.Ridge", new[] { "FACP_StarletRidge" } },
            { "Geist.BlankFace", new[] { "FACP_GeistBlankEyes", "FACP_GeistBlankLids", "FACP_GeistBlankBrows" } },
            { "Kabouter.EarlessHead", new[] { "FACP_KabouterEarless" } },
            { "Kabouter.BlankBrow", new[] { "FACP_KabouterBlankBrow" } },
            { "Molong.FiendEarless", new[] { "FACP_MolongFiendEarless" } },
            { "Korg.Head", new[] { "FACP_KorgHead" } },
            { "Korg.Mouth", new[] { "FACP_KorgMouthNormal" } },
            { "Elves.EarlessHead", new[] { "FACP_ElvesEarless" } },
            { "RimElves.EarlessHead", new[] { "FACP_RimElvesEarless" } },
            { "Dioktri.SnakeTongue", new[] { "FACP_SnakeMouthNormal", "FACP_SnakeMouthFang", "FACP_SnakeMouthShark" } },
            { "Phytokin.BarkSkin", new[] { "BarkSkin" } },
            { "Saurid.ScaleSkin", new[] { "VRESaurids_ScaleSkin" } }
        };

        // Face types that only exist when a second mod is installed too, so not finding one is
        // expected rather than a sign its patch failed. Fang and Shark come from the
        // Dioktri_Experimentals folder, which loads only alongside FA - Experimentals.
        public static readonly HashSet<string> FaceTypesNeedingAnotherMod = new HashSet<string>
        {
            "FACP_SnakeMouthFang",
            "FACP_SnakeMouthShark"
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
            Add(list, "Vanilla.Gaunt", null, DlcGate.Biotech);
            Add(list, "Vanilla.HeavyJaw", null, DlcGate.Biotech);
            Add(list, "Vanilla.PigNose", null, DlcGate.Biotech);
            Add(list, "Vanilla.YttakinHair", null, DlcGate.Biotech, modifiesGenes: true);

            // ---- Alpha Genes ----
            const string alphaGenesId = "sarg.alphagenes";
            Add(list, "AlphaGenes.FoxFace", alphaGenesId);
            Add(list, "AlphaGenes.AnimusEars", alphaGenesId);
            Add(list, "AlphaGenes.Drakonori", alphaGenesId);
            Add(list, "AlphaGenes.RockSpurs", alphaGenesId);
            Add(list, "AlphaGenes.VenomFangs", alphaGenesId);

            // ---- Big and Small ----
            Add(list, "BigAndSmall.GhoulHead", BigAndSmallCore, DlcGate.Anomaly);
            Add(list, "BigAndSmall.DragonHead", BigAndSmallCore);
            Add(list, "BigAndSmall.OgreHeadUncanny", BigAndSmallCore);
            Add(list, "BigAndSmall.OgreHeadLumpy", BigAndSmallCore);
            Add(list, "BigAndSmall.SatanHead", BigAndSmallCore);
            Add(list, "BigAndSmall.StonemaskHead", BigAndSmallCore);
            Add(list, "BigAndSmall.InsectoidFourArmed", BigAndSmallCore);

            // ---- Lamias and Other Snakes ----
            Add(list, "Lamias.SnekSnoot", "RedMattis.LamiasAndOtherSnakes");

            // ---- Slimes ----
            Add(list, "Slimes.SludgeBody", "RedMattis.BSSlimes");

            // ---- Undead ----
            const string undeadId = "RedMattis.Undead";
            Add(list, "Undead.WerewolfSnoot", undeadId);
            Add(list, "Undead.WerewolfForm", undeadId);

            // ---- Yokai ----
            Add(list, "Yokai.LesserOni", "RedMattis.Yokai", DlcGate.None, modifiesGenes: true);

            // ---- Boglegs ----
            Add(list, "Bogleg.FatSac", "det.boglegs", DlcGate.None, modifiesGenes: true);

            Add(list, "Bogleg.Whiskers", "det.boglegs");

            // ---- Brawnum ----
            Add(list, "Brawnum.Snout", "det.brawnum");

            Add(list, "Brawnum.Bonechin", "det.brawnum");
            Add(list, "Brawnum.BovineEars", "det.brawnum");

            // ---- Venators ----
            Add(list, "Venators.DownturnedEars", "det.venators");

            // ---- Oni ----
            Add(list, "Oni.Ears", "eclair155.onixeno");

            // ---- Keshig ----
            Add(list, "Keshig.SplitJaw", "det.keshig");

            // ---- Stoneborn ----
            Add(list, "Stoneborn.BushyEyebrows", "det.stoneborn");

            // ---- Auraeyl ----
            Add(list, "Auraeyl.Head", "Erin.Auraeyl");

            Add(list, "Auraeyl.SyncSkinColour", "Erin.Auraeyl");

            // ---- Auronya ----
            Add(list, "Auronya.Head", "Erin.AuronyaBiotech");

            Add(list, "Auronya.Whiskers", "Erin.AuronyaBiotech");

            // ---- Fenrisúlfr ----
            Add(list, "Fenrisulfr.Head", "Erin.Fenrisulfr");

            // ---- Expie ----
            Add(list, "Expie.Head", "erin.expie");

            // ---- Shisune ----
            Add(list, "Shisune.Head", "Erin.Shisune");

            // ---- Rhyaeth ----
            Add(list, "Rhyaeth.Head", "erin.rhyaeth");
            Add(list, "Rhyaeth.FaceOverlays", "erin.rhyaeth");

            // ---- Starlet ----
            Add(list, "Starlet.Ridge", "Sovereign.bnuy");

            // ---- Geist ----
            Add(list, "Geist.BlankFace", "Sov.Tenmo");

            // ---- Dioktri ----
            Add(list, "Dioktri.SnakeTongue", "Rimsenal.Dioktri");

            // ---- Kabouter ----
            Add(list, "Kabouter.BulbousNose", "Sovereign.ChipChipChip");
            Add(list, "Kabouter.FluffyBrows", "Sovereign.ChipChipChip");
            Add(list, "Kabouter.HighPointEars", "Sovereign.ChipChipChip");
            Add(list, "Kabouter.EarlessHead", "Sovereign.ChipChipChip");
            Add(list, "Kabouter.BlankBrow", "Sovereign.ChipChipChip");

            // ---- Molong ----
            Add(list, "Molong.Skinhorns", "Sov.Tengri");
            Add(list, "Molong.FiendEars", "Sov.Tengri");
            Add(list, "Molong.BigNose", "Sov.Tengri");
            Add(list, "Molong.TigerEye", "Sov.Tengri");
            Add(list, "Molong.FiendEarless", "Sov.Tengri");

            // ---- Korg ----
            Add(list, "Korg.Head", "Sovereign.Kork");
            Add(list, "Korg.StubbyEars", "Sovereign.Kork");
            Add(list, "Korg.SmushedNose", "Sovereign.Kork");
            Add(list, "Korg.Tusks", "Sovereign.Kork");
            Add(list, "Korg.Mouth", "Sovereign.Kork");

            // ---- Faction - Elves ----
            Add(list, "Elves.EarlessHead", "ICC.FOV.ELVES");

            // ---- Rim-Elves ----
            Add(list, "RimElves.EarlessHead", "VexedTrees980.RimElvesBiotech");

            // ---- Faun ----
            Add(list, "Faun.DeerHead", "V.Rooboid.Faun");

            // ---- Minotaur ----
            AddMode(list, "Minotaur.BovineHead", "tug.Minotaur", new[] { "FacialAnimHeads", "EyesOnly", "Off" });
            Add(list, "Minotaur.NoseRing", "tug.Minotaur");


            // ---- Vanilla Races Expanded ----
            Add(list, "Lycanthrope.CanineNose", "vanillaracesexpanded.lycanthrope");
            Add(list, "Lycanthrope.CanineEars", "vanillaracesexpanded.lycanthrope");
            Add(list, "Lycanthrope.WolfTail", "vanillaracesexpanded.lycanthrope");
            Add(list, "Phytokin.BarkSkin", "vanillaracesexpanded.phytokin");
            Add(list, "Sanguophage.EarlessHead", "vanillaracesexpanded.sanguophage");
            Add(list, "Sanguophage.EkkimianGaunt", "vanillaracesexpanded.sanguophage", DlcGate.None, modifiesGenes: true);
            Add(list, "Sanguophage.BatEars", "vanillaracesexpanded.sanguophage");
            Add(list, "Sanguophage.UpperBatEars", "vanillaracesexpanded.sanguophage");
            Add(list, "Sanguophage.BatNose", "vanillaracesexpanded.sanguophage");
            Add(list, "Saurid.ScaleSkin", "vanillaracesexpanded.saurid");

            // ---- switched on and off in XML, not in the startup pass ----
            // These append into, or replace a value in, a def that belongs to the source mod,
            // so "off" cannot be a no-op the way it is for a def of our own - it has to mean
            // the edit never happens. Their patch files wrap the operation in PatchOperationToggled, which
            // reads the setting while the combined XML document is being built. The cost is
            // that an XML-caching mod freezes them at whatever they were when it cached, so
            // the settings window names them while such a mod is running.
            MarkGatedInXml(list,
                "Auraeyl.Head",       // marking overlay appended to ERN_AuraeylBody
                "Auronya.Whiskers",   // whisker node appended to ERN_AuronyaHead
                "Sanguophage.BatEars",       // texPath replaced on VRE_BatEars
                "Sanguophage.UpperBatEars",  // texPath replaced on VRE_UpperBatEars
                "Sanguophage.BatNose",       // texPath replaced on VRE_BatNose
                "Kabouter.BulbousNose",      // texPath replaced on GS_bulbous_Gnose
                "Kabouter.FluffyBrows",      // texPaths entry replaced on GS_Fluffy_Eyebrows
                "Kabouter.HighPointEars",    // texPath replaced on GS_GHigh_Pointy
                "Molong.Skinhorns",          // texPath replaced on GS_Headbone_Skinhorns
                "Molong.FiendEars",          // texPath replaced on GS_Ears_MFiend
                "Molong.BigNose",            // texPath + texPathFemale replaced on GS_BigNose
                "Molong.TigerEye",           // texPath replaced on the GS_TigerEyeHediff node
                "Korg.StubbyEars",           // texPath replaced on GS_Stubby_Ear
                "Korg.SmushedNose",          // texPath replaced on GS_smushed_nose
                "Korg.Tusks",                // texPaths/li replaced on GS_Tusks
                "Korg.Mouth");               // GS_Tusks node layer lifted above the mouth

            // Minotaur.BovineHead is deliberately not on that list. Its "on" state deletes the
            // source mod's own head node, which no amount of skipping a patch can do, so it
            // stays in StartupPatcher where off means putting that node back.

            return list;
        }

        // Flags the toggles whose XML is gated by PatchOperationToggled. Keys are checked
        // against the list so a rename cannot silently unflag one.
        private static void MarkGatedInXml(List<ToggleEntry> list, params string[] keys)
        {
            foreach (string key in keys)
            {
                ToggleEntry entry = list.Find(e => e.key == key);
                if (entry == null)
                {
                    Log.Error("[FACP] MarkGatedInXml: no toggle named \"" + key + "\".");
                    continue;
                }
                entry.gatedInXml = true;
            }
        }

        // A patch whose options are mutually exclusive. modeKeys[0] is the default.
        private static void AddMode(List<ToggleEntry> list, string key, string packageId, string[] modeKeys,
            DlcGate dlc = DlcGate.None)
        {
            Add(list, key, packageId, dlc);
            list[list.Count - 1].modeKeys = modeKeys;
        }

        // The category is the key's first segment ("Sanguophage" for "Sanguophage.BatEars"),
        // so every toggle of one source mod shares a header without naming it twice.
        private static void Add(List<ToggleEntry> list, string key, string packageId,
            DlcGate dlc = DlcGate.None, bool modifiesGenes = false)
        {
            list.Add(new ToggleEntry
            {
                key = key,
                category = key.Substring(0, key.IndexOf('.')),
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
