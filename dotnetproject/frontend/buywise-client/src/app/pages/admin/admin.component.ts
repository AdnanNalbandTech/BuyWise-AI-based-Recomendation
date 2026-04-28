import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Category, Product, ProductUpsert } from '../../core/models';
import { ProductService } from '../../core/product.service';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.css'
})
export class AdminComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly productsApi = inject(ProductService);

  categories: Category[] = [];
  products: Product[] = [];
  message = '';
  error = '';
  saving = false;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required]],
    description: ['', [Validators.required]],
    price: [99, [Validators.required, Validators.min(1)]],
    imageUrl: ['https://images.unsplash.com/photo-1516321318423-f06f85e504b3?auto=format&fit=crop&w=900&q=80', [Validators.required]],
    stock: [10, [Validators.required, Validators.min(0)]],
    categoryId: [1, [Validators.required, Validators.min(1)]],
    rating: [4.5, [Validators.required, Validators.min(0), Validators.max(5)]],
    reviewCount: [25, [Validators.required, Validators.min(0)]],
    brand: ['', [Validators.required]],
    tags: ['', [Validators.required]],
    featured: [false]
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.productsApi.getCategories().subscribe((categories) => {
      this.categories = categories;
      if (categories.length) {
        this.form.controls.categoryId.setValue(categories[0].id);
      }
    });

    this.productsApi.getProducts().subscribe((products) => this.products = products);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    this.error = '';
    this.message = '';
    const payload = this.form.getRawValue() as ProductUpsert;

    this.productsApi.createProduct(payload).subscribe({
      next: (product) => {
        this.products = [product, ...this.products];
        this.message = `${product.name} was added to BUYWISE.`;
        this.saving = false;
        this.form.patchValue({
          name: '',
          description: '',
          price: 99,
          stock: 10,
          rating: 4.5,
          reviewCount: 25,
          brand: '',
          tags: '',
          featured: false
        });
      },
      error: (response) => {
        this.error = response.error?.message ?? 'Product could not be added.';
        this.saving = false;
      }
    });
  }

  deleteProduct(product: Product): void {
    this.productsApi.deleteProduct(product.id).subscribe({
      next: () => {
        this.products = this.products.filter((item) => item.id !== product.id);
        this.message = `${product.name} was removed.`;
      },
      error: () => this.error = 'Product could not be deleted. It may already be part of an order.'
    });
  }
}
