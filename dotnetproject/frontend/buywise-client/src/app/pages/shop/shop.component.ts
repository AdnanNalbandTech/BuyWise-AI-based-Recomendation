import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { CartService } from '../../core/cart.service';
import { Category, Product } from '../../core/models';
import { ProductService } from '../../core/product.service';

@Component({
  selector: 'app-shop',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './shop.component.html',
  styleUrl: './shop.component.css'
})
export class ShopComponent implements OnInit {
  private readonly productService = inject(ProductService);
  private readonly cart = inject(CartService);

  categories: Category[] = [];
  products: Product[] = [];
  featured: Product[] = [];
  selectedCategoryId = 0;
  search = '';
  loading = true;
  error = '';

  ngOnInit(): void {
    forkJoin({
      categories: this.productService.getCategories(),
      products: this.productService.getProducts()
    }).subscribe({
      next: ({ categories, products }) => {
        this.categories = categories;
        this.products = products;
        this.featured = products.filter((product) => product.featured).slice(0, 4);
        this.loading = false;
      },
      error: () => {
        this.error = 'Could not load products. Start the ASP.NET API and confirm MySQL credentials.';
        this.loading = false;
      }
    });
  }

  loadProducts(): void {
    this.loading = true;
    this.productService.getProducts(this.search, this.selectedCategoryId || undefined).subscribe({
      next: (products) => {
        this.products = products;
        this.loading = false;
      },
      error: () => {
        this.error = 'Product search failed.';
        this.loading = false;
      }
    });
  }

  selectCategory(categoryId: number): void {
    this.selectedCategoryId = categoryId;
    this.loadProducts();
  }

  addToCart(product: Product): void {
    this.cart.add(product);
  }

  tags(product: Product): string[] {
    return product.tags.split(',').map((tag) => tag.trim()).filter(Boolean).slice(0, 3);
  }
}
