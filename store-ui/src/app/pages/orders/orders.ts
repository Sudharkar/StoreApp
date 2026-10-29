import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { errorMessage } from '../../core/error-message';
import { Order } from '../../core/models';
import { DatePipe } from '@angular/common';
@Component({
  selector: 'app-orders',
  imports: [RouterLink,DatePipe],
  templateUrl: './orders.html',
  styleUrl: './orders.css',
})
export class Orders implements OnInit {
  private api = inject(ApiService);

  orders = signal<Order[]>([]);
  loading = signal(false);
  error = signal('');
  actionError = signal('');
  cancellingId = signal<number | null>(null);
  page = signal(1);
  totalPages = signal(0);
  readonly pageSize = 5;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.api.getMyOrders(this.page(), this.pageSize).subscribe({
      next: (r) => {
        this.orders.set(r.items);
        this.totalPages.set(r.totalPages);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(errorMessage(e));
        this.loading.set(false);
      },
    });
  }

  goTo(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page.set(p);
    this.load();
  }

  // customer can cancel only Pending orders
  canCancel(o: Order): boolean {
    return o.status === 'Pending';
  }

  cancel(o: Order): void {
    if (this.cancellingId() !== null) return;
    this.cancellingId.set(o.id);
    this.actionError.set('');
    this.api.updateOrderStatus(o.id, 'Cancelled').subscribe({
      next: () => {
        this.cancellingId.set(null);
        this.load();
      },
      error: (e) => {
        this.cancellingId.set(null);
        this.actionError.set(errorMessage(e));
        this.load();
      },
    });
  }
}