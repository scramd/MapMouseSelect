using System;
using System.Collections.Generic;
using HarmonyLib;
using Rewired;
using Rewired.Data.Mapping;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Registers the mod's keys as real Rewired actions.
    ///
    /// Being Rewired actions makes them behave like the game's own controls. They are bindable in the in-game
    /// Controls menu, and usable from a gamepad or a stick button. Vanilla reads them with the same
    /// GetButtonTimedPressUp / GetButtonTimedPressDown calls it uses for Cancel.
    ///
    /// Two things have to happen while Rewired is still loading its data, which is what InputManager_Base.Awake
    /// is for:
    ///
    /// - the actions are appended to the action list and registered in the category map
    ///
    /// Their default keys are not written here. An ActionElementMap cannot be built before Rewired has a
    /// keyboard to build it against. The first attempt at this got that wrong: it threw inside Rewired and took
    /// the whole registration with it. EnsureDefaultBindings does it once Rewired is running instead, through
    /// the same call the game's own Controls menu binds with.
    ///
    /// They join an existing vanilla action's category, rather than getting a category of their own. The
    /// Controls menu lists actions per category from mapping sets baked into its prefab. Joining a category that
    /// is already listed therefore makes the menu show them with no prefab surgery. Their bindings also land in a
    /// map category that is already enabled in a mission.
    /// </summary>
    internal static class ModActions
    {
        internal const string SelectOnlyName = "MapMouseSelect::SelectOnly";

        internal const string DeselectOldestName = "MapMouseSelect::DeselectOldest";

        internal const string DeselectLookedAtName = "MapMouseSelect::DeselectLookedAt";

        /// <summary>
        /// The safe exit key. It runs the game's landed eject and nothing else. The game's own eject is
        /// Control+Shift+E, so Control+E is free, and it reads as the gentler half of the same idea.
        /// </summary>
        internal const string SafeExitName = "MapMouseSelect::SafeExit";

        /// <summary>
        /// Cancel is the action ExtraUiInput already uses to drop targets, so the two keys here share
        /// its category and its keyboard map.
        /// </summary>
        private const string AnchorActionName = "Cancel";

        // Far above the game's own action ids, which grow upward from zero.
        private const int SelectOnlyId = 971001;

        private const int DeselectOldestId = 971002;

        private const int DeselectLookedAtId = 971003;

        private const int SafeExitId = 971004;

        /// <summary>
        /// False until both actions exist, so nothing polls a name Rewired cannot resolve.
        /// </summary>
        internal static bool Registered { get; private set; }

        internal static void Register(InputManager_Base manager)
        {
            try
            {
                Registered = RegisterCore(manager);
            }
            catch (Exception exception)
            {
                Registered = false;
                Plugin.Log.LogError("Could not register the mod's input actions: " + exception);
            }
        }

        private static bool RegisterCore(InputManager_Base manager)
        {
            var userData = manager != null ? manager.userData : null;
            if (userData == null)
            {
                Plugin.Log.LogWarning("Rewired had no user data when the mod tried to register its actions.");
                return false;
            }

            var actions = Field<List<InputAction>>(userData, "actions");
            var categoryMap = Field<ActionCategoryMap>(userData, "actionCategoryMap");
            var anchor = actions != null ? FindAction(actions, AnchorActionName) : null;

            if (actions == null || categoryMap == null || anchor == null)
            {
                Plugin.Log.LogWarning(
                    "Could not resolve Rewired's action list, category map or the '" + AnchorActionName
                    + "' action, so the mod's keys are not registered. The game may have changed.");
                return false;
            }

            EnsureAction(actions, categoryMap, anchor.categoryId, SelectOnlyId, SelectOnlyName, "Select Target Only");
            EnsureAction(actions, categoryMap, anchor.categoryId, DeselectOldestId, DeselectOldestName, "Deselect Oldest Target");
            EnsureAction(
                actions, categoryMap, anchor.categoryId, DeselectLookedAtId, DeselectLookedAtName,
                "Deselect Target Looked At");
            EnsureAction(actions, categoryMap, anchor.categoryId, SafeExitId, SafeExitName, "Safe Exit Aircraft");

            Plugin.Log.LogInfo(
                "Registered " + SelectOnlyName + ", " + DeselectOldestName + ", " + DeselectLookedAtName + " and "
                + SafeExitName + " in the Rewired '" + CategoryName(userData, anchor.categoryId) + "' category, matching the '"
                + AnchorActionName + "' action. All four are rebindable in the Controls menu.");
            return true;
        }

        private static void EnsureAction(
            List<InputAction> actions,
            ActionCategoryMap categoryMap,
            int categoryId,
            int id,
            string name,
            string descriptiveName)
        {
            if (FindAction(actions, name) != null)
            {
                // Already there, which is the case for a second InputManager in the same session.
                return;
            }

            // Rewired keeps these setters internal, so the backing fields are written directly. That is
            // the same route the other mods on this machine take to add actions.
            var action = (InputAction)Activator.CreateInstance(typeof(InputAction), true);
            SetField(action, "_id", id);
            SetField(action, "_name", name);
            SetField(action, "_type", InputActionType.Button);
            SetField(action, "_descriptiveName", descriptiveName);
            SetField(action, "_categoryId", categoryId);
            SetField(action, "_userAssignable", true);
            actions.Add(action);
            categoryMap.AddAction(categoryId, id);
        }

        private static readonly List<ControllerMap> KeyboardMaps = new List<ControllerMap>();

        /// <summary>Actions whose default key has already been dealt with. Read from the config once per session.</summary>
        private static readonly HashSet<string> Written = new HashSet<string>(StringComparer.Ordinal);

        private static bool _writtenLoaded;

        private static bool _bindingsSettled;

        /// <summary>
        /// Gives each of the mod's actions its default key, once, on the first frame Rewired can answer.
        ///
        /// An action that already has a binding on any controller is left alone, so a player who rebound either
        /// key keeps their layout. The binding goes onto every enabled keyboard map that already binds the anchor
        /// action, the same reach the editor data would have had.
        /// ControllerMap.ReplaceOrCreateElementMap does the work, the call the Controls menu itself uses.
        ///
        /// It happens once per action and never again, recorded by name in the config. A key the player clears in
        /// the Controls menu therefore stays cleared. An action a later version of the mod adds still gets its
        /// default the first time that version runs. Latching once per install would have missed that. A player who
        /// wants a written key back either binds it again, or removes its name from that record.
        /// </summary>
        internal static void EnsureDefaultBindings()
        {
            if (_bindingsSettled || !Registered)
            {
                return;
            }

            var player = GameManager.playerInput;
            var keyboard = ReInput.controllers != null ? ReInput.controllers.Keyboard : null;
            if (player == null || keyboard == null)
            {
                return;
            }

            var anchorActionId = ReInput.mapping.GetActionId(AnchorActionName);
            if (anchorActionId < 0)
            {
                return;
            }

            KeyboardMaps.Clear();
            player.controllers.maps.GetAllMaps(ControllerType.Keyboard, KeyboardMaps);

            // The player's maps are not assigned yet on the first frames, so this stays unlatched until
            // there is something to bind into.
            if (KeyboardMaps.Count == 0)
            {
                return;
            }

            LoadWritten();

            BindIfUnbound(player, keyboard, anchorActionId, SelectOnlyId, KeyCode.Backslash, SelectOnlyName);
            BindIfUnbound(player, keyboard, anchorActionId, DeselectOldestId, KeyCode.Delete, DeselectOldestName);
            BindIfUnbound(
                player, keyboard, anchorActionId, DeselectLookedAtId, KeyCode.Minus, DeselectLookedAtName);
            BindIfUnbound(
                player, keyboard, anchorActionId, SafeExitId, KeyCode.E, SafeExitName,
                ModifierKeyFlags.LeftControl | ModifierKeyFlags.RightControl);

            // Every action this version knows about has been dealt with, so there is nothing left to look at. A later
            // version that adds one re-enters here for that one action, which is why the record is per name.
            _bindingsSettled = Written.Contains(SelectOnlyName)
                && Written.Contains(DeselectOldestName)
                && Written.Contains(DeselectLookedAtName)
                && Written.Contains(SafeExitName);
        }

        /// <summary>
        /// Reads the record of actions whose default has been dealt with, once, on the first frame there is
        /// something to bind into. Reading it before Rewired is ready would latch an empty record and rewrite
        /// every default.
        /// </summary>
        private static void LoadWritten()
        {
            if (_writtenLoaded)
            {
                return;
            }

            _writtenLoaded = true;

            var record = Plugin.DefaultBindings.Value;
            if (string.IsNullOrEmpty(record))
            {
                return;
            }

            var names = record.Split(',');
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i].Trim();
                if (name.Length > 0)
                {
                    Written.Add(name);
                }
            }
        }

        /// <summary>
        /// Notes that an action's default has been dealt with and writes the record out. The next launch then
        /// leaves it alone, whether the player keeps the key or clears it.
        /// </summary>
        private static void Record(string name)
        {
            Written.Add(name);

            var names = new List<string>(Written);
            names.Sort(StringComparer.Ordinal);
            Plugin.DefaultBindings.Value = string.Join(", ", names);
            Plugin.Settings.Save();
        }

        private static void BindIfUnbound(
            Player player,
            Keyboard keyboard,
            int anchorActionId,
            int actionId,
            KeyCode key,
            string name,
            ModifierKeyFlags modifiers = ModifierKeyFlags.None)
        {
            // Once per action, ever. A key the player has cleared is not put back, and an action a later version adds
            // still gets its default, since it is not in the record yet.
            if (Written.Contains(name))
            {
                return;
            }

            if (player.controllers.maps.GetFirstElementMapWithAction(actionId, false) != null)
            {
                // The player bound this one, so there is no default to write, now or later.
                Record(name);
                return;
            }

            var identifier = keyboard.GetElementIdentifierByKeyCode(key);
            if (identifier == null)
            {
                Plugin.Log.LogWarning(
                    "Rewired could not resolve the " + key + " key, so " + name + " has no default binding.");
                return;
            }

            var assignment = ElementAssignment.CompleteAssignment(
                ControllerType.Keyboard,
                ControllerElementType.Button,
                identifier.id,
                AxisRange.Full,
                key,
                modifiers,
                actionId,
                Pole.Positive,
                false);

            var added = 0;
            for (var i = 0; i < KeyboardMaps.Count; i++)
            {
                var map = KeyboardMaps[i];
                if (map == null || !map.enabled || !BindsAction(map, anchorActionId))
                {
                    continue;
                }

                if (map.ReplaceOrCreateElementMap(assignment))
                {
                    added++;
                }
            }

            if (added == 0)
            {
                Plugin.Log.LogWarning(
                    "No keyboard map took a default " + Describe(key, modifiers) + " binding for " + name
                    + ", so it has to be bound in the Controls menu. The game may have changed.");
                return;
            }

            Plugin.Verbose("Bound " + name + " to " + Describe(key, modifiers) + " on " + added + " keyboard map(s).");
            Record(name);
        }

        /// <summary>The key as the Controls menu would write it, for the log lines.</summary>
        private static string Describe(KeyCode key, ModifierKeyFlags modifiers)
        {
            var text = string.Empty;

            if ((modifiers & (ModifierKeyFlags.LeftShift | ModifierKeyFlags.RightShift)) != ModifierKeyFlags.None)
            {
                text += "Shift+";
            }

            if ((modifiers & (ModifierKeyFlags.LeftControl | ModifierKeyFlags.RightControl)) != ModifierKeyFlags.None)
            {
                text += "Ctrl+";
            }

            if ((modifiers & (ModifierKeyFlags.LeftAlt | ModifierKeyFlags.RightAlt)) != ModifierKeyFlags.None)
            {
                text += "Alt+";
            }

            return text + key;
        }

        private static bool BindsAction(ControllerMap map, int actionId)
        {
            var elements = map.AllMaps;
            for (var i = 0; i < elements.Count; i++)
            {
                if (elements[i] != null && elements[i].actionId == actionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SetField(object instance, string name, object value)
        {
            var field = AccessTools.Field(instance.GetType(), name);
            if (field == null)
            {
                Plugin.Log.LogWarning(
                    instance.GetType().Name + "." + name + " was not found, so the mod's key may not "
                    + "work as bound. The game may have changed.");
                return;
            }

            field.SetValue(instance, value);
        }

        private static InputAction FindAction(List<InputAction> actions, string name)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                if (actions[i] != null && string.Equals(actions[i].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return actions[i];
                }
            }

            return null;
        }

        private static string CategoryName(object userData, int categoryId)
        {
            var categories = Field<List<InputActionCategory>>(userData, "actionCategories");
            if (categories != null)
            {
                for (var i = 0; i < categories.Count; i++)
                {
                    if (categories[i] != null && categories[i].id == categoryId)
                    {
                        return categories[i].name;
                    }
                }
            }

            return categoryId.ToString();
        }

        private static T Field<T>(object instance, string name) where T : class
        {
            var field = AccessTools.Field(instance.GetType(), name);
            return field != null ? field.GetValue(instance) as T : null;
        }
    }
}
