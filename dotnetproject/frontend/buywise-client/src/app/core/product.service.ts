import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from './auth.service';
import { Category, Product, ProductUpsert, Recommendation } from './models';

const API_URL = 'http://localhost:5148/api';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  getCategories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${API_URL}/categories`);
  }

  getProducts(search = '', categoryId?: number): Observable<Product[]> {
    let params = new HttpParams();
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    if (categoryId) {
      params = params.set('categoryId', categoryId);
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

  private authHeaders(): HttpHeaders {
    const token = this.auth.token;
    return token ? new HttpHeaders({ Authorization: `Bearer ${token}` }) : new HttpHeaders();
  }
}
