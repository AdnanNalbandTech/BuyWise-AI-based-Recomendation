import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { AuthService } from './auth.service';
import { UserActivityRequest } from './models';

const API_URL = 'http://localhost:5148/api';

@Injectable({ providedIn: 'root' })
export class ActivityService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  track(productId: number, activityType: UserActivityRequest['activityType'], quantity = 1): void {
    const user = this.auth.currentUser;
    if (!user) {
      return;
    }

    this.http.post(`${API_URL}/user-activities`, {
      userId: user.id,
      productId,
      activityType,
      quantity
    }).subscribe({ error: () => undefined });
  }
}
