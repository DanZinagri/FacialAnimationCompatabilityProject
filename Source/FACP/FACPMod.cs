using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FACP
{
    // Nothing here runs during play. The constructor reads the settings file once at
    // startup so StartupPatcher can consult it; the rest of the class only executes while
    // the settings window is actually open.
    public class FACPMod : Mod
    {
        // All player-facing text is in Languages/English/Keyed/FACP_Settings.xml (window) and
        // FACP_Toggles.xml (the toggles themselves).
        public static string GeneChangeTag
        {
            get { return "FACP.UI.GeneTag".Translate(); }
        }

        public static string GeneChangeNote
        {
            get { return "FACP.UI.GeneChangeNote".Translate(); }
        }

        public static FACPSettings Settings;

        private const float RowHeight = 26f;
        private const float CheckboxRowHeight = 24f;
        private const float CategoryHeaderHeight = 34f;
        private const float CategoryGap = 10f;
        private const float ButtonWidth = 110f;
        private const float ButtonHeight = 28f;
        private const float CheckboxSize = 24f;
        private const float ModeRowHeight = 24f;
        // Enough room for each note to wrap to three lines in a narrow settings window.
        private const float ExistingPawnNoteHeight = 56f;
        private const float GeneNoteHeight = 52f;
        private const float CacheNoticeHeight = 44f;

        // Missile Girl bundles Gagarin, which caches the combined XML document and only
        // rebuilds it when the mod list or the number of XML files changes. A cached launch
        // never calls ApplyPatches at all, so the few patches that decide their own on/off
        // state in XML - see ToggleRegistry.gatedInXml - keep whatever they were set to when
        // that cache was written. Every other toggle runs from the startup pass and is
        // unaffected, which is the distinction the notice has to draw.
        private const string XmlCacheModId = "vr.missilegirl";

        private static readonly Color CacheNoticeColor = new Color(1f, 0.66f, 0.32f);

        private static int xmlCacheModActive = -1;
        private static string cacheNotice;

        private static readonly Color GeneTagColor = new Color(0.88f, 0.72f, 0.36f);

        private static float geneTagWidth = -1f;

        private Vector2 scrollPos;

        public FACPMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<FACPSettings>();
        }

        // Consulted by StartupPatcher. An unknown key means "never switched off", so new
        // patches ship enabled - except the opt-in preferences, which ship off.
        public static bool IsEnabled(string key)
        {
            return (Settings == null) ? !FACPSettings.IsDefaultOff(key) : Settings.IsEnabled(key);
        }

        // Consulted by StartupPatcher for the multiple-choice patches.
        public static string GetMode(string key, string defaultMode)
        {
            return (Settings == null) ? defaultMode : Settings.GetMode(key, defaultMode);
        }

        public override string SettingsCategory()
        {
            return "FACP.SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            List<ToggleEntry> entries = ToggleRegistry.Entries;

            if (entries.Count == 0)
            {
                Widgets.Label(inRect, "FACP.UI.NothingToConfigure".Translate());
                return;
            }

            float headerHeight = DrawHeader(inRect, entries);
            float footerHeight = ExistingPawnNoteHeight
                + (ToggleRegistry.AnyModifiesGenes ? GeneNoteHeight : 0f);

            Rect outRect = new Rect(inRect.x, inRect.y + headerHeight, inRect.width,
                inRect.height - headerHeight - footerHeight);
            Rect viewRect = new Rect(0f, 0f, outRect.width - 20f, ViewHeight(entries));

            Widgets.BeginScrollView(outRect, ref scrollPos, viewRect);

            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);

            string category = null;
            for (int i = 0; i < entries.Count; i++)
            {
                ToggleEntry entry = entries[i];

                if (entry.category != category)
                {
                    category = entry.category;
                    if (i > 0)
                    {
                        listing.Gap(CategoryGap);
                    }
                    GUI.color = new Color(0.7f, 0.85f, 1f);
                    listing.Label(entry.CategoryLabel);
                    GUI.color = Color.white;
                    listing.GapLine(2f);
                }

                if (entry.IsMode)
                {
                    DrawModeEntry(listing, entry);
                    continue;
                }

                bool enabled = Settings.IsEnabled(entry.key);
                bool before = enabled;

                // Fixed row height so the tag can be placed against known geometry, and so
                // ViewHeight below stays exact rather than an estimate.
                float rowY = listing.CurHeight;
                listing.CheckboxLabeled("    " + entry.Label, ref enabled, entry.Tooltip, CheckboxRowHeight);

                if (entry.modifiesGenes)
                {
                    float width = GeneTagWidth;
                    Rect tagRect = new Rect(listing.ColumnWidth - CheckboxSize - width - 6f, rowY + 2f,
                        width, CheckboxRowHeight - 4f);
                    DrawTag(tagRect, GeneChangeTag);
                }

                if (enabled != before)
                {
                    Settings.SetEnabled(entry.key, enabled);
                }
            }

            listing.End();
            Widgets.EndScrollView();

            DrawFooter(new Rect(inRect.x, inRect.yMax - footerHeight, inRect.width, footerHeight));
        }

        // Mutually exclusive options render as radio buttons under their own label, so it is
        // obvious only one applies - a row of checkboxes would imply they can be combined.
        private static void DrawModeEntry(Listing_Standard listing, ToggleEntry entry)
        {
            Rect labelRect = listing.GetRect(CheckboxRowHeight);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(labelRect.x + 12f, labelRect.y, labelRect.width - 12f, labelRect.height),
                entry.Label);
            Text.Anchor = TextAnchor.UpperLeft;

            string current = Settings.GetMode(entry.key, entry.DefaultMode);
            for (int i = 0; i < entry.modeKeys.Length; i++)
            {
                string tooltip = entry.ModeDescription(i) + "\n\n(" + entry.key + " = " + entry.modeKeys[i] + ")";
                bool active = current == entry.modeKeys[i];
                if (listing.RadioButton(entry.ModeLabel(i), active, 32f, tooltip, null) && !active)
                {
                    Settings.SetMode(entry.key, entry.modeKeys[i]);
                }
            }
        }

        private float DrawHeader(Rect inRect, List<ToggleEntry> entries)
        {
            float y = inRect.y;

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            Widgets.Label(new Rect(inRect.x, y, inRect.width, 46f),
                "FACP.UI.Intro".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 50f;

            if (Widgets.ButtonText(new Rect(inRect.x, y, ButtonWidth, ButtonHeight), "FACP.UI.EnableAll".Translate()))
            {
                SetAll(entries, true);
            }
            if (Widgets.ButtonText(new Rect(inRect.x + ButtonWidth + 8f, y, ButtonWidth, ButtonHeight), "FACP.UI.DisableAll".Translate()))
            {
                SetAll(entries, false);
            }

            // Deliberately no "restart now" button. Forcing a relaunch from here would go
            // around whatever else is managing startup (Prepatcher and friends rewrite it),
            // and the setting is already saved when this window closes.
            string warning = RestartPending(entries) ? (string)"FACP.UI.RestartToApply".Translate() : null;

            if (warning != null)
            {
                float warningX = inRect.x + (ButtonWidth + 8f) * 2f + 8f;

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(warningX, y, inRect.xMax - warningX, ButtonHeight), warning);
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }

            y += ButtonHeight + 6f;

            string notice = CacheNotice;
            if (notice != null)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = CacheNoticeColor;
                Widgets.Label(new Rect(inRect.x, y, inRect.width, CacheNoticeHeight - 4f), notice);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                y += CacheNoticeHeight;
            }

            y += 4f;
            return y - inRect.y;
        }

        private static void DrawFooter(Rect rect)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width);
            GUI.color = Color.white;

            // Facial Animation stores each pawn's chosen face on the pawn itself, and only
            // re-rolls it when a gene change marks it stale. So a toggle can never reach a
            // colonist who already exists - worth saying plainly, because otherwise a
            // correctly applied patch looks like it did nothing.
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 0.92f, 0.6f, 0.9f);
            Widgets.Label(new Rect(rect.x, rect.y + 6f, rect.width, ExistingPawnNoteHeight - 8f),
                "FACP.UI.ExistingPawnsNote".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            if (!ToggleRegistry.AnyModifiesGenes)
            {
                return;
            }

            float y = rect.y + ExistingPawnNoteHeight;
            float tagWidth = GeneTagWidth;
            Rect tagRect = new Rect(rect.x, y + 6f, tagWidth, CheckboxRowHeight - 4f);
            DrawTag(tagRect, GeneChangeTag);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            Widgets.Label(new Rect(tagRect.xMax + 8f, y + 4f, rect.width - tagWidth - 8f, GeneNoteHeight - 6f),
                "FACP.UI.GeneTagNote".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        // Null unless an XML-caching mod is running and we actually ship a flagged patch for
        // something installed. Built once, then reused for as long as the window is open.
        private static string CacheNotice
        {
            get
            {
                if (xmlCacheModActive < 0)
                {
                    xmlCacheModActive =
                        (ModLister.GetActiveModWithIdentifier(XmlCacheModId, true) != null) ? 1 : 0;
                }
                if (xmlCacheModActive == 0)
                {
                    return null;
                }

                if (cacheNotice == null)
                {
                    List<string> categories = ToggleRegistry.CacheSensitiveCategories;
                    if (categories.Count == 0)
                    {
                        xmlCacheModActive = 0;
                        return null;
                    }
                    List<string> names = new List<string>();
                    foreach (string category in categories)
                    {
                        names.Add(("FACP.Category." + category).Translate());
                    }
                    cacheNotice = "FACP.UI.CacheNotice".Translate(string.Join(", ", names.ToArray()));
                }
                return cacheNotice;
            }
        }

        private static float GeneTagWidth
        {
            get
            {
                if (geneTagWidth < 0f)
                {
                    GameFont font = Text.Font;
                    Text.Font = GameFont.Tiny;
                    geneTagWidth = Text.CalcSize(GeneChangeTag).x + 14f;
                    Text.Font = font;
                }
                return geneTagWidth;
            }
        }

        private static void DrawTag(Rect rect, string label)
        {
            Widgets.DrawBoxSolidWithOutline(rect,
                new Color(GeneTagColor.r, GeneTagColor.g, GeneTagColor.b, 0.12f),
                new Color(GeneTagColor.r, GeneTagColor.g, GeneTagColor.b, 0.5f));

            GameFont font = Text.Font;
            TextAnchor anchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = GeneTagColor;
            Widgets.Label(rect, label);
            GUI.color = Color.white;
            Text.Anchor = anchor;
            Text.Font = font;
        }

        private static void SetAll(List<ToggleEntry> entries, bool enabled)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                // A mode entry has no on/off state to set; leave the player's choice alone.
                // Nor does "Enable all" tick an opt-in preference - those are a matter of taste,
                // not a fix - though "Disable all" still clears them.
                if (!entries[i].IsMode && !(enabled && entries[i].defaultOff))
                {
                    Settings.SetEnabled(entries[i].key, enabled);
                }
            }
        }

        // True once any checkbox no longer matches what was actually patched in at load.
        private static bool RestartPending(List<ToggleEntry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                ToggleEntry entry = entries[i];
                if (entry.IsMode)
                {
                    if (entry.appliedModeThisSession != Settings.GetMode(entry.key, entry.DefaultMode))
                    {
                        return true;
                    }
                }
                else if (entry.appliedThisSession != Settings.IsEnabled(entry.key))
                {
                    return true;
                }
            }
            return false;
        }

        private static float ViewHeight(List<ToggleEntry> entries)
        {
            float height = 20f;
            string category = null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].category != category)
                {
                    category = entries[i].category;
                    height += CategoryHeaderHeight;
                    if (i > 0)
                    {
                        height += CategoryGap;
                    }
                }
                height += entries[i].IsMode
                    ? RowHeight + entries[i].modeKeys.Length * ModeRowHeight
                    : RowHeight;
            }
            return height;
        }
    }
}
