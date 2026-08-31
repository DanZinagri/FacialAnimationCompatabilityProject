using System.Collections.Generic;
using System.Xml;
using Verse;

namespace FACP
{
    // Runs its child operations only when the matching settings toggle is on.
    //
    // Mod settings are readable this early because LoadedModManager.LoadAllActiveMods() runs
    // CreateModClasses() - where FACPMod's constructor calls GetSettings<T>() - before
    // ErrorCheckPatches() and ApplyPatches(). So a switched-off patch is simply never applied
    // and there is nothing for the startup pass to undo afterwards.
    //
    // Only wrap operations that ADD something. "Off" here means the operation never runs, which
    // is not the same as putting something back: anything that has to REMOVE part of another
    // mod's def still belongs in StartupPatcher, because there "off" is itself an edit.
    //
    // The catch, and it is a real one: an XML-caching mod (Gagarin, bundled in Missile Girl)
    // serves a pre-combined document and never calls ApplyPatches at all, so a toggle changed
    // since that cache was written keeps whatever it was set to then. Clearing the cache is the
    // only fix, and the settings window says so whenever such a mod is running.
    public class PatchOperationToggled : PatchOperation
    {
        // Toggle key from ToggleCatalogue. Labels and descriptions stay over there so there is
        // one source of truth; ConfigErrors below catches the two drifting apart.
        public string key;

        // For a multiple-choice toggle: the mode this block belongs to. Ignored for an
        // ordinary on/off toggle.
        public string mode;

        // Further toggles that must also be on. Used where one patch only makes sense as part
        // of another - the Auronya whiskers ride the head patch, for instance.
        public List<string> alsoRequires;

        public List<PatchOperation> operations = new List<PatchOperation>();

        private readonly List<string> failures = new List<string>();
        private bool skipped;

        public override IEnumerable<string> ConfigErrors()
        {
            if (string.IsNullOrEmpty(key))
            {
                yield return "PatchOperationToggled with no key.";
                yield break;
            }

            ToggleEntry entry = ToggleRegistry.Get(key);
            if (entry == null)
            {
                yield return "PatchOperationToggled key \"" + key + "\" has no entry in ToggleCatalogue.";
                yield break;
            }

            if (operations.Count == 0)
            {
                yield return "PatchOperationToggled \"" + key + "\" has no operations.";
            }

            if (mode != null)
            {
                if (!entry.IsMode)
                {
                    yield return "PatchOperationToggled \"" + key + "\" sets a mode, but that toggle is "
                        + "a plain on/off.";
                }
                else if (System.Array.IndexOf(entry.modeKeys, mode) < 0)
                {
                    yield return "PatchOperationToggled \"" + key + "\" names unknown mode \"" + mode + "\".";
                }
            }
            else if (entry.IsMode)
            {
                yield return "PatchOperationToggled \"" + key + "\" is a multiple-choice toggle but names "
                    + "no mode, so it would apply in every mode.";
            }

            if (alsoRequires != null)
            {
                foreach (string other in alsoRequires)
                {
                    if (ToggleRegistry.Get(other) == null)
                    {
                        yield return "PatchOperationToggled \"" + key + "\" requires \"" + other
                            + "\", which has no entry in ToggleCatalogue.";
                    }
                }
            }
        }

        protected override bool ApplyWorker(XmlDocument xml)
        {
            // Returning true for a skip is not optional: Complete() reports any operation that
            // never once succeeded as a failed patch, and a switched-off toggle is not a failure.
            if (!ShouldApply())
            {
                skipped = true;
                return true;
            }

            // Every child runs even after one fails. PatchOperationSequence stops at the first
            // failure, which would let a single stale xpath quietly take the rest with it.
            bool allApplied = true;
            for (int i = 0; i < operations.Count; i++)
            {
                PatchOperation op = operations[i];
                if (op == null)
                {
                    continue;
                }

                // Vanilla only fills sourceFile in on top-level operations, so children would
                // otherwise report errors with no file name attached.
                if (string.IsNullOrEmpty(op.sourceFile))
                {
                    op.sourceFile = sourceFile;
                }

                if (!op.Apply(xml))
                {
                    failures.Add(op.GetType().Name + " (#" + (i + 1) + ")");
                    allApplied = false;
                }
            }
            return allApplied;
        }

        private bool ShouldApply()
        {
            if (mode != null)
            {
                ToggleEntry entry = ToggleRegistry.Get(key);
                if (entry == null || FACPMod.GetMode(key, entry.DefaultMode) != mode)
                {
                    return false;
                }
            }
            else if (!FACPMod.IsEnabled(key))
            {
                return false;
            }

            if (alsoRequires != null)
            {
                for (int i = 0; i < alsoRequires.Count; i++)
                {
                    if (!FACPMod.IsEnabled(alsoRequires[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public override void Complete(string modIdentifier)
        {
            // Named individually, because the base class only says "patch operation failed"
            // and a wrapper full of children makes that impossible to act on.
            if (failures.Count > 0)
            {
                Log.Error("[FACP] Toggle \"" + key + "\": " + failures.Count + " of " + operations.Count
                    + " operations failed (" + string.Join(", ", failures.ToArray()) + ")."
                    + (string.IsNullOrEmpty(sourceFile) ? "" : "\nfile: " + sourceFile));
                return;
            }

            if (!skipped)
            {
                base.Complete(modIdentifier);
            }
        }

        public override string ToString()
        {
            return GetType().Name + "(" + key + (mode == null ? "" : " = " + mode) + ")";
        }
    }
}
