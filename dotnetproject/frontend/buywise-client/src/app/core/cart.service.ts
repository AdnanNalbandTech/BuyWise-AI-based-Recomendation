import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CartItem, Product } from './models';

const CART_KEY = 'buywise_cart';

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly itemSubject = new BehaviorSubject<CartItem[]>(this.readItems());

  readonly items$ = this.itemSubject.asObservable();

  get items(): CartItem[] {
    return this.itemSubject.value;
  }

  add(product: Product): void {
    const existing = this.items.find((item) => item.id === product.id);
    const next = existing
      ? this.items.map((item) => item.id === product.id ? { ...item, quantity: item.quantity + 1 } : item)
      : [...this.items, { ...product, quantity: 1 }];

    this.save(next);
  }

  setQuantity(productId: number, quantity: number): void {
    const next = this.items
      .map((item) => item.id === productId ? { ...item, quantity: Math.max(1, quantity) } : item)
      .filter((item) => item.quantity > 0);
    this.save(next);
  }

  remove(productId: number): void {
    this.save(this.items.filter((item) => item.id !== productId));
  }

  clear(): void {
    this.save([]);
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
}
