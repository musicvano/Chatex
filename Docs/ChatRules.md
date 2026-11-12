# Chat rules

[← Docs index](Index.md)

Chats work like Telegram's. The rules live in `Chatex.Business/Chats` (`ChatPermissions`, `ChatAccess`, `ChatService`) and `ChatMember.CanPost`.

## Chat types

| Type | Members | Who posts |
|---|---|---|
| **Direct** | exactly two users; one chat per pair, created on first use | both |
| **Group** | up to 1000 | everyone |
| **Channel** | unlimited subscribers | owner and admins only |

Groups and channels can be **public** (they have a unique `@handle`, can be found with `GET /api/chats/search` and joined by anyone) or **private** (no handle, invisible to non-members). Handles are 5–32 letters, digits or underscores, starting with a letter.

## Roles

| Role | Used in |
|---|---|
| `owner` | groups and channels; exactly one, the creator until ownership is transferred |
| `admin` | groups and channels |
| `member` | direct chats and groups |
| `subscriber` | channels (read-only) |

People who join or are added get the regular role of the chat: `member` in a group, `subscriber` in a channel.

## Permissions

| Action | Who |
|---|---|
| See a chat | members; anyone for a public chat |
| Send messages | members of direct chats and groups; owner and admins of channels |
| Edit a message | its sender only |
| Delete a message | its sender, or an owner or admin |
| Edit title, description, handle, avatar | owner and admins (not in direct chats) |
| Add members | owner and admins (not in direct chats), up to 100 per request |
| Remove a member | the owner removes anyone else; an admin removes members and subscribers only |
| Change roles | owner only; nobody changes their own role |
| Transfer ownership | owner, by giving someone the `owner` role; the old owner becomes an admin |
| List members | everyone in direct chats and groups; owner and admins in channels |
| Delete the chat | owner only (not in direct chats) |
| Leave | anyone except in direct chats; the owner must transfer ownership first |

## Visibility and errors

- A private chat the caller is not in answers `404 chat_not_found`, the same as a chat that does not exist, so its existence does not leak.
- A public chat the caller has not joined can be read but not posted in (`403 not_a_member`).
- A role that does not allow the action answers `403 not_allowed`; posting in a channel without rights answers `403 cannot_post`.
- Joining a chat twice is not an error.
- Read receipts (`MessagesRead` events) are sent for direct chats and groups, not for channels.

See the full list in [HTTP API → Error codes](HttpApi.md#error-codes).
