# Data model

[← Docs index](Index.md)

The database is PostgreSQL 18, mapped with EF Core 10 and Npgsql. Table and column names are snake_case. Entities live in `Chatex.DataAccess/Entities`, their mappings (lengths, indexes, delete rules) in `Configurations`.

## Entities

```
User 1──1 UserProfile
User 1──* RefreshToken
User 1──* ChatMember *──1 Chat
Chat 1──* Message *──1 User (sender)
Message 0..1──* Message (replies)
Message 0..1──* Attachment *──1 User (uploader)
```

| Entity | What it holds |
|---|---|
| `User` | the ASP.NET Core Identity account: user name (the public @handle, lower case), email, password hash, lockout, `LastSeenAt` |
| `UserProfile` | what others see: display name, bio, avatar path; one per user, created at registration |
| `RefreshToken` | a hash of a refresh token (never the token itself), expiry, revocation, the token that replaced it, user agent and IP |
| `Chat` | a direct chat, group or channel: title, description, avatar, public handle, `LastActivityAt`, soft delete |
| `ChatMember` | a user in a chat: role, join time, read position (`LastReadMessageId`), muted flag; key `(chat_id, user_id)` |
| `Message` | body, sender, optional reply, edit time, soft delete |
| `Attachment` | an uploaded file: original name (display only), media type, size, storage path; linked to a message once sent |

## Design points

- **Ids are UUIDv7** (`Guid.CreateVersion7()`), so they sort by creation time. Message ids double as pagination cursors and read positions: a member's unread messages are the ones with an id greater than `LastReadMessageId`, which only moves forward.
- **Keyset paging.** The index on `messages (chat_id, id DESC)` serves history pages without offsets. The chat list is ordered by `Chat.LastActivityAt`, updated in the same transaction as each new message.
- **One direct chat per pair.** Direct chats have a `DirectKey` made of both user ids in sorted order, with a unique index. A check constraint makes `direct_key` set exactly when the type is `Direct`.
- **Public handles.** `PublicHandle` is unique; a chat with a handle is public and searchable, a chat without one is private.
- **Soft deletes.** Deleted messages keep their row (text and attachments are removed) so replies and read positions still point at them. Deleted chats get `DeletedAt` set and disappear for everyone.
- **Timestamps** (`CreatedAt`, `UpdatedAt`) are set by `TimestampInterceptor` from the injected `TimeProvider`, which lets tests control the clock.
- **Uniqueness errors.** Unique-index violations surface as `UniqueConstraintException` from `IUnitOfWork.SaveChangesAsync`; services turn them into `Conflict` errors such as `handle_taken`.
- **Enums** (chat type, member role) are stored as strings.

## Files

Uploaded files are stored on local disk under `Storage:RootPath`, at `{yyyy}/{mm}/{guid}`; the client's file name is never used as a path. An attachment is uploaded first with no message, then linked when the message is sent; a message can only use the sender's own unsent uploads. `IAttachmentService.CleanupStaleAsync` deletes uploads that were never sent, but nothing schedules it yet.

## Working on the schema

After changing an entity or a configuration in `Chatex.DataAccess`, add a migration:

```bash
dotnet ef migrations add <Name> --project Chatex.DataAccess --output-dir Migrations
```

To check that the model and the migrations agree:

```bash
dotnet ef migrations has-pending-model-changes --project Chatex.DataAccess
```

`dotnet ef` reads the connection string from the `CHATEX_DB` environment variable (see [Getting started](GettingStarted.md#create-the-database)).
