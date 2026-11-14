# Publish a rebased branch

Use the remote SHA recorded before rebase and an explicit Git lease:
`--force-with-lease=refs/heads/BRANCH:EXPECTED_SHA`.
A failed lease requires fetching and reconciling concurrent work. Never force unconditionally.

API ref updates do not provide that atomic lease. Use guarded Git for rewritten publication; keep the integrated branch local and report the transport blocker when that route is unavailable. Verify the remote commit/tree after success.
