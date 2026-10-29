import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../core/api.service';
import { CartService } from '../../core/cart.service';
import { errorMessage } from '../../core/error-message';
import { Order } from '../../core/models';

const KEY_STORAGE = 'checkout_idempotency_key';

@Component({
  selector: 'app-cart',
  imports: [FormsModule, RouterLink],
  templateUrl: './cart.html',
  styleUrl: './cart.css',
})
export class Cart {
  cart = inject(CartService);
  private api = inject(ApiService);
  private router = inject(Router);

  placing = signal(false);
  error = signal('');
  placed = signal<Order | null>(null);

  changeQty(productId: number, value: number): void {
    this.cart.setQuantity(productId, Number(value));
    this.resetKey(); // cart content changed => new order => new key
  }

  remove(productId: number): void {
    this.cart.remove(productId);
    this.resetKey();
  }

  checkout(): void {
    if (this.placing() || this.cart.items().length === 0) return; // blocks double click
    this.placing.set(true);
    this.error.set('');

    const items = this.cart.items().map((i) => ({ productId: i.productId, quantity: i.quantity }));
    this.api.placeOrder(items, this.getKey()).subscribe({
      next: (order) => {
        this.placing.set(false);
        this.placed.set(order);
        this.cart.clear();
        this.resetKey();
      },
      error: (e) => {
        this.placing.set(false);
        // key is kept on purpose: a retry of the same checkout can never create a second order
        this.error.set(errorMessage(e));
      },
    });
  }

  goToOrders(): void {
    this.router.navigate(['/orders']);
  }

  private getKey(): string {
    let key = localStorage.getItem(KEY_STORAGE);
    if (!key) {
      key = crypto.randomUUID();
      localStorage.setItem(KEY_STORAGE, key);
    }
    return key;
  }

  private resetKey(): void {
    localStorage.removeItem(KEY_STORAGE);
  }
}