import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '@env/environment';

/**
 * Base API service providing the configured base URL from the current environment.
 * Feature-specific services should extend or compose this service.
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  protected readonly baseUrl = environment.apiBaseUrl;

  constructor(protected readonly http: HttpClient) {}

  protected get<T>(path: string): Observable<T> {
    return this.http.get<T>(`${this.baseUrl}${path}`);
  }

  protected post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${this.baseUrl}${path}`, body);
  }

  protected patch<T>(path: string, body: unknown): Observable<T> {
    return this.http.patch<T>(`${this.baseUrl}${path}`, body);
  }

  protected delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${this.baseUrl}${path}`);
  }
}
