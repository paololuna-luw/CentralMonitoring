import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { UiToastItem, UiToastService } from '../services/ui-toast.service';

@Component({
  selector: 'app-toast-outlet',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="toast-wrap">
      <div *ngFor="let t of toast.toasts()" class="toast-card" [class.ok]="t.type === 'success'" [class.err]="t.type === 'error'" [class.info]="t.type === 'info'">
        <span class="toast-text">{{ t.text }}</span>
        <button class="toast-close" (click)="close(t)">x</button>
      </div>
    </div>
  `
})
export class ToastOutletComponent {
  constructor(public toast: UiToastService) {}

  close(t: UiToastItem) {
    this.toast.remove(t.id);
  }
}
