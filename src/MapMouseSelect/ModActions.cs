using System;
using System.Collections.Generic;
using HarmonyLib;
using Rewired;
using Rewired.Data.Mapping;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Registers the mod's two keys as real Rewired actions.
    ///
    /// Being Rewired actions is what makes them behave like the game's own controls: bindable in the
    /// in-game Controls menu, usable from a gamepad or a stick button, and read through the same
    /// GetButtonTimedPressUp / GetButtonTimedPressDown calls vanilla uses for Cancel.
    ///
    /// Two things have to happen while Rewired is still loading its data, which is what
    /// InputManager_Base.Awake is for:
    ///
    /// - the actions are appended to the action list and registered in the category map
    ///
    /// Their default keys are not written here. An ActionElementMap cannot be built before Rewired has a
    /// keyboard to build it against, which is what the first attempt at this got wrong: it threw inside
    /// Rewired and took the whole registration with it. EnsureDefaultBindings does it once Rewired is
    /// running instead, through the same call the game's own Controls menu binds with.
    ///
    /// They join an existing vanilla action's category rather than getting a category of their own. The
    /// Controls menu lists actions per category from mapping sets baked into its prefab, so joining a
    /// category that is already listed makes the menu show them with no prefab surgery, and their
    /// bindings land in a map category that is already enabled in a mission.
    /// </summary>
    internal static class ModActions
    {
        internal const string SelectOnlyName = "MapMouseSelect::SelectOnly";

        internal const string DeselectOldestName = "MapMouseSelect::DeselectOldest";

        /// <summary>
        /// Cancel is the action ExtraUiInput already uses to drop targets, so the two keys here share
        /// its category and its keyboard map.
        /// </summary>
        private const string AnchorActionName = "Cancel";

        // Far above the game's own action ids, which grow upward from zero.
        private const int SelectOnlyId = 971001;

        private const int DeselectOldestId = 971002;

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

            Plugin.Log.LogInfo(
                "Registered " + SelectOnlyName + " and " + DeselectOldestName + " in the Rewired '"
                + CategoryName(userData, anchor.categoryId) + "' category, matching the '" + AnchorActionName
                + "' action. Both are rebindable in the Controls menu.");
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

        private static bool _bindingsApplied;

        /// <summary>
        /// Gives each of the mod's actions its default key, once, on the first frame Rewired can answer.
        ///
        /// An action that already has a binding on any controller is left alone, so a player who rebound
        /// either key keeps their layout. The binding goes onto every enabled keyboard map that already
        /// binds the anchor action, which is the same reach the editor data would have had, with
        /// ControllerMap.ReplaceOrCreateElementMap doing the work, the call the Controls menu itself uses.
        ///
        /// A player who deliberately clears one of these keys gets it back on the next launch. That is the
        /// price of the mod's own keys working out of the box.
        /// </summary>
        internal static void EnsureDefaultBindings()
        {
            if (_bindingsApplied || !Registered)
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

            BindIfUnbound(player, keyboard, anchorActionId, SelectOnlyId, KeyCode.Backslash, SelectOnlyName);
            BindIfUnbound(player, keyboard, anchorActionId, DeselectOldestId, KeyCode.Delete, DeselectOldestName);

            _bindingsApplied = true;
        }

        private static void BindIfUnbound(
            Player player,
            Keyboard keyboard,
            int anchorActionId,
            int actionId,
            KeyCode key,
            string name)
        {
            if (player.controllers.maps.GetFirstElementMapWithAction(actionId, false) != null)
            {
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
                ModifierKeyFlags.None,
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
                    "No keyboard map took a default " + key + " binding for " + name
                    + ", so it has to be bound in the Controls menu. The game may have changed.");
                return;
            }

            Plugin.Verbose("Bound " + name + " to " + key + " on " + added + " keyboard map(s).");
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
