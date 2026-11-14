# Verify existing artifacts

Use [execution setup](execution.md). Identify the candidate version, source commit and expected package SHA-256 from trusted candidate/rehearsal evidence, then run:

```sh
node scripts/verify-artifact.mjs ARTIFACT_DIRECTORY VERSION SOURCE_COMMIT NUPKG_SHA256
```

This checks manifest identity, package/symbol hashes, metadata, Source Link and fresh-cache public consumers. Preserve the candidate files; verification must not rebuild or replace them.

Inspect `repo:tests/PackageInspection/Program.cs` and `scripts/consume-package.mjs` to establish the checks' current coverage. Report the exact checked identity and remaining limits.
