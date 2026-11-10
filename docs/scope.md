# Feature scope

Selection means mouse selection or deselection on the map, including stock bugs that prevent those operations. Needed means that a kept selection feature fails without it in this implementation. Borderline code stays for an owner decision. It is not an endorsement of the current interaction design.

| Feature | Class | Reason and decision |
| --- | --- | --- |
| Marquee replace, add, remove; empty replace clears | Selection | Main feature. Retain the current eligibility rules and outline settings. |
| Flying target batching, limits and sounds; commander map selection | Selection | These apply the gesture to the game's two selection stores. Retain. |
| Plain mouse click replaces; Shift-click removes | Selection | Consistent individual selection and deselection. Retain. |
| Click filter and HUD marker guards | Needed by selection | Stop a rejected new click clearing valid existing targets or commander selection. They remain useful without wreck icons. |
| Selected unit icon raycast restoration | Needed by selection | Stock SelectIcon disables pointer hits. Retain so selected icons accept Shift-click and hover. |
| Click suppression after displacement | Needed by selection | Prevents an end-of-drag click changing the result when release remains beyond the threshold. It does not record maximum travel. |
| Empty weapon-list deselection repair | Needed by selection | When the HUD still references an aircraft, especially a destroyed one, Target List exclusion routes map-only selections to CombatHUD.DeSelectUnit, which returns on an empty weapon list. |
| Clear-all event repair | Needed by selection | DeselectAllIcons clears icons without the event used to clear Target List rows. |
| Pan button moved from left | Needed by selection | Marquee owns left press through drag. Stock left pan cannot operate during it. |
| Suppress Jump Map on the pan button | Needed by selection | Prevents middle pan also jumping the commander camera with the stock binding. |
| Bare modifier binding suppression | Needed by selection | Shift/Ctrl gestures would also actuate native bindings, such as throttle. Needed for that isolation with the present controls; not inherently required by box picking. |
| NTL neutral filter, reset and theme integration | Borderline | Directly controls which units a box selects. Stock faction buttons cannot exclude neutrals. Selection can still work without it. Recommend keep the capability, loosen UI coupling. |
| Double-click commander camera jump | Borderline | Preserves an action displaced by middle pan, but camera navigation is not selection. Recommend decide together with pan controls. |
| Proportional mouse pan and speed | Borderline | Useful after pan relocation; the box works without corrected pan movement. Recommend keep only if navigation remains in scope. |
| Mouse stick panning and gain | Borderline | Optional navigation style, not required for selection. Recommend remove in a later owner-approved scope change. |
| Move cursor, including SystemCursor Windows helper | Borderline | Feedback for relocated pan, but not required to select. Recommend replace with a portable cursor if kept. |
| Editor opt-in | Selection boundary | Gates marquee use in the editor. It is not integration with editor multi-selection. Keep default off. |
| Overlay colours/order, visibility, verbose logs and cockpit transition diagnostics | Selection support | Display and diagnose selection. Keep; diagnostic hooks only observe. |
| Unfocused frame-rate limit | Not selection, removed | No selection dependency. Its host-detection hook and four settings are removed. |
| Safe aircraft exit | Not selection, removed | No selection dependency. Remove its key, registration and pilot-controls patch. |
| Destroyed-building persistence/tint, DED filter, icon creation/repair/removal and associated dead-target cleanup | Not selection, removed | Extended map content, not a requirement for selecting stock icons. Keep no private icon-creation call or destroyed settings. |
| Camera-wheel masking | Not selection, removed | Fixes camera zoom behind UI, independent of selection. |
| Missing-icon missile/radar and missing-marker highlight guards | Not selection, removed | Selection does not call those paths. Removed after wreck support. |
| Target keys, Controls menu registration and Restore Defaults support | Not selection, removed | Keyboard actions on the target list that also worked with the map closed. Removed by owner decision on 2026-11-10. |
| FollowFirstUnit | Not selection, removed | Optional camera reaction to a box. Removed by owner decision on 2026-11-10. |
| Colour rerouting and depth finalizer | Obsolete, removed | 0.34.6 uses virtual UpdateColor and performs unit filter dimming itself. No replacement patch is needed. |

The retained code derives from Kerbiter's upstream commit recorded in the README. Removals are deliberate scope cuts, not evidence that the upstream features were poor. Plugin identity, version, metadata and licence remain unchanged for the owner.

## Borderline arguments for the owner

The uncertainty here is the boundary between a selection tool and the supporting map interactions it displaces. None of these is a strict dependency of rectangle picking. Keeping them now preserves the owner's choice for the planned interaction work.

| Item | Argument to keep | Argument to remove | Recommendation |
| --- | --- | --- | --- |
| NTL | Controls which units enter a box; stock faction filters cannot express neutral exclusion. | It extends the global filter system, including HUD targeting, beyond box selection. | Keep capability; loosen UI access. |
| Double-click camera jump | Preserves the commander action displaced by needed pan relocation; stock commander clicks also move/follow the camera. | Navigation does not select anything and double click can also select. | Decide with pan bindings. |
| Proportional pan/speed | Makes the relocated pan usable as a cursor-following drag alongside box selection. | The stock pan can still navigate without it; it is an independent improvement. | Keep only if navigation is included. |
| Mouse stick mode/gain | An existing alternative way to navigate the map after left drag is reserved for selection. | Optional continuous scrolling adds a separate navigation style and no selection operation depends on it. | Prefer removal in the owner's next scope choice. |
| Move cursor | Feedback for the relocated pan helps distinguish navigation from box selection. | Cosmetic feedback is not required; the Windows implementation adds platform coupling. | Replace portably if retained. |

The upstream README's subjective client-mod fair-play discussion was removed from the new player text. This fork makes no blanket claim about server acceptance or competitive fairness. The owner can choose distribution wording separately.
