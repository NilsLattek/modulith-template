# Triage Labels

The skills speak in terms of five canonical triage roles. This file maps those roles to the actual
strings used in this repo's issue tracker. Tickets are local markdown files, so a "label" is the
value of the `Status:` line near the top of an issue file, not a tracker label.

| Label in mattpocock/skills | `Status:` value      | Meaning                                  |
| -------------------------- | -------------------- | ---------------------------------------- |
| `needs-triage`             | `needs-triage`       | Maintainer needs to evaluate this issue  |
| `needs-info`               | `needs-info`         | Waiting on reporter for more information |
| `ready-for-agent`          | `ready-for-agent`    | Fully specified, ready for an AFK agent  |
| `ready-for-human`          | `ready-for-human`    | Requires human implementation            |
| `wontfix`                  | `wontfix`            | Will not be actioned                     |

When a skill mentions a role (e.g. "apply the AFK-ready triage label"), write the corresponding
value on the ticket's `Status:` line.

Edit the right-hand column to match whatever vocabulary you actually use.
