import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { AuthResponse, PublicUser } from './models';

const API_URL = 'http://localhost:5148/api';
const TOKEN_KEY = 'buywise_token';
const USER_KEY = 'buywise_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly userSubject = new BehaviorSubject<PublicUser | null>(this.readUser());

  readonly currentUser$ = this.userSubject.asObservable();

  login(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_URL}/auth/login`, { email, password }).pipe(
      tap((response) => this.storeAuth(response))
    );
  }

  register(fullName: string, email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_URL}/auth/register`, { fullName, email, password }).pipe(
      tap((response) => this.storeAuth(response))
    );
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.userSubject.next(null);
  }

  get token(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  get currentUser(): PublicUser | null {
    return this.userSubject.value;
  }

  isLoggedIn(): boolean {
    return Boolean(this.token && this.currentUser);
  }

  isAdmin(): boolean {
    return this.currentUser?.role.toLowerCase() === 'admin';
  }

  private storeAuth(response: AuthResponse): void {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    this.userSubject.next(response.user);
  }

  private readUser(): PublicUser | null {
    const stored = localStorage.getItem(USER_KEY);
    if (!stored) {
      return null;
    }

    try {
      return JSON.parse(stored) as PublicUser;
    } catch {
      localStorage.removeItem(USER_KEY);
      return null;
    }
  }
}
