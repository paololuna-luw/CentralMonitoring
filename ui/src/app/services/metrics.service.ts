import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { env } from '../env';

export interface MetricSample {
  id: string;
  hostId: string;
  metricKey: string;
  timestampUtc: string;
  value: number;
  labelsJson?: string | null;
}

export interface MetricsLatestQuery {
  snmpIp?: string;
  hostId?: string;
  freshMinutes?: number;
}

@Injectable({ providedIn: 'root' })
export class MetricsService {
  private base = `${env.apiBase}/api/v1/metrics`;

  constructor(private http: HttpClient) {}

  latest(query: MetricsLatestQuery): Observable<MetricSample[]> {
    let params = new HttpParams();
    if (query.snmpIp) params = params.set('snmpIp', query.snmpIp);
    if (query.hostId) params = params.set('hostId', query.hostId);
    if (query.freshMinutes) params = params.set('freshMinutes', query.freshMinutes);
    return this.http.get<MetricSample[]>(`${this.base}/latest`, { params });
  }
}
