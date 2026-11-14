# Package infrastructure

Read `repo:docs/contributing.md`, `src/CardProbability/CardProbability.csproj`, the affected `scripts/` code and `.github/workflows/validate.yml` / `publish.yml`.

Preserve BCL-only production dependencies, the `CardProbability` assembly/namespace, XML documentation, MIT attribution, README/license, Source Link and portable symbols. Test frameworks and host assemblies stay out of the package.

Preserve clean committed source identity, isolated package-only consumers, candidate manifest/hashes and publication of the exact validated artifacts without rebuilding. Keep workflow permissions and Trusted Publishing scoped to the canonical repository, workflow and environment. Never store credentials or bypass provenance checks.

Historical transfer hashes in `repo:docs/transfer-manifest.json` describe the original extraction. Do not rewrite them to make later source changes appear identical to that baseline.
