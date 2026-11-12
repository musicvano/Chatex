import { HttpClient } from '@angular/common/http';
import { Injectable, effect, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../auth/auth.service';

/**
 * Avatars and attachments need the bearer token, so `<img src="/api/...">` can't load them.
 * This fetches them with HttpClient (through the auth interceptor) and hands out object URLs,
 * cached per URL until invalidated.
 */
@Injectable({ providedIn: 'root' })
export class BlobUrlService {
  private readonly http = inject(HttpClient);
  private readonly cache = new Map<string, Promise<string | null>>();
  /** Bumped when an avatar changes, so components re-request it. */
  private readonly versions = signal<Readonly<Record<string, number>>>({});

  constructor() {
    const auth = inject(AuthService);
    effect(() => {
      auth.sessionEpoch();
      this.clear();
    });
  }

  /** An object URL for the resource, or null if it doesn't exist. */
  get(url: string): Promise<string | null> {
    let entry = this.cache.get(url);
    if (!entry) {
      entry = firstValueFrom(this.http.get(url, { responseType: 'blob' })).then(
        (blob) => URL.createObjectURL(blob),
        () => {
          this.cache.delete(url);
          return null;
        },
      );
      this.cache.set(url, entry);
    }
    return entry;
  }

  /** Version counter for a URL; read it in a computed/resource to reload after `invalidate`. */
  version(url: string): number {
    return this.versions()[url] ?? 0;
  }

  /** Forget a cached resource (e.g. an avatar that was replaced). */
  invalidate(url: string): void {
    const entry = this.cache.get(url);
    this.cache.delete(url);
    void entry?.then((objectUrl) => objectUrl && URL.revokeObjectURL(objectUrl));
    this.versions.update((v) => ({ ...v, [url]: (v[url] ?? 0) + 1 }));
  }

  /** Downloads a protected file and saves it under its original name. */
  async download(url: string, fileName: string): Promise<void> {
    const blob = await firstValueFrom(this.http.get(url, { responseType: 'blob' }));
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(objectUrl), 10_000);
  }

  private clear(): void {
    for (const entry of this.cache.values()) {
      void entry.then((objectUrl) => objectUrl && URL.revokeObjectURL(objectUrl));
    }
    this.cache.clear();
  }
}
