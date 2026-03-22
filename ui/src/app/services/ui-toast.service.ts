import { Injectable, signal } from '@angular/core';

export type UiToastType = 'success' | 'error' | 'info';

export interface UiToastItem {
  id: number;
  type: UiToastType;
  text: string;
}

@Injectable({ providedIn: 'root' })
export class UiToastService {
  private seq = 1;
  toasts = signal<UiToastItem[]>([]);

  success(text: string, ttlMs = 2800) {
    this.push('success', text, ttlMs);
  }

  error(text: string, ttlMs = 3600) {
    this.push('error', text, ttlMs);
  }

  info(text: string, ttlMs = 2400) {
    this.push('info', text, ttlMs);
  }

  remove(id: number) {
    this.toasts.set(this.toasts().filter(t => t.id !== id));
  }

  private push(type: UiToastType, text: string, ttlMs: number) {
    const id = this.seq++;
    this.toasts.set([...this.toasts(), { id, type, text }]);
    setTimeout(() => this.remove(id), ttlMs);
  }
}
