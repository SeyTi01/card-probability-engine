# Prepare release source

Read `repo:docs/contributing.md` and current validation/publication workflows. Inspect the requested version/tag, existing release PRs, candidate runs and published versions. Reuse existing preparation; never downgrade later work or replace a conflicting tag/artifact.

Identify the exact intended version and source changes. Resolve version literals from current project/scripts/workflows; keep historical provenance unchanged. Use [implementation](../skills/card-engine-implement/SKILL.md) for necessary changes and return its reviewed PR to the caller. Do not create an empty version bump.
