import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { env } from '../env';

export interface Alert {
  id: string;
  hostId: string;
  metricKey: string;
  triggerValue: number;
  lastTriggerValue: number;
  threshold: number;
  severity: string;
  createdAtUtc: string;
  lastTriggerAtUtc: string;
  occurrences: number;
  isResolved: boolean;
}

@Injectable({ providedIn: 'root' })
export class AlertsService {
  private base = `${env.apiBase}/api/v1/alerts`;

  constructor(private http: HttpClient) {}

  list(resolved?: boolean): Observable<Alert[]> {
    let params = new HttpParams();
    if (resolved !== undefined) params = params.set('resolved', resolved);
    return this.http.get<Alert[]>(this.base, { params });
  }

  resolve(id: string, isResolved: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}/resolve`, { isResolved });
  }
}
