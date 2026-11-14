# Publish through the API

Use available Git-object tools to publish the reviewed commit/tree with explicit verified public author/committer identity. Local Git configuration does not configure API identity. Do not publish probe objects.

Preserve every intended addition, edit and deletion and the observed remote base. Serialize dependent writes; update refs without force and reconcile concurrent updates. Verify the resulting diff/tree and identity against the reviewed local result before opening/updating a PR.

A tool unable to represent the complete change is a transport blocker. Preserve the local result and report the missing capability rather than omit files, deletions or history.
