import { Component } from '@angular/core';
import { ShellComponent } from './layout/shell.component';
import { ToastOutletComponent } from './layout/toast-outlet.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [ShellComponent, ToastOutletComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
}
