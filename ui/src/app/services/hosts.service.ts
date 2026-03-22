import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { env } from '../env';

export interface Host {
  id: string;
  name: string;
  ipAddress: string;
  type: string;
  tags?: string | null;
  isActive: boolean;
  createdAtUtc: string;
}

export interface HostCreate {
  name: string;
  ipAddress: string;
  type: number;
  tags?: string;
  isActive?: boolean;
}

export interface HostUpdate {
  name?: string;
  ipAddress?: string;
  type?: number;
  tags?: string;
  isActive?: boolean;
}

@Injectable({ providedIn: 'root' })
export class HostsService {
  private base = `${env.apiBase}/api/v1/hosts`;

  constructor(private http: HttpClient) {}

  list(): Observable<Host[]> {
    return this.http.get<Host[]>(this.base);
  }

  create(payload: HostCreate): Observable<Host> {
    return this.http.post<Host>(this.base, payload);
  }

  update(id: string, payload: HostUpdate): Observable<void> {
    return this.http.patch<void>(`${this.base}/${id}`, payload);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
