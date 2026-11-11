import { HttpErrorResponse } from '@angular/common/http';

/** The API's application/problem+json body. */
export interface Problem {
  status: number;
  code?: string;
  title?: string;
  detail?: string;
  /** Validation errors by field name (camelCase). */
  errors: Record<string, string[]>;
}

const FRIENDLY: Record<string, string> = {
  invalid_credentials: 'Wrong user name, email or password.',
  locked_out: 'Too many failed attempts. The account is locked for 15 minutes.',
  user_name_taken: 'This user name is already taken.',
  email_taken: 'An account with this email already exists.',
  wrong_password: 'The current password is wrong.',
  invalid_refresh_token: 'Your session has expired. Please sign in again.',
  avatar_invalid_type: 'Avatars must be JPEG, PNG or WebP images.',
  avatar_too_large: 'Avatars can be at most 5 MB.',
  attachment_invalid_type: 'This file type is not allowed.',
  attachment_too_large: 'This file is too large.',
  attachment_empty: 'The file is empty.',
  query_too_short: 'Type at least 2 characters.',
  handle_taken: 'This public handle is already taken.',
  unknown_users: 'Some of the selected users no longer exist.',
  direct_with_self: "You can't start a direct chat with yourself.",
  chat_full: 'This chat is full.',
  owner_must_transfer: 'Transfer ownership to another member before leaving.',
  not_a_member: 'You are not a member of this chat.',
  cannot_post: 'Only admins can post in this channel.',
  not_allowed: "You don't have permission to do that.",
  not_sender: 'You can only edit your own messages.',
  chat_not_found: 'This chat no longer exists.',
  user_not_found: 'This user no longer exists.',
  message_not_found: 'This message no longer exists.',
  member_not_found: 'This user is not a member.',
  reply_not_found: 'The message you replied to no longer exists.',
  invalid_attachments: 'Some attachments could not be used. Upload them again.',
  use_leave: 'Use "Leave chat" to remove yourself.',
  own_role: "You can't change your own role.",
  not_for_direct_chats: "Direct chats can't be changed this way.",
};

export function toProblem(error: unknown): Problem {
  if (error instanceof HttpErrorResponse) {
    const body = error.error && typeof error.error === 'object' ? error.error : {};
    const errors: Record<string, string[]> = {};
    for (const [key, messages] of Object.entries(body.errors ?? {})) {
      const field = key.length ? key[0].toLowerCase() + key.slice(1) : key;
      errors[field] = Array.isArray(messages) ? messages.map(String) : [String(messages)];
    }
    return {
      status: error.status,
      code: typeof body.code === 'string' ? body.code : undefined,
      title: typeof body.title === 'string' ? body.title : undefined,
      detail: typeof body.detail === 'string' ? body.detail : undefined,
      errors,
    };
  }
  return { status: 0, errors: {}, detail: error instanceof Error ? error.message : undefined };
}

/** A sentence to show the user for any failed request. */
export function problemMessage(error: unknown): string {
  const problem = toProblem(error);
  if (problem.code && FRIENDLY[problem.code]) {
    return FRIENDLY[problem.code];
  }
  if (problem.status === 0) {
    return "Can't reach the server. Check your connection.";
  }
  const firstFieldError = Object.values(problem.errors)[0]?.[0];
  return problem.detail || firstFieldError || problem.title || 'Something went wrong. Try again.';
}

/** Validation messages for one form field, or an empty list. */
export function fieldErrors(error: unknown, field: string): string[] {
  return toProblem(error).errors[field] ?? [];
}
