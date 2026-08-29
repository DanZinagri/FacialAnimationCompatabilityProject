using System.Collections.Generic;

namespace FACP
{
    // One user-facing toggle. Declared in ToggleCatalogue, which is the source of truth;
    // StartupPatcher reads it to decide what to do at startup.
    public class ToggleEntry
    {
        public string key;
        public string label;
        public string category;
        public string description;

        // packageId of the source mod this patch is for, matching the IfModActive condition
        // on the LoadFolders entry the patch file lives under. Null for patches that are not
        // gated on a mod at all.
        public string packageId;

        public DlcGate dlc;

        // Set on toggles that edit a XenotypeDef's gene list rather than a GeneDef's own
        // fields. A GeneDef edit shows up on existing pawns straight away, because the def
        // itself is what changed; adding or removing a gene from a xenotype only affects
        // pawns generated afterwards. Flagged rows get a tag in the settings list.
        public bool modifiesGenes;

        // Face types this toggle's XML adds. Always added; StartupPatcher parks them on a
        // non-existent gene when the toggle is off.
        public string[] faceTypeDefs;

        // Set on the few patches that are a choice between mutually exclusive treatments
        // rather than a simple on/off. modeKeys[0] is the default. When this is null the
        // entry is an ordinary checkbox.
        public string[] modeKeys;
        public string[] modeLabels;
        public string[] modeDescriptions;

        public bool IsMode
        {
            get { return modeKeys != null && modeKeys.Length > 0; }
        }

        public string DefaultMode
        {
            get { return IsMode ? modeKeys[0] : null; }
        }

        public string CurrentMode
        {
            get { return FACPMod.GetMode(key, DefaultMode); }
        }

        // What StartupPatcher actually acted on this launch, compared against the live
        // setting to work out whether a restart is pending. For a mode entry the string
        // holds the mode that was applied.
        public bool appliedThisSession;
        public string appliedModeThisSession;

        public string Tooltip
        {
            get
            {
                string text = string.IsNullOrEmpty(description) ? label : description;
                if (modifiesGenes)
                {
                    text += "\n\n" + FACPMod.GeneChangeNote;
                }
                return text + "\n\n(" + key + ")";
            }
        }
    }

    public static class ToggleRegistry
    {
        private static Dictionary<string, ToggleEntry> byKey;
        private static List<ToggleEntry> available;

        private static Dictionary<string, ToggleEntry> ByKey
        {
            get
            {
                if (byKey == null)
                {
                    byKey = new Dictionary<string, ToggleEntry>();
                    foreach (ToggleEntry entry in ToggleCatalogue.All)
                    {
                        byKey[entry.key] = entry;
                    }
                }
                return byKey;
            }
        }

        // Look up a single toggle by key. Returns null for an unknown key.
        public static ToggleEntry Get(string key)
        {
            ToggleEntry entry;
            ByKey.TryGetValue(key, out entry);
            return entry;
        }

        // Catalogue order, filtered to the source mods actually installed.
        public static List<ToggleEntry> Entries
        {
            get
            {
                if (available == null)
                {
                    // Grouped by category, categories in the order they first appear in the
                    // catalogue. The settings window opens a new header whenever the category
                    // differs from the previous row, so entries sharing a category have to be
                    // contiguous here or that category would get two headers.
                    available = new List<ToggleEntry>();
                    List<string> categoryOrder = new List<string>();
                    foreach (ToggleEntry entry in ToggleCatalogue.All)
                    {
                        if (ToggleCatalogue.IsAvailable(entry) && !categoryOrder.Contains(entry.category))
                        {
                            categoryOrder.Add(entry.category);
                        }
                    }
                    foreach (string category in categoryOrder)
                    {
                        foreach (ToggleEntry entry in ToggleCatalogue.All)
                        {
                            if (entry.category == category && ToggleCatalogue.IsAvailable(entry))
                            {
                                available.Add(entry);
                            }
                        }
                    }
                }
                return available;
            }
        }

        public static bool AnyModifiesGenes
        {
            get
            {
                foreach (ToggleEntry entry in Entries)
                {
                    if (entry.modifiesGenes)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

    }
}
