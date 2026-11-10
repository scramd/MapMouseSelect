using System;
using System.Collections.Generic;
using HarmonyLib;
using Rewired;
using Rewired.Data.Mapping;
using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Registers the mod's keys as real Rewired actions, so they are bindable in the Controls menu and usable
    /// from a gamepad. They join Cancel's category, which the Controls menu already lists, and are read with the
    /// same GetButtonTimedPressUp / GetButtonTimedPressDown calls vanilla uses for Cancel.
    ///
    /// The actions are appended during InputManager_Base.Awake, while Rewired is still loading its data. Default
    /// keys are not written there: an ActionElementMap cannot be built before Rewired has a keyboard, and trying
    /// threw inside Rewired and lost the whole registration. EnsureDefaultBindings writes them once Rewired runs.
    /// </summary>
    internal static class ModActions
    {
        internal const string SelectOnlyName = "MapMouseSelect::SelectOnly";

        internal const string DeselectOldestName = "MapMouseSelect::DeselectOldest";

        internal const string DeselectLookedAtName = "MapMouseSelect::DeselectLookedAt";

        /// <summary>Cancel's category and keyboard map, which the mod's keys share.</summary>
        private const string AnchorActionName = "Cancel";

        /// <summary>The actions this version gives a default key to, and so the ones a record drop has to cover.</summary>
        private static readonly string[] DefaultNames =
        {
            SelectOnlyName, DeselectOldestName, DeselectLookedAtName
        };

        // Far above the game's own action ids, which grow upward from zero.
        private const int SelectOnlyId = 971001;

        private const int DeselectOldestId = 971002;

        private const int DeselectLookedAtId = 971003;

        /// <summary>False until all three actions exist, so nothing polls a name Rewired cannot resolve.</summary>
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

            Plugin.Log.LogInfo("Registered three map selection actions in the Rewired Controls menu.");
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

            // Rewired keeps these setters internal, so the backing fields are written directly.
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

        /// <summary>Gives each of the mod's actions its default key once, on the first frame Rewired can answer.
        /// An action that already has a binding on any controller is left alone, so a rebound key stays rebound.
        /// The write goes through ControllerMap.ReplaceOrCreateElementMap, as the Controls menu itself does, onto
        /// every enabled keyboard map that already binds Cancel. It happens once per action, ever, recorded by name
        /// in the config, so a key cleared in the menu stays cleared and an action added later still gets its
        /// default.</summary>
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

            // The player's maps are not assigned yet on the first frames, so this stays unlatched until there is
            // something to bind into.
            if (KeyboardMaps.Count == 0)
            {
                return;
            }

            LoadWritten();

            BindIfUnbound(player, keyboard, anchorActionId, SelectOnlyId, KeyCode.Backslash, SelectOnlyName);
            BindIfUnbound(player, keyboard, anchorActionId, DeselectOldestId, KeyCode.Delete, DeselectOldestName);
            BindIfUnbound(
                player, keyboard, anchorActionId, DeselectLookedAtId, KeyCode.Minus, DeselectLookedAtName);

            // Every action this version knows about is now dealt with.
            _bindingsSettled = Written.Contains(SelectOnlyName)
                && Written.Contains(DeselectOldestName)
                && Written.Contains(DeselectLookedAtName);
        }

        /// <summary>
        /// Drops the mod's actions from the record so the defaults are written again, for the game's Restore Defaults
        /// button.
        ///
        /// That button calls LoadDefaultMaps, which clears the player's Controller Maps and reloads the ones authored
        /// in Rewired. The mod adds its actions to Rewired at runtime, so those maps hold no elements for them and the
        /// record would otherwise keep the keys gone for good. Anything the player rebound before the button was
        /// pressed is cleared by it too, so the defaults are the right thing to write back.
        ///
        /// The write itself is left to the next frame's EnsureDefaultBindings, so this stays a flag flip.
        /// </summary>
        internal static void ForgetDefaults()
        {
            // The Controls menu can be reached before a mission has run the bind pass, so the record is read here
            // rather than left until then. It is only ever read once.
            LoadWritten();

            var dropped = false;

            for (var i = 0; i < DefaultNames.Length; i++)
            {
                if (Written.Remove(DefaultNames[i]))
                {
                    dropped = true;
                }
            }

            if (!dropped)
            {
                return;
            }

            _bindingsSettled = false;
            SaveWritten();
            Plugin.Verbose("Restore Defaults was used, so the mod's default keys are written again.");
        }

        /// <summary>
        /// Reads the record once, on the first frame there is something to bind into. Reading it before Rewired is
        /// ready would latch an empty record and rewrite every default.
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

        private static void Record(string name)
        {
            Written.Add(name);
            SaveWritten();
        }

        private static void SaveWritten()
        {
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
            string name)
        {
            // Once per action, ever: a cleared key is not put back, an action added later is not in the record yet.
            if (Written.Contains(name))
            {
                return;
            }

            if (player.controllers.maps.GetFirstElementMapWithAction(actionId, false) != null)
            {
                // The player bound this one, so there is no default to write.
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
            Record(name);
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

        private static T Field<T>(object instance, string name) where T : class
        {
            var field = AccessTools.Field(instance.GetType(), name);
            return field != null ? field.GetValue(instance) as T : null;
        }
    }
}
