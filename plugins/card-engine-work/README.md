# Card Engine Work

Skills for `SeyTi01/card-probability-engine`, adapted from YGO Work 1.1.4. The portable plugin uses the existing GitHub connection and provides ten standalone entry points:

- `card-engine-plan`, `card-engine-implement`, `card-engine-diagnose`
- `card-engine-verify`, `card-engine-package`, `card-engine-review`
- `card-engine-issue`, `card-engine-pr`, `card-engine-rebase`, `card-engine-release`

Each skill loads [shared context](shared/context.md). [Contract routing](shared/change-routes.md) selects technical guidance; dedicated references own execution, publication and release stages. Repository docs remain authoritative for model/API semantics.

This adaptation removes calculator UI, localization, card data/artwork, Cloudflare, app persistence and routine browser/worker setup. It adds library ownership, binary API compatibility, neutral probability oracles, actual package consumers, main-based development and exact-artifact NuGet publication. Calculator integration is isolated behind the host-rehearsal trigger.

The maintainer workflow is [plugin maintenance](shared/plugin-maintenance.md). From the repository root:

```sh
node plugins/card-engine-work/scripts/package.mjs --check
node --test plugins/card-engine-work/scripts/package.test.mjs
node plugins/card-engine-work/scripts/package.mjs /absolute/path/outside/plugin/card-engine-work.tar.gz
```

The packager includes `plugin.json`, `.app.json`, assets, skills and shared instructions. Source tooling and this README stay outside the uploaded archive. Plugin versioning is independent of the engine package and runtime model version.
