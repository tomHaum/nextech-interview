import { RelativeTimePipe } from './relative-time.pipe';

describe('RelativeTimePipe', () => {
  let pipe: RelativeTimePipe;
  const now = Math.floor(Date.now() / 1000);

  beforeEach(() => {
    pipe = new RelativeTimePipe();
  });

  it('returns "just now" for elapsed < 60s', () => {
    expect(pipe.transform(now - 0)).toBe('just now');
    expect(pipe.transform(now - 59)).toBe('just now');
  });

  it('returns "Xm ago" for elapsed 60s–3599s', () => {
    expect(pipe.transform(now - 60)).toBe('1m ago');
    expect(pipe.transform(now - 3599)).toBe('59m ago');
  });

  it('returns "Xh ago" for elapsed 1h–23h', () => {
    expect(pipe.transform(now - 3600)).toBe('1h ago');
    expect(pipe.transform(now - 86399)).toBe('23h ago');
  });

  it('returns "Xd ago" for elapsed 1d–6d', () => {
    expect(pipe.transform(now - 86400)).toBe('1d ago');
    expect(pipe.transform(now - 604799)).toBe('6d ago');
  });

  it('returns "Xw ago" for elapsed >= 7d', () => {
    expect(pipe.transform(now - 604800)).toBe('1w ago');
    expect(pipe.transform(now - 1209600)).toBe('2w ago');
  });
});
