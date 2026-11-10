# Critic questions and decisions

Each round used a new read-only Claude Fable 5.2 delegate through provider claudeAgent, model claude-fable-5-2, mode wait. The helper could inspect the fork, the 0.34.6 source, the four companion mods and the bindings README. It could not edit, commit, publish, launch the game or start agents. The briefs were standalone.

## Feature scope, before deletion

Asked: challenge the selection/needed/removed/borderline classification and identify dependencies that would break selection.

Returned: retain the new-click filter and marker guards and empty-list deselection repair. Treat NTL as borderline because stock faction filters cannot exclude neutrals. Split required pan-button Jump Map suppression from the borderline double-click replacement. Remove obsolete colour rerouting and watch the SafeExit settled-defaults condition.

Decision: used. NTL and the selection guards stay. CameraJump stays whole with its two responsibilities documented. The colour fix had already been applied independently. The critic's removal/dependency warnings shaped the separate commits. Wreck support went before radar safety patches. Action IDs 971001 to 971003 remain unchanged.

## Coupling proposal, before final write-up

Asked: challenge the patch/reflection inventory, four-mod conflicts, looser alternatives and rewrite recommendation. Specifically ask how the NTL clone actually processes input.

Returned: the blanket skip also stops native map follow/clamp/placement and starts on pending clicks. A visual overlay cannot own input. Narrow input fixes are sufficient before a rewrite. Public RemoveAll and a different button template/reset order can avoid private UI/event access. NTL also filters HUD selection. Enabled-flag saves and selected-icon native Select effects need explicit limits. Found unused category diagnostic reflection.

Decision: mostly used. Recommend loosening input in place, with an EventSystem drag component as an optional later rewrite. Include full skip effects, NTL pointer/reset/right-click behaviour, public-API alternatives and conditional Minimap reservation risk. Remove dead CategoryName/actionCategories reflection and obsolete colour diagnostics. Independent source reads support those findings. Some stale-text findings were already being corrected concurrently.

The suggested pan/input/NTL/event changes are recommendations only. They are not implemented here because the task reserves interaction design and coupling rewrites for later. In particular, one non-raycasting overlay is already present; adding another would not fix input ownership. A public RemoveAll repair would trade event-wide notification for a pane-specific repair, so that cost remains explicit.

One critic claim was rejected after a metadata check: InputAction's parameterless constructor is public in the supplied Rewired assembly. Activator.CreateInstance permits non-public access, but that does not make this constructor non-public. The reflection inventory distinguishes reflective construction from the genuinely private field writes.

## Finished README, docs and source review

Asked: find wrong, missing or unclear player claims, gaps in every patch/reflection/input inventory, removal damage, scope omissions and weak alternatives. The helper inspected the finished drafts against current game/source and all four companion mods.

Returned: no removal damage or surviving removed-feature patches; Harmony and reflection inventories were complete. Correct commander Select Target Only and document its missing-marker path, editor side effects, shortcut exactness, target keys in chat, stopped-map gesture state and mixed commander orders. Add direct API contracts, a dated modification notice, and concrete Minimap/lifecycle caveats.

Decision: mostly used. Corrected the player text and stale source comments; added the limits, direct-API table, dated scramd-fork notice, metadata game-value explanation and native HUD Select arbitration. Recorded removal of upstream's subjective fair-play discussion. Kept the missing-marker target-key behaviour as an explicit inherited limit instead of broadening the implementation changes into target-action fixes. Added the loader-lifecycle question rather than asserting a BepInEx setting causes it; the helper did not verify that explanation.

The checker remains fail-fast deliberately: it reports the first precise mismatch, then can be rerun after review. It now prints leftGate in a mismatch and its README distinguishes explicit reference-folder arguments. No interaction redesign, NTL UI rewrite or reservation fix was implemented. Ignored work scripts are not package inputs; no release package was prepared.
