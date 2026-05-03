import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ActivityService } from '../../core/activity.service';
import { CartService } from '../../core/cart.service';
import { Product, Recommendation } from '../../core/models';
import { ProductService } from '../../core/product.service';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.css'
})
export class ProductDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly productService = inject(ProductService);
  private readonly cart = inject(CartService);
  private readonly activity = inject(ActivityService);

  product?: Product;
  recommendations: Recommendation[] = [];
  similarProducts: Recommendation[] = [];
  frequentlyBoughtTogether: Recommendation[] = [];
  loading = true;
  error = '';

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = Number(params.get('id'));
      this.loadProduct(id);
    });
  }

  loadProduct(id: number): void {
    this.loading = true;
    this.productService.getProduct(id).subscribe({
      next: (product) => {
        this.product = product;
        this.loading = false;
        this.activity.track(product.id, 'View');
        this.loadRecommendations(product.id);
      },
      error: () => {
        this.error = 'Product could not be loaded.';
        this.loading = false;
      }
    });
  }

  loadRecommendations(productId: number): void {
    this.productService.getRecommendations(productId, this.cart.productIds(), 6).subscribe({
      next: (items) => this.recommendations = items,
      error: () => this.recommendations = []
    });

    this.productService.getSimilarProducts(productId, this.cart.productIds(), 6).subscribe({
      next: (items) => this.similarProducts = items,
      error: () => this.similarProducts = []
    });

    this.productService.getFrequentlyBoughtTogether(productId, 4).subscribe({
      next: (items) => this.frequentlyBoughtTogether = items,
      error: () => this.frequentlyBoughtTogether = []
    });
  }

  addToCart(product: Product | Recommendation): void {
    if ('description' in product) {
      this.cart.add(product);
      this.loadRecommendations(product.id);
      return;
    }

    this.productService.getProduct(product.id).subscribe((fullProduct) => {
      this.cart.add(fullProduct);
      this.loadRecommendations(this.product?.id ?? fullProduct.id);
    });
  }

  tags(product: Product): string[] {
    return product.tags.split(',').map((tag) => tag.trim()).filter(Boolean);
  }
}
