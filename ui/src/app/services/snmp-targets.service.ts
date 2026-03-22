import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { env } from '../env';

export interface SnmpMetric {
  key: string;
  oid: string;
  enabled: boolean;
}

export interface SnmpTarget {
  id: string;
  name?: string | null;
  ipAddress: string;
  version: string;
  hostId?: string | null;
  hostName?: string | null;
  community?: string;
  profile?: string;
  tags?: string;
  enabled: boolean;
  createdAtUtc: string;
  metrics?: SnmpMetric[] | null;
  consecutiveFailures?: number;
  lastSuccessUtc?: string | null;
  lastFailureUtc?: string | null;
}

export interface SnmpTargetCreate {
  hostId?: string;
  name?: string;
  ipAddress?: string;
  version: string;
  community?: string;
  profile?: string;
  tags?: string;
  enabled?: boolean;
  metrics?: SnmpMetric[];
}

export interface SnmpTargetUpdate {
  hostId?: string;
  name?: string;
  community?: string;
  profile?: string;
  tags?: string;
  enabled?: boolean;
  metrics?: SnmpMetric[] | null;
}

@Injectable({ providedIn: 'root' })
export class SnmpTargetsService {
  private base = `${env.apiBase}/api/v1/snmp/targets`;

  constructor(private http: HttpClient) {}

  list(ip?: string): Observable<SnmpTarget[]> {
    let params = new HttpParams();
    if (ip) params = params.set('ip', ip);
    return this.http.get<SnmpTarget[]>(this.base, { params });
  }

  get(id: string): Observable<SnmpTarget> {
    return this.http.get<SnmpTarget>(`${this.base}/${id}`);
  }

  create(payload: SnmpTargetCreate): Observable<SnmpTarget> {
    return this.http.post<SnmpTarget>(this.base, payload);
  }

  update(id: string, payload: SnmpTargetUpdate): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}`, payload);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  enableAll(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/metrics/enableAll`, {});
  }

  disableAll(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/metrics/disableAll`, {});
  }

  catalog(): Observable<SnmpMetric[]> {
    return this.http.get<SnmpMetric[]>(`${env.apiBase}/api/v1/metrics/catalog`);
  }
}
