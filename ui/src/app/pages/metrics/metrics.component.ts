import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MetricsService, MetricSample } from '../../services/metrics.service';
import { SnmpTarget, SnmpTargetsService } from '../../services/snmp-targets.service';
import { UiToastService } from '../../services/ui-toast.service';
import { Host, HostsService } from '../../services/hosts.service';

@Component({
  selector: 'app-metrics',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './metrics.component.html'
})
export class MetricsComponent implements OnInit, OnDestroy {
  sourceType: 'snmp' | 'agent' = 'snmp';
  snmpIp = '';
  hostId = '';
  freshMinutes = 2;
  autoRefreshSeconds = 0;
  loading = signal(false);
  data = signal<MetricSample[]>([]);
  targets = signal<SnmpTarget[]>([]);
  hosts = signal<Host[]>([]);
  lastLoadedAt = signal<Date | null>(null);
  error = signal<string | null>(null);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  constructor(
    private svc: MetricsService,
    private targetsSvc: SnmpTargetsService,
    private hostsSvc: HostsService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.targetsSvc.list().subscribe({
      next: res => this.targets.set(res),
      error: () => {
        this.toast.error('No se pudieron cargar los targets.');
        this.error.set('No se pudieron cargar los targets');
      }
    });

    this.hostsSvc.list().subscribe({
      next: res => this.hosts.set(res),
      error: () => this.toast.error('No se pudieron cargar hosts/agentes.')
    });
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
  }

  load() {
    if (this.sourceType === 'snmp' && !this.snmpIp) {
      this.toast.info('Selecciona una IP SNMP primero.');
      this.error.set('Selecciona una IP SNMP');
      return;
    }
    if (this.sourceType === 'agent' && !this.hostId) {
      this.toast.info('Selecciona un HostId de agente primero.');
      this.error.set('Selecciona un HostId');
      return;
    }
    this.loading.set(true);
    this.error.set(null);
    this.svc.latest({
      snmpIp: this.sourceType === 'snmp' ? this.snmpIp : undefined,
      hostId: this.sourceType === 'agent' ? this.hostId : undefined,
      freshMinutes: this.freshMinutes
    }).subscribe({
      next: res => {
        this.data.set(res);
        this.lastLoadedAt.set(new Date());
        if (res.length === 0) this.toast.info('No hay metricas en la ventana seleccionada.');
      },
      error: () => {
        this.toast.error('No se pudieron cargar las metricas.');
        this.error.set('No se pudieron cargar las metricas');
      },
      complete: () => this.loading.set(false)
    });
  }

  onTargetChange(ip: string) {
    this.snmpIp = ip || '';
    if (this.sourceType === 'snmp' && this.snmpIp) this.load();
  }

  onHostChange(hostId: string) {
    this.hostId = hostId || '';
    if (this.sourceType === 'agent' && this.hostId) this.load();
  }

  onSourceTypeChange(type: 'snmp' | 'agent') {
    this.sourceType = type;
    this.data.set([]);
    this.error.set(null);
  }

  applyAutoRefresh() {
    this.stopAutoRefresh();
    if (!this.autoRefreshSeconds || this.autoRefreshSeconds <= 0) return;

    this.refreshTimer = setInterval(() => {
      if (!this.loading() && this.snmpIp) this.load();
    }, this.autoRefreshSeconds * 1000);
  }

  stopAutoRefresh() {
    if (!this.refreshTimer) return;
    clearInterval(this.refreshTimer);
    this.refreshTimer = null;
  }

  isFresh(ts: string): boolean {
    const cutoff = Date.now() - this.freshMinutes * 60 * 1000;
    return new Date(ts).getTime() >= cutoff;
  }
}
