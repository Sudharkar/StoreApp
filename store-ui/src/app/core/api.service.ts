import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { API_URL } from './auth.service';
import {
  Order, PagedResult, Product, ProductQuery, SaveProductRequest, TopCustomer,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  // ---------- products ----------
  getProducts(q: ProductQuery): Observable<PagedResult<Product>> {
    let params = new HttpParams().set('page', q.page).set('pageSize', q.pageSize);
    if (q.name) params = params.set('name', q.name);
    if (q.minPrice != null) params = params.set('minPrice', q.minPrice);
    if (q.maxPrice != null) params = params.set('maxPrice', q.maxPrice);
    if (q.inStock) params = params.set('inStock', true);
    if (q.sortBy) params = params.set('sortBy', q.sortBy);
    if (q.desc) params = params.set('desc', true);
    return this.http.get<PagedResult<Product>>(`${API_URL}/products`, { params });
  }

  getProduct(id: number): Observable<Product> {
    return this.http.get<Product>(`${API_URL}/products/${id}`);
  }

  createProduct(body: SaveProductRequest): Observable<Product> {
    return this.http.post<Product>(`${API_URL}/products`, body);
  }

  updateProduct(id: number, body: SaveProductRequest): Observable<Product> {
    return this.http.put<Product>(`${API_URL}/products/${id}`, body);
  }

  // ---------- orders ----------
  placeOrder(items: { productId: number; quantity: number }[], idempotencyKey: string): Observable<Order> {
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });
    return this.http.post<Order>(`${API_URL}/orders`, { items }, { headers });
  }

  getMyOrders(page: number, pageSize: number): Observable<PagedResult<Order>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Order>>(`${API_URL}/orders/my`, { params });
  }

  getAllOrders(page: number, pageSize: number): Observable<PagedResult<Order>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Order>>(`${API_URL}/orders`, { params });
  }

  updateOrderStatus(id: number, status: string): Observable<Order> {
    return this.http.patch<Order>(`${API_URL}/orders/${id}/status`, { status });
  }

  // ---------- reports ----------
  getTopCustomers(days = 30, top = 5): Observable<TopCustomer[]> {
    const params = new HttpParams().set('days', days).set('top', top);
    return this.http.get<TopCustomer[]>(`${API_URL}/reports/top-customers`, { params });
  }
}