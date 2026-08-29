using System.Collections.Generic;
using Verse;

namespace FACP
{
    // Stores the switched-off keys rather than the switched-on ones, so everything
    // defaults to enabled and a newly added patch turns itself on without the settings
    // file needing to know about it. Also keeps the file down to a handful of lines.
    //
    // A few patches are not a simple on/off - they pick one of several mutually exclusive
    // treatments. Those are stored separately by key, and an absent key means the default.
    public class FACPSettings : ModSettings
    {
        private HashSet<string> disabled = new HashSet<string>();

        private Dictionary<string, string> modes = new Dictionary<string, string>();

        public bool IsEnabled(string key)
        {
            return !disabled.Contains(key);
        }

        public void SetEnabled(string key, bool enabled)
        {
            if (enabled)
            {
                disabled.Remove(key);
            }
            else
            {
                disabled.Add(key);
            }
        }

        public string GetMode(string key, string defaultMode)
        {
            string mode;
            if (modes.TryGetValue(key, out mode) && !mode.NullOrEmpty())
            {
                return mode;
            }
            return defaultMode;
        }

        public void SetMode(string key, string mode)
        {
            modes[key] = mode;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            List<string> list = (Scribe.mode == LoadSaveMode.Saving) ? new List<string>(disabled) : null;
            Scribe_Collections.Look(ref list, "disabledPatches", LookMode.Value);

            Dictionary<string, string> savedModes = (Scribe.mode == LoadSaveMode.Saving) ? modes : null;
            Scribe_Collections.Look(ref savedModes, "patchModes", LookMode.Value, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                disabled = (list == null) ? new HashSet<string>() : new HashSet<string>(list);
                modes = savedModes ?? new Dictionary<string, string>();
            }
        }
    }
}
