import { DIALOG_DATA, Dialog, DialogConfig, DialogRef } from '@angular/cdk/dialog';
import { ComponentType } from '@angular/cdk/portal';
import { ChangeDetectionStrategy, Component, Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

/** Opens app dialogs with shared styling (a centred card that fills the width on phones). */
@Injectable({ providedIn: 'root' })
export class Dialogs {
  private readonly dialog = inject(Dialog);

  open<R, D, C>(
    component: ComponentType<C>,
    data?: D,
    config: DialogConfig<D, DialogRef<R, C>> = {},
  ): DialogRef<R, C> {
    return this.dialog.open<R, D, C>(component, {
      data,
      autoFocus: 'first-tabbable',
      restoreFocus: true,
      backdropClass: 'cdk-overlay-dark-backdrop',
      panelClass: 'app-dialog-panel',
      maxWidth: '100vw',
      ...config,
    });
  }

  async confirm(data: ConfirmData): Promise<boolean> {
    const ref = this.open<boolean, ConfirmData, ConfirmDialog>(ConfirmDialog, data);
    return (await firstValueFrom(ref.closed)) === true;
  }
}

export interface ConfirmData {
  title: string;
  message: string;
  confirmText?: string;
  danger?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog-card p-6" role="alertdialog" aria-labelledby="confirm-title">
      <h2 id="confirm-title" class="text-lg font-semibold">{{ data.title }}</h2>
      <p class="mt-2 text-sm text-muted">{{ data.message }}</p>
      <div class="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
        <button type="button" class="btn-secondary" (click)="ref.close(false)">Cancel</button>
        <button
          type="button"
          [class]="data.danger ? 'btn-danger' : 'btn-primary'"
          (click)="ref.close(true)"
        >
          {{ data.confirmText ?? 'Confirm' }}
        </button>
      </div>
    </div>
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmData>(DIALOG_DATA);
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
}
