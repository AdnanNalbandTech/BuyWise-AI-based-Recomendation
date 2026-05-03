import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';
import { CartItem, CartSummary, Product } from './models';

const CART_KEY = 'buywise_cart';
const API_URL = 'http://localhost:5148/api';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly itemSubject = new BehaviorSubject<CartItem[]>(this.readItems());

  readonly items$ = this.itemSubject.asObservable();

  get items(): CartItem[] {
    return this.itemSubject.value;
  }

  add(product: Product): void {
    this.addItem(product, true);
  }

  addLocal(product: Product): void {
    this.addItem(product, false);
  }

  private addItem(product: Product, syncWithServer: boolean): void {
    const existing = this.items.find((item) => item.id === product.id);
    const next = existing
      ? this.items.map((item) => item.id === product.id ? { ...item, quantity: item.quantity + 1 } : item)
      : [...this.items, { ...product, quantity: 1 }];

    this.save(next);
    if (syncWithServer) {
      this.syncAdd(product.id, 1);
    }
  }

  setQuantity(productId: number, quantity: number): void {
    const next = this.items
      .map((item) => item.id === productId ? { ...item, quantity: Math.max(1, quantity) } : item)
      .filter((item) => item.quantity > 0);
    this.save(next);
    this.syncQuantity(productId, quantity);
  }

  remove(productId: number): void {
    this.save(this.items.filter((item) => item.id !== productId));
    const user = this.auth.currentUser;
    if (user) {
      this.http.delete<CartSummary>(`${API_URL}/cart/${user.id}/items/${productId}`).subscribe({ error: () => undefined });
    }
  }

  clear(): void {
    this.save([]);
    const user = this.auth.currentUser;
    if (user) {
      this.http.delete(`${API_URL}/cart/${user.id}`).subscribe({ error: () => undefined });
    }
  }

  total(): number {
    return this.items.reduce((sum, item) => sum + item.price * item.quantity, 0);
  }

  productIds(): number[] {
    return this.items.map((item) => item.id);
  }

  private save(items: CartItem[]): void {
    localStorage.setItem(CART_KEY, JSON.stringify(items));
    this.itemSubject.next(items);
  }

  private readItems(): CartItem[] {
    const stored = localStorage.getItem(CART_KEY);
    if (!stored) {
      return [];
    }

    try {
      return JSON.parse(stored) as CartItem[];
    } catch {
      localStorage.removeItem(CART_KEY);
      return [];
    }
  }

  private syncAdd(productId: number, quantity: number): void {
    const user = this.auth.currentUser;
    if (!user) {
      return;
    }

    this.http.post<CartSummary>(`${API_URL}/cart`, { userId: user.id, productId, quantity }).subscribe({ error: () => undefined });
  }

  private syncQuantity(productId: number, quantity: number): void {
    const user = this.auth.currentUser;
    if (!user) {
      return;
    }

    this.http.put<CartSummary>(`${API_URL}/cart/${productId}`, { userId: user.id, quantity }).subscribe({ error: () => undefined });
  }
}
