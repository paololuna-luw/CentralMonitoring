import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  SnmpMetric,
  SnmpTarget,
  SnmpTargetCreate,
  SnmpTargetUpdate,
  SnmpTargetsService
} from '../../services/snmp-targets.service';
import { Host, HostsService } from '../../services/hosts.service';
import { UiToastService } from '../../services/ui-toast.service';

@Component({
  selector: 'app-targets',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './targets.component.html'
})
export class TargetsComponent implements OnInit, OnDestroy {
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  ipFilter = '';
  hostIdFilter = '';
  loading = signal(false);
  targets = signal<SnmpTarget[]>([]);
  hosts = signal<Host[]>([]);
  selected = signal<SnmpTarget | null>(null);
  catalog = signal<SnmpMetric[]>([]);
  selectedMetrics = signal<SnmpMetric[]>([]);
  saving = signal(false);
  deleting = signal(false);
  confirmDeleteOpen = signal(false);
  error = signal<string | null>(null);
  creating = signal(false);

  createModel: SnmpTargetCreate = {
    hostId: '',
    name: '',
    ipAddress: '',
    version: 'v2c',
    community: '',
    profile: '',
    tags: '',
    enabled: true,
    metrics: []
  };

  edit: SnmpTargetUpdate = {
    name: '',
    community: '',
    profile: '',
    tags: '',
    enabled: true,
    metrics: []
  };

  constructor(
    private svc: SnmpTargetsService,
    private hostsSvc: HostsService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
    this.load();
    this.loadCatalog();
    this.loadHosts();
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
    this.svc.list(this.ipFilter || undefined).subscribe({
      next: data => this.targets.set(this.filterByHostId(data)),
      error: () => this.error.set('No se pudieron cargar los targets'),
      complete: () => this.loading.set(false)
    });
  }

  private filterByHostId(rows: SnmpTarget[]): SnmpTarget[] {
    const hostId = this.hostIdFilter.trim().toLowerCase();
    if (!hostId) return rows;
    return rows.filter(r => (r.hostId || '').toLowerCase().includes(hostId));
  }

  hostIdForTarget(t: SnmpTarget): string {
    return t.hostId || '-';
  }

  hostNameForTarget(t: SnmpTarget): string {
    return t.hostName || '-';
  }

  private loadHosts() {
    this.hostsSvc.list().subscribe({
      next: rows => this.hosts.set(rows ?? []),
      error: () => {}
    });
  }

  createTarget() {
    const hostId = (this.createModel.hostId || '').trim();
    const ip = (this.createModel.ipAddress || '').trim();
    const version = (this.createModel.version || '').trim();
    const community = (this.createModel.community || '').trim();
    if (!ip && !hostId) {
      this.error.set('Debes indicar IpAddress o seleccionar HostId.');
      return;
    }
    if (!version) {
      this.error.set('Version es requerida.');
      return;
    }
    if (version.toLowerCase() === 'v2c' && !community) {
      this.error.set('Community es requerida para v2c.');
      return;
    }

    this.creating.set(true);
    this.error.set(null);
    const payload: SnmpTargetCreate = {
      ...this.createModel,
      hostId: hostId || undefined,
      name: (this.createModel.name || '').trim() || undefined,
      ipAddress: ip || undefined,
      version,
      community: community || undefined,
      profile: (this.createModel.profile || '').trim() || undefined,
      tags: (this.createModel.tags || '').trim() || undefined
    };

    this.svc.create(payload).subscribe({
      next: created => {
        this.toast.success('Target creado.');
        this.createModel = {
          hostId: '',
          name: '',
          ipAddress: '',
          version: 'v2c',
          community: '',
          profile: '',
          tags: '',
          enabled: true,
          metrics: []
        };
        this.load();
        this.selectTarget(created.id);
      },
      error: () => {
        this.toast.error('No se pudo crear el target.');
        this.error.set('No se pudo crear el target');
      },
      complete: () => this.creating.set(false)
    });
  }

  loadCatalog() {
    this.svc.catalog().subscribe({
      next: data => {
        this.catalog.set(data ?? []);
        const current = this.selected();
        if (current) {
          this.selectedMetrics.set(this.mergeMetrics(current.metrics ?? []));
        }
      },
      error: () => {}
    });
  }

