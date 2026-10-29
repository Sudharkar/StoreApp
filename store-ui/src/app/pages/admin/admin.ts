import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/api.service';
import { errorMessage } from '../../core/error-message';
import { Order, Product, SaveProductRequest, TopCustomer } from '../../core/models';

type Tab = 'products' | 'orders' | 'report';

@Component({
  selector: 'app-admin',
  imports: [FormsModule, DatePipe],
  templateUrl: './admin.html',
  styleUrl: './admin.css',
})
export class Admin implements OnInit {
  private api = inject(ApiService);

  tab = signal<Tab>('products');
  loading = signal(false);
  error = signal('');
  message = signal('');

  // products
  products = signal<Product[]>([]);
  productPage = signal(1);
  productPages = signal(0);
  editingId: number | null = null;
  form: SaveProductRequest = this.emptyForm();

  // orders
  orders = signal<Order[]>([]);
  orderPage = signal(1);
  orderPages = signal(0);
  readonly statuses = ['Pending', 'Confirmed', 'Shipped', 'Cancelled'];

  // report
  top = signal<TopCustomer[]>([]);
  days = 30;

  ngOnInit(): void {
    this.loadProducts();
  }

  setTab(t: Tab): void {
    this.tab.set(t);
    this.error.set('');
    this.message.set('');
    if (t === 'products') this.loadProducts();
    if (t === 'orders') this.loadOrders();
    if (t === 'report') this.loadReport();
  }

  // ---------- products ----------
  loadProducts(): void {
    this.loading.set(true);
    this.api.getProducts({ page: this.productPage(), pageSize: 8, sortBy: 'newest' }).subscribe({
      next: (r) => {
        this.products.set(r.items);
        this.productPages.set(r.totalPages);
        this.loading.set(false);
      },
      error: (e) => this.fail(e),
    });
  }

  edit(p: Product): void {
    this.editingId = p.id;
    this.form = { sku: p.sku, name: p.name, price: p.price, stockQuantity: p.stockQuantity, isActive: p.isActive };
    this.message.set('');
  }

  resetForm(): void {
    this.editingId = null;
    this.form = this.emptyForm();
  }

  saveProduct(): void {
    this.error.set('');
    const req = this.editingId
      ? this.api.updateProduct(this.editingId, this.form)
      : this.api.createProduct(this.form);
    req.subscribe({
      next: () => {
        this.message.set(this.editingId ? 'Product updated.' : 'Product created.');
        this.resetForm();
        this.loadProducts();
      },
      error: (e) => this.error.set(errorMessage(e)),
    });
  }

  goProducts(p: number): void {
    if (p < 1 || p > this.productPages()) return;
    this.productPage.set(p);
    this.loadProducts();
  }

  // ---------- orders ----------
  loadOrders(): void {
    this.loading.set(true);
    this.api.getAllOrders(this.orderPage(), 8).subscribe({
      next: (r) => {
        this.orders.set(r.items);
        this.orderPages.set(r.totalPages);
        this.loading.set(false);
      },
      error: (e) => this.fail(e),
    });
  }

  changeStatus(o: Order, status: string): void {
    if (status === o.status) return;
    this.error.set('');
    this.api.updateOrderStatus(o.id, status).subscribe({
      next: () => {
        this.message.set(`Order #${o.id} is now ${status}.`);
        this.loadOrders();
      },
      error: (e) => {
        this.error.set(errorMessage(e));
        this.loadOrders(); // reset dropdown to the real status
      },
    });
  }

  goOrders(p: number): void {
    if (p < 1 || p > this.orderPages()) return;
    this.orderPage.set(p);
    this.loadOrders();
  }

  // ---------- report ----------
  loadReport(): void {
    this.loading.set(true);
    this.api.getTopCustomers(this.days, 5).subscribe({
      next: (r) => {
        this.top.set(r);
        this.loading.set(false);
      },
      error: (e) => this.fail(e),
    });
  }

  maxSpend(): number {
    return Math.max(1, ...this.top().map((t) => t.totalSpend));
  }

  private fail(e: unknown): void {
    this.error.set(errorMessage(e));
    this.loading.set(false);
  }

  private emptyForm(): SaveProductRequest {
    return { sku: '', name: '', price: 0, stockQuantity: 0, isActive: true };
  }
}