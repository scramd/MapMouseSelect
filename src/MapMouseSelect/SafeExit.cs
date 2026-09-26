using UnityEngine;

namespace MapMouseSelect
{
    /// <summary>
    /// Ctrl+E: leave an aircraft the way the game leaves it when it is parked at a base, and nothing at all anywhere
    /// else.
    ///
    /// The game has one eject key, and what it does depends on where the aircraft is. PilotPlayerState.PlayerControls
    /// calls aircraft.StartEjectionSequence() and then, when aircraft.IsLanded() is true, switches the pilot to its
    /// parked state. IsLanded is `radarAlt &lt; 5 && speed &lt; 2.5`. The server's EjectionSequence asks the same
    /// question to decide between firing the seats and letting the crew out where they stand. To the game, a landing
    /// at a base and a landing in a field are therefore one case.
    ///
    /// This key takes that case and requires the base as well. It acts only when the aircraft is on the ground and
    /// inside one of its own faction's airbases, which is the game's own test for "landed at a base". That is the
    /// same pair of conditions its landed/crashed reporting uses, through FactionHQ.AnyNearAirbase. Everywhere else
    /// it is silent, which leaves the game's own key as the only way out of an aircraft in the air.
    ///
    /// Both statements it runs are the game's own, in the game's own order. Nothing here invents an exit.
    /// </summary>
    internal static class SafeExit
    {
        /// <summary>
        /// Reads the key. Called once a frame from the postfix on PilotPlayerState.PlayerControls, which is the method
        /// the game reads its own eject key in. This key therefore sits beside it, rather than somewhere else in the
        /// frame, and inherits the gates that method opens with: flight controls enabled, and a pilot who is not
        /// blacked out. It also means nothing is polled while no one is in a cockpit, since the state machine only
        /// ticks there.
        /// </summary>
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

        /// <summary>
        /// The pilot this client is flying, or null if there is none.
        ///
        /// Pilot.playerState is entered only for the pilot whose Player is the local one. That makes it the game's
        /// own marker for "this seat is being flown from here", rather than a guess at an index. The state is asked
        /// for by value and not by type, since every Pilot carries a playerState instance of its own and an AI
        /// seat's would answer a type test too.
        /// </summary>
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
