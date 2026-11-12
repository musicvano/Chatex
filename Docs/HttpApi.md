# HTTP API

[← Docs index](Index.md)

All endpoints live under `/api` and need an access token (`Authorization: Bearer <token>`), except register, login, refresh and logout. In Development the interactive reference is at `/scalar` and the OpenAPI document at `/openapi/v1.json`.

## Routes

| Area | Routes |
|---|---|
| Auth | `POST /api/auth/register`, `/login`, `/refresh`, `/logout`, `/logout-all` |
| Profile | `GET`/`PUT /api/me`, `PUT /api/me/password`, `PUT`/`DELETE /api/me/avatar` |
| Users | `GET /api/users?query=`, `GET /api/users/{id}`, `GET /api/users/{id}/avatar` |
| Chats | `GET`/`POST /api/chats`, `POST /api/chats/direct`, `GET /api/chats/search?query=`, `GET`/`PUT`/`DELETE /api/chats/{id}`, avatar, `join`, `leave`, `members`, `members/{userId}/role`, `mute`, `read` |
| Messages | `GET`/`POST /api/chats/{id}/messages?before=&after=&limit=`, `PUT`/`DELETE /api/messages/{id}` |
| Attachments | `POST /api/attachments` (multipart, field `file`), `GET /api/attachments/{id}` |

Who may call which chat and message routes is described in [Chat rules](ChatRules.md).

## Conventions

- **Tokens.** Login, register and refresh return the access token in the body and set the refresh token as the `chatex_refresh` cookie (`HttpOnly`, `Secure`, `SameSite=Strict`, path `/api/auth`). Scripts never see it; the browser sends it only to `/api/auth/refresh` and `/api/auth/logout`. Refresh tokens rotate on every use, and reusing an old one ends every session of that user. Five failed logins lock the account for 15 minutes.
- **Errors** are `application/problem+json`. The `code` field is a stable machine-readable code (see [below](#error-codes)); validation errors also have `errors` keyed by field. Status codes: validation 400, unauthorized 401, forbidden 403, not found 404, conflict 409. Malformed bodies also answer 400.
- **Enums** are camelCase strings (`"group"`, `"admin"`).
- **Files.** Avatars and attachments need the access token too, so the frontend loads them with `HttpClient` as blobs instead of plain `<img src>` URLs.

## Paging

Lists return `{ items, nextCursor }`. **Cursors** are opaque strings: pass `nextCursor` back as `cursor` (chat lists, members) or `before` (message history). `limit` defaults to 50 and is capped at 100.

Message history has three modes:

| Query | Returns |
|---|---|
| no cursor | the newest messages, newest first |
| `before=<cursor>` | older messages, newest first (scrolling up) |
| `after=<cursor>` | newer messages, oldest first (catching up after a reconnect) |

Passing both `before` and `after` is a validation error (`both_cursors`). Message ids are UUIDv7, so ordering by id is ordering by time, and the database index on `(chat_id, id)` serves every page with a keyset query instead of an offset.

## Limits

| What | Limit |
|---|---|
| Message body | 4000 characters; may be empty only when the message has attachments |
| Attachments per message | 10 |
| Attachment size | `Attachments:MaxBytes` (20 MB by default), allow-listed media types only |
| Avatar | JPEG, PNG or WebP, at most 5 MB |
| Group members | 1000; at most 100 added per request |
| Chat title / description | 128 / 1000 characters |
| User name | 3–32 letters, digits or underscores, starting with a letter; stored in lower case |
| Public chat handle | 5–32 letters, digits or underscores, starting with a letter; stored in lower case |
| Display name / bio | 64 / 500 characters |
| Password | at least 8 characters with a digit |
| Search query | at least 2 characters |

## Error codes

| Code | Status | Meaning |
|---|---|---|
| `validation_failed` | 400 | one or more fields are invalid; see `errors` |
| `invalid_cursor` | 400 | the cursor is not valid |
| `both_cursors` | 400 | `before` and `after` were both given |
| `query_too_short` | 400 | search needs at least 2 characters |
| `direct_with_self` | 400 | a direct chat with yourself |
| `not_for_direct_chats` | 400 | the action does not apply to direct chats |
| `unknown_users` | 400 | some of the given users do not exist |
| `invalid_user_count` | 400 | add between 1 and 100 users at a time |
| `invalid_role` | 400 | the role is not available in this chat type |
| `own_role` | 400 | you cannot change your own role |
| `use_leave` | 400 | remove yourself with `leave` instead |
| `reply_not_found` | 400 | the replied message is not in this chat |
| `invalid_attachments` | 400 | attachments do not exist, are not yours or are already sent |
| `attachment_invalid_type`, `attachment_too_large`, `attachment_empty` | 400 | upload rejected |
| `avatar_invalid_type`, `avatar_too_large` | 400 | avatar rejected |
| `wrong_password` | 400 | the current password is wrong |
| `invalid_credentials` | 401 | wrong user name, email or password |
| `locked_out` | 401 | too many failed attempts |
| `invalid_refresh_token` | 401 | the session has expired; sign in again |
| `not_a_member` | 403 | join the chat first |
| `not_allowed` | 403 | your role does not allow this |
| `cannot_post` | 403 | only the owner and admins post in a channel |
| `not_sender` | 403 | only the sender can edit a message |
| `chat_not_found` | 404 | the chat does not exist, or it is private and you are not a member |
| `member_not_found`, `message_not_found`, `user_not_found`, `attachment_not_found`, `avatar_not_found` | 404 | the item does not exist |
| `user_name_taken`, `email_taken` | 409 | registration conflict |
| `handle_taken` | 409 | the public handle is in use |
| `owner_must_transfer` | 409 | the owner must hand over ownership before leaving |
| `chat_full` | 409 | the group already has 1000 members |

The codes come from the `*Errors` classes in `Chatex.Business`. Add new codes there and to this table.
