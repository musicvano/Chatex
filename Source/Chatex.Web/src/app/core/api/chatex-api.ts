import { HttpClient, HttpEvent, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  AttachmentDto,
  AuthResponse,
  ChangePasswordRequest,
  ChatDto,
  ChatListItemDto,
  ChatMemberDto,
  ChatMemberRole,
  ChatSummaryDto,
  CreateChatRequest,
  LoginRequest,
  MessageDto,
  MyProfileDto,
  PageDto,
  RegisterRequest,
  SendMessageRequest,
  UpdateChatRequest,
  UpdateProfileRequest,
  UserDto,
  UserSummaryDto,
} from './models';

/** Builds query params, skipping null and undefined values. */
function params(values: Record<string, string | number | null | undefined>): HttpParams {
  let result = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== null && value !== undefined && value !== '') {
      result = result.set(key, String(value));
    }
  }
  return result;
}

function fileForm(file: File): FormData {
  const form = new FormData();
  form.append('file', file, file.name);
  return form;
}

/** /api/auth: the refresh token travels in an HttpOnly cookie, see authInterceptor. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/register', body);
  }

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/login', body);
  }

  refresh(): Observable<AuthResponse> {
    return this.http.post<AuthResponse>('/api/auth/refresh', null);
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', null);
  }

  logoutAll(): Observable<void> {
    return this.http.post<void>('/api/auth/logout-all', null);
  }
}

/** /api/me */
@Injectable({ providedIn: 'root' })
export class ProfileApi {
  private readonly http = inject(HttpClient);

  get(): Observable<MyProfileDto> {
    return this.http.get<MyProfileDto>('/api/me');
  }

  update(body: UpdateProfileRequest): Observable<MyProfileDto> {
    return this.http.put<MyProfileDto>('/api/me', body);
  }

  changePassword(body: ChangePasswordRequest): Observable<void> {
    return this.http.put<void>('/api/me/password', body);
  }

  uploadAvatar(file: File): Observable<MyProfileDto> {
    return this.http.put<MyProfileDto>('/api/me/avatar', fileForm(file));
  }

  deleteAvatar(): Observable<MyProfileDto> {
    return this.http.delete<MyProfileDto>('/api/me/avatar');
  }
}

/** /api/users */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);

  search(query: string, limit = 20): Observable<UserSummaryDto[]> {
    return this.http.get<UserSummaryDto[]>('/api/users', { params: params({ query, limit }) });
  }

  get(id: string): Observable<UserDto> {
    return this.http.get<UserDto>(`/api/users/${id}`);
  }
}

/** /api/chats */
@Injectable({ providedIn: 'root' })
export class ChatsApi {
  private readonly http = inject(HttpClient);

  list(cursor?: string | null, limit = 30): Observable<PageDto<ChatListItemDto>> {
    return this.http.get<PageDto<ChatListItemDto>>('/api/chats', {
      params: params({ cursor, limit }),
    });
  }

  create(body: CreateChatRequest): Observable<ChatDto> {
    return this.http.post<ChatDto>('/api/chats', body);
  }

  direct(userId: string): Observable<ChatDto> {
    return this.http.post<ChatDto>('/api/chats/direct', { userId });
  }

  search(query: string, limit = 20): Observable<ChatSummaryDto[]> {
    return this.http.get<ChatSummaryDto[]>('/api/chats/search', {
      params: params({ query, limit }),
    });
  }

  get(id: string): Observable<ChatDto> {
    return this.http.get<ChatDto>(`/api/chats/${id}`);
  }

  update(id: string, body: UpdateChatRequest): Observable<ChatDto> {
    return this.http.put<ChatDto>(`/api/chats/${id}`, body);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/chats/${id}`);
  }

  uploadAvatar(id: string, file: File): Observable<ChatDto> {
    return this.http.put<ChatDto>(`/api/chats/${id}/avatar`, fileForm(file));
  }

  deleteAvatar(id: string): Observable<ChatDto> {
    return this.http.delete<ChatDto>(`/api/chats/${id}/avatar`);
  }

  join(id: string): Observable<ChatDto> {
    return this.http.post<ChatDto>(`/api/chats/${id}/join`, null);
  }

  leave(id: string): Observable<void> {
    return this.http.post<void>(`/api/chats/${id}/leave`, null);
  }

  members(id: string, cursor?: string | null, limit = 50): Observable<PageDto<ChatMemberDto>> {
    return this.http.get<PageDto<ChatMemberDto>>(`/api/chats/${id}/members`, {
      params: params({ cursor, limit }),
    });
  }

  addMembers(id: string, userIds: string[]): Observable<{ added: number }> {
    return this.http.post<{ added: number }>(`/api/chats/${id}/members`, { userIds });
  }

  removeMember(id: string, userId: string): Observable<void> {
    return this.http.delete<void>(`/api/chats/${id}/members/${userId}`);
  }

  setRole(id: string, userId: string, role: ChatMemberRole): Observable<void> {
    return this.http.put<void>(`/api/chats/${id}/members/${userId}/role`, { role });
  }

  mute(id: string, muted: boolean): Observable<void> {
    return this.http.put<void>(`/api/chats/${id}/mute`, { muted });
  }

  markRead(id: string, messageId: string): Observable<void> {
    return this.http.post<void>(`/api/chats/${id}/read`, { messageId });
  }
}

/** /api/chats/{id}/messages and /api/messages */
@Injectable({ providedIn: 'root' })
export class MessagesApi {
  private readonly http = inject(HttpClient);

  /**
   * No cursor: the newest page, newest first. `before`: older pages, newest first.
   * `after`: newer messages, oldest first (catching up after a reconnect).
   */
  list(
    chatId: string,
    options: { before?: string | null; after?: string | null; limit?: number } = {},
  ): Observable<PageDto<MessageDto>> {
    return this.http.get<PageDto<MessageDto>>(`/api/chats/${chatId}/messages`, {
      params: params({ before: options.before, after: options.after, limit: options.limit ?? 50 }),
    });
  }

  send(chatId: string, body: SendMessageRequest): Observable<MessageDto> {
    return this.http.post<MessageDto>(`/api/chats/${chatId}/messages`, body);
  }

  edit(id: string, body: string): Observable<MessageDto> {
    return this.http.put<MessageDto>(`/api/messages/${id}`, { body });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/messages/${id}`);
  }
}

/** /api/attachments: upload first, then send the ids with the message. */
@Injectable({ providedIn: 'root' })
export class AttachmentsApi {
  private readonly http = inject(HttpClient);

  upload(file: File): Observable<HttpEvent<AttachmentDto>> {
    return this.http.post<AttachmentDto>('/api/attachments', fileForm(file), {
      reportProgress: true,
      observe: 'events',
    });
  }
}

export const avatarUrl = {
  user: (id: string) => `/api/users/${id}/avatar`,
  chat: (id: string) => `/api/chats/${id}/avatar`,
};

export const attachmentUrl = (id: string) => `/api/attachments/${id}`;
