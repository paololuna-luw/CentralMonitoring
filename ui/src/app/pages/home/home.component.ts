import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AlertsService, Alert } from '../../services/alerts.service';
import { RulesService, Rule } from '../../services/rules.service';
import { SnmpTarget, SnmpTargetsService } from '../../services/snmp-targets.service';
import { UiToastService } from '../../services/ui-toast.service';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './home.component.html'
})
export class HomeComponent implements OnInit {
  loading = signal(false);
  alerts = signal<Alert[]>([]);
  rules = signal<Rule[]>([]);
  targets = signal<SnmpTarget[]>([]);

  constructor(
    private alertsSvc: AlertsService,
    private rulesSvc: RulesService,
    private targetsSvc: SnmpTargetsService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  refresh() {
    this.loading.set(true);

    this.targetsSvc.list().subscribe({
      next: res => this.targets.set(res),
      error: () => this.toast.error('No se pudieron cargar targets para el resumen.')
    });

    this.alertsSvc.list(false).subscribe({
      next: res => this.alerts.set(res),
      error: () => this.toast.error('No se pudieron cargar alertas para el resumen.')
    });

    this.rulesSvc.list().subscribe({
      next: res => this.rules.set(res),
      error: () => this.toast.error('No se pudieron cargar reglas para el resumen.'),
      complete: () => this.loading.set(false)
    });
  }

  enabledTargets(): number {
    return this.targets().filter(t => t.enabled).length;
  }

  failingTargets(): number {
    return this.targets().filter(t => t.enabled && (t.consecutiveFailures || 0) > 0).length;
  }

  criticalAlerts(): number {
    return this.alerts().filter(a => (a.severity || '').toLowerCase().includes('critical')).length;
  }

  activeRules(): number {
    return this.rules().filter(r => r.enabled).length;
  }
}
