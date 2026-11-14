# Compare published and candidate artifacts

Compare published assembly contents, package metadata, Source Link and symbols with the accepted candidate and its recorded hashes/source commit. Verify any NuGet-added repository signature using supported NuGet verification; compare payload contents rather than assuming the signed container retains the unsigned SHA-256. [NuGet signing reference](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference) documents repository signing.

A duplicate-version response does not establish identity. Resolve mismatches before further publication. Preserve the original candidate and record which published artifacts are confirmed or still missing.
