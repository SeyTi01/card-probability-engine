# Choose source publication

Establish authentication without printing credentials. Public fetch access does not prove push access. Review outgoing commits/messages/paths/content and keep unrelated or private machine/account details out of publication.

| Available route | Procedure |
| --- | --- |
| Authenticated local Git, ordinary update | [Local identity](local-git.md); commit the reviewed task changes, push the intended ref normally and verify remote commit/tree. |
| GitHub API only, ordinary update | [API publication](api-publication.md) |
| Previously published branch was rebased | [Guarded rewritten publication](rewritten-branch.md) |

A missing transport blocks publication, not independent local work.
