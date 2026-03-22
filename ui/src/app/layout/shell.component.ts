import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

interface NavItem {
  label: string;
  path: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  darkMode = signal(true);

  nav: NavItem[] = [
    { label: 'Resumen', path: '/home' },
    { label: 'Targets', path: '/targets' },
    { label: 'Agentes', path: '/hosts' },
    { label: 'Metricas', path: '/metrics' },
    { label: 'Alertas', path: '/alerts' },
    { label: 'Reglas', path: '/rules' }
  ];

  constructor() {
    const stored = localStorage.getItem('cm-theme');
    const useDark = stored ? stored === 'dark' : true;
    this.darkMode.set(useDark);
    this.applyTheme();
  }

  toggleTheme() {
    this.darkMode.set(!this.darkMode());
    this.applyTheme();
  }

  private applyTheme() {
    const root = document.documentElement;
    root.classList.toggle('dark', this.darkMode());
    localStorage.setItem('cm-theme', this.darkMode() ? 'dark' : 'light');
  }
}
