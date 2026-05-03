import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { Category, Product, ProductFilters, ProductUpsert, Recommendation } from './models';

const API_URL = 'http://localhost:5148/api';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${API_URL}/categories`);
  }

  getProducts(search = '', categoryId?: number, filters: ProductFilters = {}): Observable<Product[]> {
    let params = new HttpParams();
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    if (categoryId) {
      params = params.set('categoryId', categoryId);
    }
    if (filters.minPrice) {
      params = params.set('minPrice', filters.minPrice);
    }
    if (filters.maxPrice) {
      params = params.set('maxPrice', filters.maxPrice);
    }
    if (filters.brand?.trim()) {
      params = params.set('brand', filters.brand.trim());
    }
    if (filters.minRating) {
      params = params.set('minRating', filters.minRating);
    }
    if (filters.tags?.trim()) {
      params = params.set('tags', filters.tags.trim());
    }

    return this.http.get<Product[]>(`${API_URL}/products`, { params });
  }

  getProduct(id: number): Observable<Product> {
    return this.http.get<Product>(`${API_URL}/products/${id}`);
  }

  createProduct(product: ProductUpsert): Observable<Product> {
    return this.http.post<Product>(`${API_URL}/products`, product, { headers: this.authHeaders() });
  }

  updateProduct(id: number, product: ProductUpsert): Observable<Product> {
    return this.http.put<Product>(`${API_URL}/products/${id}`, product, { headers: this.authHeaders() });
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${API_URL}/products/${id}`, { headers: this.authHeaders() });
  }

  getRecommendations(productId: number, cartIds: number[] = [], take = 6): Observable<Recommendation[]> {
    let params = new HttpParams().set('take', take);
    if (cartIds.length > 0) {
      params = params.set('cartIds', cartIds.join(','));
    }

    return this.http.get<Recommendation[]>(`${API_URL}/recommendations/${productId}`, { params });
  }

  getSimilarProducts(productId: number, cartIds: number[] = [], take = 6): Observable<Recommendation[]> {
    let params = new HttpParams().set('take', take);
    if (cartIds.length > 0) {
      params = params.set('cartIds', cartIds.join(','));
    }

    return this.http.get<Recommendation[]>(`${API_URL}/recommendations/similar/${productId}`, { params });
  }

  getFrequentlyBoughtTogether(productId: number, take = 4): Observable<Recommendation[]> {
    const params = new HttpParams().set('take', take);
    return this.http.get<Recommendation[]>(`${API_URL}/recommendations/frequently-bought-together/${productId}`, { params });
  }

  getRecommendedForUser(userId: number, take = 8): Observable<Recommendation[]> {
    const params = new HttpParams().set('take', take);
    return this.http.get<Recommendation[]>(`${API_URL}/recommendations/for-you/${userId}`, { params });
  }

  private authHeaders(): HttpHeaders {
    const token = this.auth.token;
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }
}
