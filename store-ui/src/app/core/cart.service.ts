import { Injectable, computed, signal } from '@angular/core';
import { CartItem, Product } from './models';

const CART_KEY = 'cart';

@Injectable({ providedIn: 'root' })
export class CartService {
  readonly items = signal<CartItem[]>(this.load());
  readonly count = computed(() => this.items().reduce((n, i) => n + i.quantity, 0));
  readonly subTotal = computed(() => this.items().reduce((s, i) => s + i.price * i.quantity, 0));

  add(p: Product, qty = 1): void {
    const list = [...this.items()];
    const existing = list.find((i) => i.productId === p.id);
    if (existing) {
      existing.quantity = Math.min(existing.quantity + qty, p.stockQuantity);
      existing.maxStock = p.stockQuantity;
    } else {
      list.push({
        productId: p.id, name: p.name, price: p.price,
        quantity: Math.min(qty, p.stockQuantity), maxStock: p.stockQuantity,
      });
    }
    this.save(list);
  }

  setQuantity(productId: number, qty: number): void {
    const list = this.items()
      .map((i) => (i.productId === productId ? { ...i, quantity: Math.min(Math.max(qty, 1), i.maxStock) } : i));
    this.save(list);
  }

  remove(productId: number): void {
    this.save(this.items().filter((i) => i.productId !== productId));
  }

  clear(): void {
    this.save([]);
  }

  private save(list: CartItem[]): void {
    this.items.set(list);
    localStorage.setItem(CART_KEY, JSON.stringify(list));
  }

  private load(): CartItem[] {
    try {
      return JSON.parse(localStorage.getItem(CART_KEY) ?? '[]') as CartItem[];
    } catch {
      return [];
    }
  }
}