  selectTarget(id: string) {
    this.loading.set(true);
    this.error.set(null);
    this.svc.get(id).subscribe({
      next: target => {
        this.selected.set(target);
        this.edit = {
          name: target.name ?? target.profile ?? '',
          community: target.community ?? '',
          profile: target.profile ?? '',
          tags: target.tags ?? '',
          enabled: target.enabled,
          metrics: []
        };
        this.selectedMetrics.set(this.mergeMetrics(target.metrics ?? []));
      },
      error: () => this.error.set('No se pudo cargar el detalle del target'),
      complete: () => this.loading.set(false)
    });
  }

  private mergeMetrics(targetMetrics: SnmpMetric[]): SnmpMetric[] {
    const catalog = this.catalog();
    if (catalog.length === 0) return [...targetMetrics];

    const byKey = new Map(targetMetrics.map(m => [m.key, m]));
    return catalog.map(c => {
      const current = byKey.get(c.key);
      return {
        key: c.key,
        oid: c.oid,
        enabled: current?.enabled ?? c.enabled
      };
    });
  }

  setAllMetrics(enabled: boolean) {
    this.selectedMetrics.set(this.selectedMetrics().map(m => ({ ...m, enabled })));
  }

  toggleMetric(metricKey: string, enabled: boolean) {
    this.selectedMetrics.set(
      this.selectedMetrics().map(m => (m.key === metricKey ? { ...m, enabled } : m))
    );
  }

  saveSelected() {
    const target = this.selected();
    if (!target) return;

    this.saving.set(true);
    this.error.set(null);
    const payload: SnmpTargetUpdate = {
      name: (this.edit.name || '').trim() || undefined,
      community: this.edit.community || undefined,
      profile: this.edit.profile || undefined,
      tags: this.edit.tags || undefined,
      enabled: this.edit.enabled,
      metrics: this.selectedMetrics()
    };

    this.svc.update(target.id, payload).subscribe({
      next: () => {
        this.toast.success('Target actualizado.');
        this.load();
        this.selectTarget(target.id);
      },
      error: () => {
        this.toast.error('No se pudo guardar el target.');
        this.error.set('No se pudo guardar el target');
        this.saving.set(false);
      },
      complete: () => this.saving.set(false)
    });
  }

  askDeleteSelected() {
    if (!this.selected()) return;
    this.confirmDeleteOpen.set(true);
  }

  cancelDelete() {
    this.confirmDeleteOpen.set(false);
  }

  confirmDelete() {
    const target = this.selected();
    if (!target) return;

    this.deleting.set(true);
    this.svc.delete(target.id).subscribe({
      next: () => {
        this.toast.success('Target eliminado.');
        this.selected.set(null);
        this.selectedMetrics.set([]);
        this.confirmDeleteOpen.set(false);
        this.load();
      },
      error: () => {
        this.toast.error('No se pudo eliminar el target.');
        this.error.set('No se pudo eliminar el target');
      },
      complete: () => this.deleting.set(false)
    });
  }

  enableAllServer() {
    const target = this.selected();
    if (!target) return;
    this.svc.enableAll(target.id).subscribe({
      next: () => {
        this.toast.success('Todas las metricas habilitadas.');
        this.selectTarget(target.id);
      },
      error: () => {
        this.toast.error('No se pudo habilitar todo.');
        this.error.set('No se pudo habilitar todo');
      }
    });
  }

  disableAllServer() {
    const target = this.selected();
    if (!target) return;
    this.svc.disableAll(target.id).subscribe({
      next: () => {
        this.toast.success('Todas las metricas deshabilitadas.');
        this.selectTarget(target.id);
      },
      error: () => {
        this.toast.error('No se pudo deshabilitar todo.');
        this.error.set('No se pudo deshabilitar todo');
      }
    });
  }

  status(t: SnmpTarget): 'ok' | 'failing' | 'disabled' {
    if (!t.enabled) return 'disabled';
    if (t.consecutiveFailures && t.consecutiveFailures > 0) return 'failing';
    return 'ok';
  }

  statusLabel(t: SnmpTarget): string {
    const s = this.status(t);
    if (s === 'ok') return 'OK';
    if (s === 'failing') return `Failing (${t.consecutiveFailures || 0})`;
    return 'Disabled';
  }

  statusColor(t: SnmpTarget): string {
    const s = this.status(t);
    if (s === 'ok') return 'var(--ok)';
    if (s === 'failing') return 'var(--warn)';
    return 'var(--muted)';
  }

  private startAutoRefresh() {
    this.refreshTimer = setInterval(() => {
      // Evita golpear el backend durante operaciones de usuario.
      if (this.creating() || this.saving() || this.deleting() || this.loading()) return;
      this.load();
      this.loadHosts();
    }, 8000);
  }
}
