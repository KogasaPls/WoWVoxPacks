# Contributing

## Commits and release notes

Use [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/):

```text
type(scope): brief description
```

Use a scope when the change concerns a particular addon:

| Scope | Addon |
| --- | --- |
| `bigwigs_voice` | BigWigs Voice |
| `bigwigs_countdown` | BigWigs Countdown |
| `callouts` | Callouts |
| `nsrt` | Northern Sky Raid Tools |
| `exboss` | ExBoss |
| `packs` | Changes shared by several voice packs |

Write `feat`, `fix`, and `perf` descriptions for addon users: what they can hear,
configure, or use after installing the release. Name the mechanic and player-visible
outcome so each subject remains useful without surrounding commit history. Examples:

```text
feat(bigwigs_voice): add Kith'ix abilities
feat(nsrt): add missing Ula'tek callouts
fix(nsrt): use Left and Right recordings for wave directions
fix(packs): support WoW 12.1.5
```

Use `build`, `ci`, `docs`, `refactor`, `test`, `style`, or `chore` for project
internals. Internal scopes such as `build`, `ci`, `release`, and `deps` are useful
when applicable. A fix to the release workflow is `fix(ci): ...` or `ci: ...`,
not an addon fix. Put implementation details, validation, and investigative history
in the commit body or PR description. Split unrelated addon changes and internal
maintenance into separate commits. Use the same convention for PR titles.

CI checks new non-merge commits in the push or PR range. Existing history and
Git-generated merge subjects are exempt. Before committing, review the subject
against the addon scopes above; CI validates syntax, not whether a description is
useful to players.

Release notes include `feat`, `fix`, and `perf` descriptions when the commit changes
that addon's distributed files and those files changed since the preceding release.
The `packs` scope applies to several addons; an unscoped description is labelled
with the addon its files affect. `!` marks a breaking change in the notes.
Build-only metadata and internal scopes are excluded. Legacy unstructured subjects
are not copied into new release notes.

Commit a user-facing source change together with its generated recordings or Lua.
If rendering is committed separately, give that commit a player-facing description
of the resulting addon change. Source-only work has not shipped yet.
Callout additions and removals are checked against distributed registrations, so
pending vocabulary and names retained as aliases do not appear as new or removed
sounds. The full Git comparison remains available from the changelog link for
maintainers.

The automatic updater names rendered callout additions and removals, the BigWigs
Voice version being synced, and game compatibility changes in its commit and PR
title. Updates to build inputs and rebuilds without a described addon change use
`chore` and stay out of player release notes.

GitHub and CurseForge use the same generated player-facing notes. Preview a
release before publication:

```sh
python scripts/release_notes.py --tag <tag> --ref <commit-or-tag> --repository-url https://github.com/KogasaPls/WoWVoxPacks
```

Check a branch's new commit subjects:

```sh
python scripts/commit_messages.py --base origin/main --head HEAD
```

Published commits and tags stay stable. Correct published release text separately
when requested rather than rewriting the history that produced its archives.
