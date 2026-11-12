import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  resource,
} from '@angular/core';
import { avatarUrl } from '../../core/api/chatex-api';
import { BlobUrlService } from '../../core/media/blob-url.service';

const COLORS = [
  'bg-rose-500',
  'bg-orange-500',
  'bg-amber-500',
  'bg-emerald-500',
  'bg-teal-500',
  'bg-sky-500',
  'bg-blue-500',
  'bg-violet-500',
  'bg-fuchsia-500',
];

/** A round avatar: the uploaded image (loaded as a blob) or coloured initials, plus an online dot. */
@Component({
  selector: 'app-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    class: 'relative inline-flex shrink-0',
    '[style.width.px]': 'size()',
    '[style.height.px]': 'size()',
  },
  template: `
    @if (image.value(); as src) {
      <img [src]="src" alt="" class="size-full rounded-full object-cover" />
    } @else {
      <span
        aria-hidden="true"
        class="flex size-full items-center justify-center rounded-full font-semibold text-white select-none"
        [class]="color()"
        [style.font-size.px]="size() * 0.4"
        >{{ initials() }}</span
      >
    }
    @if (online()) {
      <span
        class="absolute right-0 bottom-0 block rounded-full bg-emerald-500 ring-2 ring-surface"
        [style.width.px]="dot()"
        [style.height.px]="dot()"
        title="Online"
      ></span>
    }
  `,
})
export class Avatar {
  private readonly blobs = inject(BlobUrlService);

  readonly kind = input<'user' | 'chat'>('user');
  readonly id = input<string | null | undefined>(null);
  readonly name = input('');
  readonly hasAvatar = input(false);
  readonly size = input(40);
  readonly online = input(false);

  protected readonly dot = computed(() => Math.max(8, Math.round(this.size() * 0.28)));

  protected readonly initials = computed(() => {
    const words = this.name().trim().split(/\s+/).filter(Boolean);
    const letters = words.length > 1 ? words[0][0] + words[1][0] : (words[0] ?? '?').slice(0, 2);
    return letters.toUpperCase();
  });

  protected readonly color = computed(() => {
    const key = this.id() ?? this.name();
    let hash = 0;
    for (let i = 0; i < key.length; i++) {
      hash = (hash * 31 + key.charCodeAt(i)) | 0;
    }
    return COLORS[Math.abs(hash) % COLORS.length];
  });

  protected readonly image = resource({
    params: () => {
      const id = this.id();
      if (!id || !this.hasAvatar()) {
        return undefined;
      }
      const url = avatarUrl[this.kind()](id);
      return { url, version: this.blobs.version(url) };
    },
    loader: ({ params }) => this.blobs.get(params.url),
  });
}
