import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { ChatbotRequest, ChatbotResponse } from './models';

const API_URL = 'http://localhost:5148/api';

@Injectable({ providedIn: 'root' })
export class ChatbotService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  ask(request: ChatbotRequest): Observable<ChatbotResponse> {
    const token = this.auth.token;
    const headers = token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : undefined;
    return this.http.post<ChatbotResponse>(`${API_URL}/chatbot/query`, request, { headers });
  }
}
