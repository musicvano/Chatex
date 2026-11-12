/** DTOs of the Chatex API. Ids are GUID strings, dates are ISO strings, enums are camelCase strings. */

export type ChatType = 'direct' | 'group' | 'channel';
export type ChatMemberRole = 'owner' | 'admin' | 'member' | 'subscriber';

export interface MyProfileDto {
  id: string;
  userName: string;
  email: string;
  displayName: string;
  bio?: string | null;
  hasAvatar: boolean;
  createdAt: string;
}

export interface UserDto {
  id: string;
  userName: string;
  displayName: string;
  bio?: string | null;
  hasAvatar: boolean;
  lastSeenAt?: string | null;
}

export interface UserSummaryDto {
  id: string;
  userName: string;
  displayName: string;
  hasAvatar: boolean;
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: MyProfileDto;
}

export interface ChatDto {
  id: string;
  type: ChatType;
  title?: string | null;
  description?: string | null;
  publicHandle?: string | null;
  hasAvatar: boolean;
  createdAt: string;
  memberCount: number;
  /** Null when the caller is not a member (public chat preview). */
  myRole?: ChatMemberRole | null;
  isMuted: boolean;
  peer?: UserSummaryDto | null;
}

export interface LastMessageDto {
  id: string;
  senderId: string;
  body: string;
  createdAt: string;
}

export interface ChatListItemDto {
  id: string;
  type: ChatType;
  title?: string | null;
  hasAvatar: boolean;
  lastActivityAt: string;
  myRole: ChatMemberRole;
  isMuted: boolean;
  lastMessage?: LastMessageDto | null;
  unreadCount: number;
  peer?: UserSummaryDto | null;
}

export interface ChatSummaryDto {
  id: string;
  type: ChatType;
  title?: string | null;
  description?: string | null;
  publicHandle?: string | null;
  hasAvatar: boolean;
}

export interface ChatMemberDto {
  user: UserSummaryDto;
  role: ChatMemberRole;
  joinedAt: string;
}

export interface AttachmentDto {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
}

export interface MessageDto {
  id: string;
  chatId: string;
  sender: UserSummaryDto;
  /** Null when the message is deleted. */
  body?: string | null;
  replyToMessageId?: string | null;
  createdAt: string;
  editedAt?: string | null;
  isDeleted: boolean;
  attachments: AttachmentDto[];
}

export interface PageDto<T> {
  items: T[];
  nextCursor?: string | null;
}

// Requests

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  login: string;
  password: string;
}

export interface UpdateProfileRequest {
  displayName: string;
  bio?: string | null;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface CreateChatRequest {
  type: 'group' | 'channel';
  title: string;
  description?: string | null;
  publicHandle?: string | null;
  memberIds?: string[];
}

export interface UpdateChatRequest {
  title: string;
  description?: string | null;
  publicHandle?: string | null;
}

export interface SendMessageRequest {
  body?: string | null;
  replyToMessageId?: string | null;
  attachmentIds?: string[];
}

/** Display name of a chat: direct chats have no title, so they use the peer's name. */
export function chatTitle(chat: { title?: string | null; peer?: UserSummaryDto | null }): string {
  return chat.title || chat.peer?.displayName || 'Chat';
}

export function canManageChat(role: ChatMemberRole | null | undefined): boolean {
  return role === 'owner' || role === 'admin';
}

/** Channels accept posts from owners and admins only. */
export function canPost(type: ChatType, role: ChatMemberRole | null | undefined): boolean {
  if (!role) {
    return false;
  }
  return type !== 'channel' || canManageChat(role);
}

/** UUIDv7 ids sort by creation time, so comparing the canonical strings orders messages. */
export function compareIds(a: string, b: string): number {
  return a < b ? -1 : a > b ? 1 : 0;
}
