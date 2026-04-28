import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { CartService } from '../../core/cart.service';
import { CartItem, Recommendation } from '../../core/models';
import { OrderService } from '../../core/order.service';
import { ProductService } from '../../core/product.service';

@Component({
  selector: 'app-cart',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './cart.component.html',
  styleUrl: './cart.component.css'
})
export class CartComponent implements OnInit {
  private readonly cart = inject(CartService);
  private readonly auth = inject(AuthService);
  private readonly products = inject(ProductService);
  private readonly orders = inject(OrderService);
  private readonly fb = inject(FormBuilder);

  readonly items$ = this.cart.items$;
  recommendations: Recommendation[] = [];
  message = '';
  error = '';
  placingOrder = false;

  readonly checkoutForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    shippingAddress: ['', [Validators.required, Validators.minLength(8)]]
  });

  ngOnInit(): void {
    const user = this.auth.currentUser;
    if (user) {
      this.checkoutForm.patchValue({ fullName: user.fullName, email: user.email });
    }

    this.items$.subscribe((items) => this.loadRecommendations(items));
  }

  setQuantity(item: CartItem, event: Event): void {
    const input = event.target as HTMLInputElement;
    this.cart.setQuantity(item.id, Number(input.value));
  }

  remove(id: number): void {
    this.cart.remove(id);
  }

  total(): number {
    return this.cart.total();
  }

  placeOrder(): void {
    if (this.checkoutForm.invalid || this.cart.items.length === 0) {
      this.checkoutForm.markAllAsTouched();
      return;
    }

    const user = this.auth.currentUser;
    const { fullName, email, shippingAddress } = this.checkoutForm.getRawValue();
    this.placingOrder = true;
    this.error = '';
    this.message = '';

    this.orders.createOrder({
      userId: user?.id ?? 0,
      fullName,
      email,
      shippingAddress,
      items: this.cart.items.map((item) => ({
        productId: item.id,
        productName: item.name,
        quantity: item.quantity,
        unitPrice: item.price
      }))
    }).subscribe({
      next: (order) => {
        this.message = `Order #${order.id} placed successfully.`;
        this.cart.clear();
        this.placingOrder = false;
      },
      error: (response) => {
        this.error = response.error?.message ?? 'Order could not be placed.';
        this.placingOrder = false;
      }
    });
  }

  addRecommendation(rec: Recommendation): void {
    this.products.getProduct(rec.id).subscribe((product) => this.cart.add(product));
  }

  private loadRecommendations(items: CartItem[]): void {
    if (items.length === 0) {
      this.recommendations = [];
      return;
    }

    const focusProduct = items[0];
    this.products.getRecommendations(focusProduct.id, items.map((item) => item.id), 4).subscribe({
      next: (recs) => this.recommendations = recs,
      error: () => this.recommendations = []
    });
  }
}
