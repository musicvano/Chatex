import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ChatsApi } from '../../core/api/chatex-api';
import { UserSummaryDto } from '../../core/api/models';
import { problemMessage } from '../../core/api/problem';
import { ICONS } from '../../shared/ui/icons';
import { UserSearch } from '../../shared/ui/user-search';

export interface AddMembersData {
  chatId: string;
  existing: string[];
}

/** Picks users and adds them to a chat; closes with the number added. */
@Component({
  selector: 'app-add-members-dialog',
  imports: [UserSearch, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog-card p-5" role="dialog" aria-labelledby="add-members-title">
      <div class="mb-4 flex items-center">
        <h2 id="add-members-title" class="flex-1 text-lg font-semibold">Add members</h2>
        <button type="button" class="-mr-2 icon-btn" aria-label="Close" (click)="ref.close()">
          <svg lucideX class="size-5"></svg>
        </button>
      </div>
      @if (error()) {
        <p
          class="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300"
          role="alert"
        >
          {{ error() }}
        </p>
      }
      <app-user-search [multiple]="true" [exclude]="data.existing" [(selected)]="selected" />
      <div class="mt-4 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" class="btn-secondary" (click)="ref.close()">Cancel</button>
        <button
          type="button"
          class="btn-primary"
          [disabled]="!selected().length || busy()"
          (click)="add()"
        >
          @if (busy()) {
            <svg lucideLoaderCircle class="size-4 animate-spin"></svg>
          }
          Add {{ selected().length || '' }}
        </button>
      </div>
    </div>
  `,
})
export class AddMembersDialog {
  protected readonly ref = inject<DialogRef<number>>(DialogRef);
  protected readonly data = inject<AddMembersData>(DIALOG_DATA);
  private readonly api = inject(ChatsApi);
  protected readonly selected = signal<UserSummaryDto[]>([]);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async add(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);
    try {
      const result = await firstValueFrom(
        this.api.addMembers(
          this.data.chatId,
          this.selected().map((u) => u.id),
        ),
      );
      this.ref.close(result.added);
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}
