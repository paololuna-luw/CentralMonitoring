import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { Host, HostCreate, HostUpdate, HostsService } from '../../services/hosts.service';
import { MetricsService } from '../../services/metrics.service';
import { UiToastService } from '../../services/ui-toast.service';

type HostState = 'Created' | 'Connected' | 'Confirmed';

interface HostStatus {
  state: HostState;
  lastMetricUtc?: string;
}

@Component({
  selector: 'app-hosts',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './hosts.component.html'
})
export class HostsComponent implements OnInit, OnDestroy {
  private refreshTimer: ReturnType<typeof setInterval> | null = null;
  loading = signal(false);
  creating = signal(false);
  deletingHostId = signal<string | null>(null);
  confirmDeleteHost = signal<Host | null>(null);
  savingHostId = signal<string | null>(null);
  actionsHostId = signal<string | null>(null);
  editingHostId = signal<string | null>(null);
  hosts = signal<Host[]>([]);
  hostStatuses = signal<Record<string, HostStatus>>({});
  error = signal<string | null>(null);

  recentMinutes = 5;
  form: HostCreate = {
    name: '',
    ipAddress: '',
    type: 1,
    tags: '',
    isActive: true
  };

  editForm: HostUpdate = {
    name: '',
    ipAddress: '',
    type: 1,
    tags: '',
    isActive: true
  };

  constructor(
    private hostsSvc: HostsService,
    private metricsSvc: MetricsService,
    private toast: UiToastService
  ) {}

  ngOnInit(): void {
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
    this.hostsSvc.list().subscribe({
      next: rows => {
        this.hosts.set(rows);
        this.loadStatuses(rows);
      },
      error: () => {
        this.error.set('No se pudieron cargar los hosts.');
        this.loading.set(false);
      }
    });
  }

  createHost() {
    const name = this.form.name.trim();
    const ip = this.form.ipAddress.trim();
    if (!name || !ip) {
      this.error.set('Name e IpAddress son requeridos.');
      return;
    }

    this.creating.set(true);
    this.error.set(null);
    const payload: HostCreate = {
      ...this.form,
      name,
      ipAddress: ip,
      tags: (this.form.tags || '').trim() || undefined
    };
    this.hostsSvc.create(payload).subscribe({
      next: () => {
        this.toast.success('Host creado. Usa su HostId en Programa 2.');
        this.form = { name: '', ipAddress: '', type: 1, tags: '', isActive: true };
        this.load();
      },
      error: () => {
        this.toast.error('No se pudo crear el host.');
        this.error.set('No se pudo crear el host.');
      },
      complete: () => this.creating.set(false)
    });
  }

  refreshStatuses() {
    this.loadStatuses(this.hosts());
  }

  private loadStatuses(rows: Host[]) {
    if (rows.length === 0) {
      this.hostStatuses.set({});
      this.loading.set(false);
      return;
    }

    const calls = rows.map(h =>
      forkJoin({
        any: this.metricsSvc.latest({ hostId: h.id }).pipe(catchError(() => of([]))),
        recent: this.metricsSvc.latest({ hostId: h.id, freshMinutes: this.recentMinutes }).pipe(catchError(() => of([])))
      }).pipe(
        map(({ any, recent }) => {
          const hasAny = any.length > 0;
          const hasRecent = recent.length > 0;
          const lastMetricUtc = hasAny
            ? any
                .map(m => m.timestampUtc)
                .sort((a, b) => new Date(b).getTime() - new Date(a).getTime())[0]
            : undefined;

          const state: HostState = hasRecent ? 'Confirmed' : hasAny ? 'Connected' : 'Created';
          return { hostId: h.id, state, lastMetricUtc };
        })
      )
    );

    forkJoin(calls).subscribe({
      next: values => {
        const map: Record<string, HostStatus> = {};
        for (const v of values) {
          map[v.hostId] = { state: v.state, lastMetricUtc: v.lastMetricUtc };
        }
        this.hostStatuses.set(map);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  stateColor(id: string): string {
    const st = this.hostStatuses()[id]?.state || 'Created';
    if (st === 'Confirmed') return 'var(--ok)';
    if (st === 'Connected') return 'var(--warn)';
    return 'var(--muted)';
  }

  stateLabel(id: string): string {
    return this.hostStatuses()[id]?.state || 'Created';
  }

  lastMetric(id: string): string {
    return this.hostStatuses()[id]?.lastMetricUtc || '';
  }

  async copyHostId(id: string) {
    try {
      await navigator.clipboard.writeText(id);
      this.toast.success('HostId copiado.');
    } catch {
      this.toast.error('No se pudo copiar HostId.');
    }
  }

  toggleActions(id: string) {
    this.actionsHostId.set(this.actionsHostId() === id ? null : id);
  }

  closeActions() {
    this.actionsHostId.set(null);
  }

  startEdit(host: Host) {
    this.closeActions();
    this.editingHostId.set(host.id);
    this.editForm = {
      name: host.name,
      ipAddress: host.ipAddress,
      type: this.hostTypeToNumber(host.type),
      tags: host.tags || '',
      isActive: host.isActive
    };
  }

  cancelEdit() {
    this.editingHostId.set(null);
    this.savingHostId.set(null);
  }

  saveEdit(host: Host) {
    const name = (this.editForm.name || '').trim();
    const ip = (this.editForm.ipAddress || '').trim();
    if (!name || !ip) {
      this.toast.error('Name e IpAddress son requeridos para editar.');
      return;
    }

    this.savingHostId.set(host.id);
    const payload: HostUpdate = {
      name,
      ipAddress: ip,
      type: this.editForm.type || 1,
      tags: (this.editForm.tags || '').trim() || undefined,
      isActive: this.editForm.isActive
    };

    this.hostsSvc.update(host.id, payload).subscribe({
      next: () => {
        this.toast.success('Host actualizado.');
        this.cancelEdit();
        this.load();
      },
      error: () => {
        this.toast.error('No se pudo actualizar el host.');
        this.savingHostId.set(null);
      }
    });
  }

  askDeleteHost(host: Host) {
    this.closeActions();
    this.confirmDeleteHost.set(host);
  }

  cancelDeleteHost() {
    if (this.deletingHostId()) return;
    this.confirmDeleteHost.set(null);
  }

  deleteHostConfirmed() {
    const host = this.confirmDeleteHost();
    if (!host) return;
    if (this.deletingHostId()) return;

    this.deletingHostId.set(host.id);
    this.hostsSvc.delete(host.id).subscribe({
      next: () => {
        this.toast.success('Host eliminado.');
        this.confirmDeleteHost.set(null);
        this.load();
      },
      error: () => {
        this.toast.error('No se pudo eliminar el host.');
      },
      complete: () => this.deletingHostId.set(null)
    });
  }

  private hostTypeToNumber(type: string): number {
    const normalized = (type || '').toLowerCase();
    if (normalized === 'linux') return 2;
    if (normalized === 'network') return 3;
    return 1;
  }

  private startAutoRefresh() {
    this.refreshTimer = setInterval(() => {
      if (this.creating() || this.savingHostId() || this.deletingHostId()) return;
      if (this.editingHostId() || this.confirmDeleteHost()) return;
      this.load();
    }, 9000);
  }
}
