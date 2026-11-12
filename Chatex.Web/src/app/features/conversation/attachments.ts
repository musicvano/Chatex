import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
} from '@angular/core';
import { attachmentUrl } from '../../core/api/chatex-api';
import { AttachmentDto } from '../../core/api/models';
import { BlobUrlService } from '../../core/media/blob-url.service';
import { ToastService } from '../../core/ui/toast.service';
import { Dialogs } from '../../shared/ui/dialogs';
import { ICONS } from '../../shared/ui/icons';
import { FileSizePipe } from '../../shared/ui/pipes';

export const isImage = (a: AttachmentDto) =>
  ['image/jpeg', 'image/png', 'image/gif', 'image/webp'].includes(a.contentType);

/** One image attachment as a fixed-size thumbnail, so loading it never shifts the message list. */
@Component({
  selector: 'app-attachment-image',
  imports: [ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button
      type="button"
      class="block h-44 w-full overflow-hidden rounded-xl bg-black/10 sm:h-52"
      [attr.aria-label]="'Open image ' + attachment().fileName"
      (click)="open()"
    >
      @if (src.value(); as url) {
        <img [src]="url" [alt]="attachment().fileName" class="size-full object-cover" />
      } @else if (src.isLoading()) {
        <span class="flex size-full items-center justify-center"
          ><svg lucideLoaderCircle class="size-6 animate-spin opacity-60"></svg
        ></span>
      } @else {
        <span class="flex size-full items-center justify-center text-xs opacity-70"
          >Image unavailable</span
        >
      }
    </button>
  `,
})
export class AttachmentImage {
  private readonly blobs = inject(BlobUrlService);
  private readonly dialogs = inject(Dialogs);
  readonly attachment = input.required<AttachmentDto>();

  protected readonly src = resource({
    params: () => attachmentUrl(this.attachment().id),
    loader: ({ params }) => this.blobs.get(params),
  });

  protected open(): void {
    const url = this.src.value();
    if (url) {
      this.dialogs.open(
        ImageViewer,
        { url, attachment: this.attachment() },
        { panelClass: 'app-dialog-panel' },
      );
    }
  }
}

/** The attachments of one message: image thumbnails first, then file chips. */
@Component({
  selector: 'app-message-attachments',
  imports: [AttachmentImage, FileSizePipe, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (images().length) {
      <div class="mb-1 grid gap-1" [class.grid-cols-2]="images().length > 1">
        @for (a of images(); track a.id) {
          <app-attachment-image [attachment]="a" />
        }
      </div>
    }
    @for (a of files(); track a.id) {
      <button
        type="button"
        class="mb-1 flex w-full min-w-0 items-center gap-3 rounded-xl px-2 py-2 text-left"
        [class]="mine() ? 'bg-white/15 hover:bg-white/25' : 'bg-subtle hover:bg-line'"
        [attr.aria-label]="'Download ' + a.fileName"
        (click)="download(a)"
      >
        <span
          class="flex size-10 shrink-0 items-center justify-center rounded-lg"
          [class]="
            mine()
              ? 'bg-white/20'
              : 'bg-brand-100 text-brand-700 dark:bg-brand-950 dark:text-brand-300'
          "
        >
          <svg lucideFileText class="size-5"></svg>
        </span>
        <span class="min-w-0 flex-1">
          <span class="block truncate text-sm font-medium">{{ a.fileName }}</span>
          <span class="block text-xs opacity-70">{{ a.sizeBytes | fileSize }}</span>
        </span>
        <svg lucideDownload class="size-4 shrink-0 opacity-70"></svg>
      </button>
    }
  `,
})
export class MessageAttachments {
  private readonly blobs = inject(BlobUrlService);
  private readonly toast = inject(ToastService);

  readonly attachments = input.required<AttachmentDto[]>();
  readonly mine = input(false);

  protected readonly images = computed(() => this.attachments().filter(isImage));
  protected readonly files = computed(() => this.attachments().filter((a) => !isImage(a)));

  protected async download(a: AttachmentDto): Promise<void> {
    try {
      await this.blobs.download(attachmentUrl(a.id), a.fileName);
    } catch (error) {
      this.toast.error(error);
    }
  }
}

/** Full-size image with a download button. */
@Component({
  selector: 'app-image-viewer',
  imports: [ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative flex max-h-[100dvh] max-w-[100vw] flex-col items-center">
      <div class="absolute top-2 right-2 z-10 flex gap-2">
        <button
          type="button"
          class="flex size-10 items-center justify-center rounded-full bg-black/60 text-white hover:bg-black/80"
          aria-label="Download"
          (click)="download()"
        >
          <svg lucideDownload class="size-5"></svg>
        </button>
        <button
          type="button"
          class="flex size-10 items-center justify-center rounded-full bg-black/60 text-white hover:bg-black/80"
          aria-label="Close"
          (click)="ref.close()"
        >
          <svg lucideX class="size-5"></svg>
        </button>
      </div>
      <img
        [src]="data.url"
        [alt]="data.attachment.fileName"
        class="max-h-[92dvh] max-w-[96vw] rounded-lg object-contain shadow-2xl"
      />
    </div>
  `,
})
export class ImageViewer {
  protected readonly data = inject<{ url: string; attachment: AttachmentDto }>(DIALOG_DATA);
  protected readonly ref = inject(DialogRef);
  private readonly blobs = inject(BlobUrlService);

  protected download(): void {
    void this.blobs.download(attachmentUrl(this.data.attachment.id), this.data.attachment.fileName);
  }
}
