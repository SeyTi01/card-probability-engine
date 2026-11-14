# Verify the published version

Use [execution setup](execution.md). Read the exact NuGet.org version/listing and confirm package ID, repository, version, MIT attribution and README.

Restore that pinned version from NuGet.org into a fresh cache and temporary consumer outside the repository source graph. Reuse the public consumer and sample programs with only a `PackageReference`; suppress inherited build imports and verify resolved assets contain the intended package, not a project reference or local feed. Run the consumer and samples.

Record the resolved package identity and outcomes. Release acceptance also requires [candidate comparison](published-identity.md).
