import { TestBed } from '@angular/core/testing';
import { NEVER } from 'rxjs';
import { fakeRealtime } from '../../../testing/fakes';
import { AttachmentsApi } from '../../core/api/chatex-api';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { ToastService } from '../../core/ui/toast.service';
import { MessagesStore } from '../../state/messages.store';
import { Composer, MAX_ATTACHMENTS, isAllowedFile } from './composer';

describe('Composer', () => {
  let store: { send: ReturnType<typeof vi.fn>; edit: ReturnType<typeof vi.fn> };
  let toast: { error: ReturnType<typeof vi.fn> };
  let upload: ReturnType<typeof vi.fn>;
  let realtime: ReturnType<typeof fakeRealtime>;

  async function create() {
    store = { send: vi.fn(async () => undefined), edit: vi.fn(async () => undefined) };
    toast = { error: vi.fn() };
    upload = vi.fn(() => NEVER);
    realtime = fakeRealtime();
    TestBed.configureTestingModule({
      imports: [Composer],
      providers: [
        { provide: MessagesStore, useValue: store },
        { provide: AttachmentsApi, useValue: { upload } },
        { provide: ToastService, useValue: toast },
        { provide: RealtimeService, useValue: realtime },
      ],
    });
    const fixture = TestBed.createComponent(Composer);
    fixture.componentRef.setInput('chatId', 'chat-1');
    await fixture.whenStable();
    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    return { fixture, textarea };
  }

  function type(textarea: HTMLTextAreaElement, value: string) {
    textarea.value = value;
    textarea.dispatchEvent(new Event('input'));
  }

  function press(textarea: HTMLTextAreaElement, init: KeyboardEventInit) {
    const event = new KeyboardEvent('keydown', { key: 'Enter', cancelable: true, ...init });
    textarea.dispatchEvent(event);
    return event;
  }

  it('sends on Enter and clears the box', async () => {
    const { fixture, textarea } = await create();
    type(textarea, '  hello  ');
    await fixture.whenStable();

    const event = press(textarea, {});
    await fixture.whenStable();

    expect(event.defaultPrevented).toBe(true);
    expect(store.send).toHaveBeenCalledWith('hello', [], null);
    expect(textarea.value).toBe('');
  });

  it('keeps Shift+Enter for a new line', async () => {
    const { fixture, textarea } = await create();
    type(textarea, 'line one');
    await fixture.whenStable();

    const event = press(textarea, { shiftKey: true });

    expect(event.defaultPrevented).toBe(false);
    expect(store.send).not.toHaveBeenCalled();
  });

  it("doesn't send an empty message", async () => {
    const { textarea } = await create();
    type(textarea, '   ');
    press(textarea, {});
    expect(store.send).not.toHaveBeenCalled();
  });

  it('tells the others the user is typing', async () => {
    const { textarea } = await create();
    type(textarea, 'h');
    expect(realtime.sendTyping).toHaveBeenCalledWith('chat-1');
  });

  it(`uploads at most ${MAX_ATTACHMENTS} files per message`, async () => {
    const { fixture } = await create();
    const files = Array.from({ length: MAX_ATTACHMENTS + 2 }, (_, i) => new File(['x'], `f${i}.txt`, { type: 'text/plain' }));

    fixture.componentInstance.addFiles(files);

    expect(upload).toHaveBeenCalledTimes(MAX_ATTACHMENTS);
    expect(toast.error).toHaveBeenCalled();
  });

  it('rejects file types the server does not accept', async () => {
    const { fixture } = await create();
    fixture.componentInstance.addFiles([new File(['x'], 'run.exe', { type: 'application/x-msdownload' })]);
    expect(upload).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalled();
  });
});

describe('isAllowedFile', () => {
  it('accepts allowed types, or allowed extensions when the type is unknown', () => {
    expect(isAllowedFile(new File([''], 'a.png', { type: 'image/png' }))).toBe(true);
    expect(isAllowedFile(new File([''], 'notes.docx', { type: '' }))).toBe(true);
    expect(isAllowedFile(new File([''], 'a.svg', { type: 'image/svg+xml' }))).toBe(false);
  });
});
