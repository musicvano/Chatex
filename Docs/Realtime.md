# Realtime (SignalR)

[← Docs index](Index.md)

The hub lives at `/hubs/chat` and needs the same access token as the API. Browsers cannot send headers on WebSocket and Server-Sent Events requests, so the token may also come as the `access_token` query parameter, which the server accepts only on `/hubs` paths. Payloads are JSON with camelCase names and camelCase enum strings, the same as the HTTP API.

On connect the server adds the connection to one group per chat of the user (`chat:{chatId}`), so chat events reach current members only. Members added later join the group on their open connections right away; removed members get `MemberRemoved` and then nothing more from that chat.

## Server to client

| Event | Arguments | When |
|---|---|---|
| `MessageCreated` | `message` | a message was sent |
| `MessageUpdated` | `message` | a message was edited |
| `MessageDeleted` | `chatId, messageId` | a message was deleted |
| `MessagesRead` | `chatId, userId, lastReadMessageId` | a member's read position moved forward (not for channels) |
| `MembersAdded` | `chatId, userIds[]` | users joined or were added, including everyone in a new chat |
| `MemberRemoved` | `chatId, userId` | a user left or was removed |
| `ChatUpdated` | `chatId` | title, description, handle, avatar or roles changed; refetch the chat |
| `ChatDeleted` | `chatId` | the owner deleted the chat |
| `Typing` | `chatId, userId` | another member is typing; expire the indicator after about 5 s |
| `UserOnline` | `userId` | a user who shares a chat with you opened their first connection |
| `UserOffline` | `userId, lastSeenAt` | that user's last connection closed and they did not return within the grace period |

## Client to server

| Method | Returns | Notes |
|---|---|---|
| `Typing(chatId)` | nothing | throttle to one call every 3 s; fails with a `HubException` if you are not a member |
| `GetOnlineUsers(userIds[])` | `userIds[]` | which of the given users are online now |

## Presence

Presence is kept in memory: a user is online while they have at least one connection (tabs and devices count separately). When the last one closes, the server waits `Presence:OfflineGraceSeconds` so a page reload does not flicker, then saves `lastSeenAt` and sends `UserOffline`. Because it is in memory, the API runs as a single process; scaling out would need a SignalR backplane.

## Reconnects

The handshake finishes before the server has joined the groups. After connecting or reconnecting, fetch what you missed with `GET /api/chats/{id}/messages?after=` rather than relying on events alone.

## Adding an event

Server events are raised by business services through `IChatNotifier` after a commit. A new event touches, in order: `IChatNotifier` and `NullChatNotifier` (Business), `IChatClient` and `SignalRChatNotifier` (Api), the `HubEvent` union in `Chatex.Web/src/app/core/realtime/realtime.service.ts`, the stores that react to it, and the table above.
