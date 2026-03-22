import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AlertsService, Alert } from '../../services/alerts.service';
import { UiToastService } from '../../services/ui-toast.service';
import { Host, HostsService } from '../../services/hosts.service';

interface DeviceAlertSummary {
  deviceKey: string;
  hostId: string;
  hostName: string;
  hostIp: string;
  maxSeverity: string;
  alertCount: number;
  latestAtUtc: string;
}

@Component({
  selector: 'app-alerts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './alerts.component.html'
})
export class AlertsComponent implements OnInit, OnDestroy {
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  activeAlerts = signal<Alert[]>([]);
  hosts = signal<Host[]>([]);
  hostMap = signal<Record<string, Host>>({});
  selectedDevice = signal<string | null>(null);
  loading = signal(false);
  resolvingIds = signal<Set<string>>(new Set());
  error = signal<string | null>(null);
  severityFilter = 'all';
  fromDate = '';
  toDate = '';
  searchText = '';

  constructor(
    private svc: AlertsService,
    private hostsSvc: HostsService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.reloadHosts();
    this.load();
    this.startAutoRefresh();
  }

  ngOnDestroy(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  load() {
    this.loading.set(true);
    this.error.set(null);

    this.svc.list(false).subscribe({
      next: active => {
        this.activeAlerts.set(active);
      },
      error: () => this.error.set('No se pudieron cargar las alertas'),
      complete: () => this.loading.set(false)
    });
  }

  resolve(id: string) {
    const next = new Set(this.resolvingIds());
    next.add(id);
    this.resolvingIds.set(next);

    this.svc.resolve(id, true).subscribe({
      next: () => {
        this.toast.success('Alerta resuelta correctamente.');
        this.load();
      },
      error: () => this.toast.error('No se pudo resolver la alerta.'),
      complete: () => {
        const updated = new Set(this.resolvingIds());
        updated.delete(id);
        this.resolvingIds.set(updated);
      }
    });
  }

  filteredActiveAlerts(): Alert[] {
    return this.applyFilters(this.activeAlerts());
  }

  groupedByDevice(): DeviceAlertSummary[] {
    const map = new Map<string, DeviceAlertSummary>();
    const hosts = this.hostMap();

    for (const alert of this.filteredActiveAlerts()) {
      const host = hosts[alert.hostId];
      const current = map.get(alert.hostId);

      if (!current) {
        map.set(alert.hostId, {
          deviceKey: alert.hostId,
          hostId: alert.hostId,
          hostName: host?.name || 'Sin nombre',
          hostIp: host?.ipAddress || '-',
          maxSeverity: alert.severity,
          alertCount: 1,
          latestAtUtc: alert.lastTriggerAtUtc || alert.createdAtUtc
        });
        continue;
      }

      current.alertCount += 1;
      current.maxSeverity = this.higherSeverity(current.maxSeverity, alert.severity);
      const latestAt = alert.lastTriggerAtUtc || alert.createdAtUtc;
      if (new Date(latestAt).getTime() > new Date(current.latestAtUtc).getTime()) {
        current.latestAtUtc = latestAt;
      }
    }

    return [...map.values()].sort((a, b) => {
      const bySeverity = this.severityRank(b.maxSeverity) - this.severityRank(a.maxSeverity);
      if (bySeverity !== 0) return bySeverity;
      return new Date(b.latestAtUtc).getTime() - new Date(a.latestAtUtc).getTime();
    });
  }

  selectDevice(hostId: string) {
    this.selectedDevice.set(hostId);
  }

  clearDeviceFilter() {
    this.selectedDevice.set(null);
  }

  severityColor(severity: string): string {
    const normalized = (severity || '').toLowerCase();
    if (normalized.includes('critical')) return 'var(--danger)';
    if (normalized.includes('warn')) return 'var(--warn)';
    return 'var(--ok)';
  }

  hostName(hostId: string): string {
    return this.hostMap()[hostId]?.name || 'Sin nombre';
  }

  hostIp(hostId: string): string {
    return this.hostMap()[hostId]?.ipAddress || '-';
  }

  private applyFilters(alerts: Alert[]): Alert[] {
    const query = this.searchText.trim().toLowerCase();
    const from = this.fromDate ? new Date(`${this.fromDate}T00:00:00`) : null;
    const to = this.toDate ? new Date(`${this.toDate}T23:59:59`) : null;
    const hosts = this.hostMap();

    return alerts.filter(alert => {
      const severityOk =
        this.severityFilter === 'all' ||
        (alert.severity || '').toLowerCase() === this.severityFilter.toLowerCase();

      const primaryDate = new Date(alert.lastTriggerAtUtc || alert.createdAtUtc);
      const fromOk = !from || primaryDate >= from;
      const toOk = !to || primaryDate <= to;

      const host = hosts[alert.hostId];
      const text = `${alert.metricKey} ${alert.hostId} ${host?.name || ''} ${host?.ipAddress || ''}`.toLowerCase();
      const queryOk = !query || text.includes(query);

      const deviceOk = !this.selectedDevice() || alert.hostId === this.selectedDevice();
      return severityOk && fromOk && toOk && queryOk && deviceOk;
    });
  }

  private reloadHosts() {
    this.hostsSvc.list().subscribe({
      next: rows => {
        this.hosts.set(rows);
        const map: Record<string, Host> = {};
        for (const host of rows) {
          map[host.id] = host;
        }
        this.hostMap.set(map);
      },
      error: () => {}
    });
  }

  private startAutoRefresh() {
    this.refreshTimer = setInterval(() => {
      if (this.loading()) return;
      if (this.resolvingIds().size > 0) return;
      this.load();
      this.reloadHosts();
    }, 8000);
  }

  private higherSeverity(left: string, right: string): string {
    return this.severityRank(right) > this.severityRank(left) ? right : left;
  }

  private severityRank(severity: string): number {
    const normalized = (severity || '').toLowerCase();
    if (normalized.includes('critical')) return 3;
    if (normalized.includes('warn')) return 2;
    return 1;
  }
}
