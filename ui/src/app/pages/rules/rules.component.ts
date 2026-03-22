import { Component, OnDestroy, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Rule, RuleCreate, RulesService, RuleUpdate } from '../../services/rules.service';
import { UiToastService } from '../../services/ui-toast.service';

@Component({
  selector: 'app-rules',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './rules.component.html'
})
export class RulesComponent implements OnInit, OnDestroy {
  private static readonly allowedOps = new Set(['>', '>=', '<', '<=', '==', '!=']);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  rules = signal<Rule[]>([]);
  loading = signal(false);
  saving = signal(false);
  createError = signal<string | null>(null);
  error = signal<string | null>(null);
  drafts: Record<string, RuleUpdate> = {};
  originalDrafts: Record<string, RuleUpdate> = {};

  newRule: RuleCreate = {
    metricKey: '',
    operator: '>',
    threshold: 0,
    windowMinutes: 2,
    severity: 'Warning',
    enabled: true
  };

  constructor(private svc: RulesService, private toast: UiToastService) {}

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
    this.svc.list().subscribe({
      next: res => {
        this.rules.set(res);
        this.drafts = {};
        this.originalDrafts = {};
        for (const rule of res) {
          const draft = {
            operator: rule.operator,
            threshold: rule.threshold,
            windowMinutes: rule.windowMinutes,
            severity: rule.severity,
            snmpIp: rule.snmpIp ?? '',
            enabled: rule.enabled
          };
          this.drafts[rule.id] = { ...draft };
          this.originalDrafts[rule.id] = { ...draft };
        }
      },
      error: () => this.error.set('No se pudieron cargar las reglas'),
      complete: () => this.loading.set(false)
    });
  }

  create() {
    this.createError.set(null);
    const metricKey = this.newRule.metricKey.trim();
    const severity = (this.newRule.severity || '').trim();

    if (!metricKey) {
      this.createError.set('metricKey es requerido.');
      return;
    }
    if (!RulesComponent.allowedOps.has(this.newRule.operator)) {
      this.createError.set('Operator invalido.');
      return;
    }
    if (!this.newRule.windowMinutes || this.newRule.windowMinutes <= 0) {
      this.createError.set('windowMinutes debe ser mayor a 0.');
      return;
    }
    if (!severity) {
      this.createError.set('severity es requerida.');
      return;
    }

    const payload: RuleCreate = {
      ...this.newRule,
      metricKey,
      severity
    };

    this.svc.create(payload).subscribe({
      next: () => {
        this.toast.success('Regla creada.');
        this.newRule = {
          metricKey: '',
          operator: '>',
          threshold: 0,
          windowMinutes: 2,
          severity: 'Warning',
          enabled: true
        };
        this.load();
      },
      error: () => this.toast.error('No se pudo crear la regla.')
    });
  }

  saveRule(rule: Rule) {
    const draft = this.drafts[rule.id];
    if (!draft) return;
    const validation = this.getDraftValidation(rule.id);
    if (validation) return;
    if (!this.isDraftDirty(rule.id)) return;

    this.saving.set(true);
    this.svc.update(rule.id, draft).subscribe({
      next: () => {
        this.toast.success('Regla actualizada.');
        this.load();
      },
      error: () => {
        this.toast.error('No se pudo actualizar la regla.');
        this.error.set('No se pudo actualizar la regla');
        this.saving.set(false);
      },
      complete: () => this.saving.set(false)
    });
  }

  cancelRuleChanges(ruleId: string) {
    const original = this.originalDrafts[ruleId];
    if (!original) return;
    this.drafts[ruleId] = { ...original };
  }

  isDraftDirty(ruleId: string): boolean {
    const draft = this.drafts[ruleId];
    const original = this.originalDrafts[ruleId];
    if (!draft || !original) return false;
    return JSON.stringify(draft) !== JSON.stringify(original);
  }

  getDraftValidation(ruleId: string): string | null {
    const draft = this.drafts[ruleId];
    if (!draft) return 'Draft no encontrado.';

    const op = (draft.operator || '').trim();
    const severity = (draft.severity || '').trim();
    const windowMinutes = Number(draft.windowMinutes);

    if (!RulesComponent.allowedOps.has(op)) return 'Operator invalido.';
    if (!Number.isFinite(windowMinutes) || windowMinutes <= 0) return 'windowMinutes debe ser > 0.';
    if (!severity) return 'severity es requerida.';
    return null;
  }

  private hasDirtyDrafts(): boolean {
    return Object.keys(this.drafts).some(id => this.isDraftDirty(id));
  }

  private startAutoRefresh() {
    this.refreshTimer = setInterval(() => {
      if (this.loading() || this.saving()) return;
      if (this.hasDirtyDrafts()) return;
      this.load();
    }, 10000);
  }
}
