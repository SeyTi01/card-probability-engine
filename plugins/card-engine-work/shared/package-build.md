# Build a traceable candidate

Read [package infrastructure](package-infrastructure.md) and use [execution setup](execution.md). Resolve the intended version explicitly; inspect current script defaults rather than assuming they track the project version.

Require a repository-root checkout with all candidate source committed and no unrelated/untracked changes. For dirty source, use [source preparation](package-source.md) before continuing.

Choose an empty ignored output directory and run:

```sh
node scripts/validate.mjs --version VERSION --output artifacts/CANDIDATE
```

Substitute the agreed version and distinct output directory. The script owns the build, complete neutral suite, samples, one Release pack, inspection and isolated consumers; do not repeat them separately for the same candidate.

Return the source commit, version, manifest, package/symbol SHA-256 values and artifact paths. Keep these exact bytes for rehearsal and publication.
