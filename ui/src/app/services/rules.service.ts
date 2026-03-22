import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { env } from '../env';

export interface Rule {
  id: string;
  metricKey: string;
  operator: string;
  threshold: number;
  windowMinutes: number;
  severity: string;
  hostId?: string | null;
  snmpIp?: string | null;
  labelContains?: string | null;
  enabled: boolean;
  createdAtUtc: string;
}

export interface RuleCreate {
  metricKey: string;
  operator: string;
  threshold: number;
  windowMinutes: number;
  severity: string;
  hostId?: string | null;
  snmpIp?: string | null;
  labelContains?: string | null;
  enabled?: boolean;
}

export interface RuleUpdate {
  operator?: string;
  threshold?: number;
  windowMinutes?: number;
  severity?: string;
  hostId?: string | null;
  snmpIp?: string | null;
  labelContains?: string | null;
  enabled?: boolean;
}

@Injectable({ providedIn: 'root' })
export class RulesService {
  private base = `${env.apiBase}/api/v1/rules`;

  constructor(private http: HttpClient) {}

  list(): Observable<Rule[]> {
    return this.http.get<Rule[]>(this.base);
  }

  create(payload: RuleCreate): Observable<Rule> {
    return this.http.post<Rule>(this.base, payload);
  }

  update(id: string, payload: RuleUpdate): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}`, payload);
  }
}
