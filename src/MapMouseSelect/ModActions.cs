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
    /// - default bindings are written into the keyboard map that already binds the action they sit
    ///   beside, so they work before the player has opened any menu
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

            AddDefaultBinding(userData, anchor.id, SelectOnlyId, KeyCode.Backslash);
            AddDefaultBinding(userData, anchor.id, DeselectOldestId, KeyCode.Delete);

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

        /// <summary>
        /// Writes a default binding into the same keyboard map entries that bind the anchor action, so
        /// the key works out of the box and the Controls menu offers "reset to defaults" for it.
        /// </summary>
        private static void AddDefaultBinding(object userData, int anchorActionId, int actionId, KeyCode key)
        {
            var maps = Field<List<ControllerMap_Editor>>(userData, "keyboardMaps");
            if (maps == null)
            {
                return;
            }

            var added = 0;
            for (var i = 0; i < maps.Count; i++)
            {
                var map = maps[i];
                if (map == null || map.actionElementMaps == null)
                {
                    continue;
                }

                if (!BindsAction(map.actionElementMaps, anchorActionId)
                    || BindsAction(map.actionElementMaps, actionId))
                {
                    continue;
                }

                map.actionElementMaps.Add(CreateKeyboardElement(actionId, key));
                added++;
            }

            if (added == 0)
            {
                Plugin.Log.LogWarning(
                    "No default " + key + " binding was written for " + actionId
                    + ", so it has to be bound in the Controls menu. The game may have changed.");
            }
        }

        private static bool BindsAction(List<ActionElementMap> elements, int actionId)
        {
            for (var i = 0; i < elements.Count; i++)
            {
                if (elements[i] != null && elements[i].actionId == actionId)
                {
                    return true;
                }
            }

            return false;
        }

        private static ActionElementMap CreateKeyboardElement(int actionId, KeyCode key)
        {
            var keyboardKey = (KeyboardKeyCode)(int)key;
            var element = new ActionElementMap(
                actionId,
                ControllerElementType.Button,
                Pole.Positive,
                keyboardKey,
                ModifierKey.None,
                ModifierKey.None,
                ModifierKey.None);

            // The identifier and axis fields are internal, and Rewired expects them populated.
            SetField(element, "_elementIdentifierId", (int)keyboardKey);
            SetField(element, "_axisRange", AxisRange.Full);
            SetField(element, "_invert", false);
            return element;
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
