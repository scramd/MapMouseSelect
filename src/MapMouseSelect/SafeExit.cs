using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Ctrl+E leaves an aircraft the way the game leaves a parked one, and does nothing anywhere else.
    /// PilotPlayerState.PlayerControls calls aircraft.StartEjectionSequence() and then, when aircraft.IsLanded() is
    /// true, switches the pilot to its parked state. IsLanded is `radarAlt &lt; 5 && speed &lt; 2.5`, and the server's
    /// EjectionSequence asks the same question, so a landing at a base and one in a field are the same case to the
    /// game. This key requires the base as well: the aircraft must be inside one of its own faction's airbases,
    /// through FactionHQ.AnyNearAirbase, the game's own test for it.
    /// </summary>
    internal static class SafeExit
    {
        /// <summary>Reads the key once a frame from the PilotPlayerState.PlayerControls postfix, the method the game
        /// reads its own eject key in, and inherits its gates: flight controls enabled and a pilot who is not blacked
        /// out. Nothing is polled while no one is in a cockpit.</summary>
        internal static void OnPlayerControls()
        {
            if (!ModActions.Registered)
            {
                return;
            }

            var player = GameManager.playerInput;
            if (player == null || !player.GetButtonDown(ModActions.SafeExitName))
            {
                return;
            }

            Exit();
        }

        private static void Exit()
        {
            var aircraft = SelectionApplier.GetLiveAircraft();
            if (aircraft == null || aircraft.HasEjected())
            {
                return;
            }

            if (!aircraft.IsLanded())
            {
                Plugin.Verbose("Safe exit ignored: " + aircraft.name + " is not on the ground.");
                return;
            }

            var hq = aircraft.NetworkHQ;
            Airbase airbase;
            if (hq == null || !hq.AnyNearAirbase(aircraft.transform.position, out airbase))
            {
                Plugin.Verbose("Safe exit ignored: " + aircraft.name + " is not at one of your own airbases.");
                return;
            }

            var pilot = PlayerPilot(aircraft);
            if (pilot == null)
            {
                Plugin.Verbose("Safe exit ignored: " + aircraft.name + " has no pilot of yours to stand down.");
                return;
            }

            aircraft.StartEjectionSequence();
            pilot.SwitchState(pilot.parkedState);
            Plugin.Verbose("Safe exit: " + aircraft.name + " parked at " + airbase.name + ".");
        }

        /// <summary>The pilot this client is flying, or null if there is none. Pilot.playerState is entered only for
        /// the pilot whose Player is the local one. The state is compared by value and not by type, since every Pilot
        /// carries a playerState of its own and an AI seat's would answer a type test too.</summary>
        private static Pilot PlayerPilot(Aircraft aircraft)
        {
            var pilots = aircraft.pilots;
            if (pilots == null)
            {
                return null;
            }

            for (var i = 0; i < pilots.Length; i++)
            {
                var pilot = pilots[i];
                if (pilot != null && pilot.playerState != null && pilot.currentState == pilot.playerState)
                {
                    return pilot;
                }
            }

            return null;
        }
    }
}
