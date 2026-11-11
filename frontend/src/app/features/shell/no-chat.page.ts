import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Dialogs } from '../../shared/ui/dialogs';
import { ICONS } from '../../shared/ui/icons';
import { NewChatDialog } from '../new-chat/new-chat-dialog';

/** The main pane on tablets and desktops when no chat is open (phones show the list instead). */
@Component({
  selector: 'app-no-chat-page',
  imports: [ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex flex-1 flex-col items-center justify-center gap-4 bg-canvas p-8 text-center' },
  template: `
    <span class="flex size-20 items-center justify-center rounded-3xl bg-brand-50 text-brand-600 dark:bg-brand-950 dark:text-brand-300">
      <svg lucideMessagesSquare class="size-10"></svg>
    </span>
    <div>
      <h2 class="text-lg font-semibold">Select a chat</h2>
      <p class="mt-1 text-sm text-muted">Pick a conversation from the list or start a new one.</p>
    </div>
    <button type="button" class="btn-primary" (click)="newChat()"><svg lucidePlus class="size-4"></svg>New chat</button>
  `,
})
export class NoChatPage {
  private readonly dialogs = inject(Dialogs);

  protected newChat(): void {
    this.dialogs.open(NewChatDialog);
  }
}
