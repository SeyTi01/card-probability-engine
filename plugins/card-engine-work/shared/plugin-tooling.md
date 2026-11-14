# Test the packager

Run `node --test plugins/card-engine-work/scripts/package.test.mjs` from the repository root. Confirm archive round trips, shared-reference inclusion, malformed input, missing/escaping paths and symlink rejection. Fix failures rather than dropping coverage.
