import { BOB, ME, message, messageId } from '../../../testing/fakes';
import { buildRows } from './message-list';

const mine = { ...BOB, id: ME.id, displayName: ME.displayName };

describe('buildRows', () => {
  it('adds a separator for each day', () => {
    const rows = buildRows(
      [
        message(1, { createdAt: '2026-11-09T10:00:00Z' }),
        message(2, { createdAt: '2026-11-10T10:00:00Z' }),
      ],
      ME.id,
      null,
    );
    expect(rows.map((r) => r.kind)).toEqual(['day', 'message', 'day', 'message']);
  });

  it('groups consecutive messages from one sender', () => {
    const rows = buildRows(
      [
        message(1, { createdAt: '2026-11-10T10:00:00Z' }),
        message(2, { createdAt: '2026-11-10T10:01:00Z' }),
        message(3, { createdAt: '2026-11-10T10:02:00Z', sender: mine }),
        message(4, { createdAt: '2026-11-10T10:30:00Z', sender: mine }),
      ],
      ME.id,
      null,
    ).filter((r) => r.kind === 'message');

    expect(rows.map((r) => [r.first, r.last])).toEqual([
      [true, false],
      [false, true],
      [true, true],
      [true, true],
    ]);
    expect(rows.map((r) => r.mine)).toEqual([false, false, true, true]);
  });

  it('links replies and marks own messages seen up to the peer read position', () => {
    const rows = buildRows(
      [
        message(1, { sender: mine }),
        message(2, { sender: mine, replyToMessageId: messageId(1) }),
        message(3, { sender: mine, replyToMessageId: messageId(99) }),
      ],
      ME.id,
      messageId(2),
    ).filter((r) => r.kind === 'message');

    expect(rows.map((r) => r.seen)).toEqual([true, true, false]);
    expect(rows[0].reply).toBeUndefined();
    expect(rows[1].reply?.id).toBe(messageId(1));
    expect(rows[2].reply).toBeNull();
  });
});
