import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'relativeTime', standalone: true, pure: true })
export class RelativeTimePipe implements PipeTransform {
  transform(unixSeconds: number): string {
    const elapsed = Math.floor(Date.now() / 1000) - unixSeconds;
    if (elapsed < 60) return 'just now';
    if (elapsed < 3600) return `${Math.floor(elapsed / 60)}m ago`;
    if (elapsed < 86400) return `${Math.floor(elapsed / 3600)}h ago`;
    if (elapsed < 604800) return `${Math.floor(elapsed / 86400)}d ago`;
    return `${Math.floor(elapsed / 604800)}w ago`;
  }
}
