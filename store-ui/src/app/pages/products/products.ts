// import { Component } from '@angular/core';

// @Component({
//   selector: 'app-products',
//   imports: [],
//   templateUrl: './products.html',
//   styleUrl: './products.css',
// })
// export class Products {}
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CartService } from '../../core/cart.service';
import { errorMessage } from '../../core/error-message';
import { Product, ProductQuery } from '../../core/models';

@Component({
  selector: 'app-products',
  imports: [FormsModule],
  templateUrl: './products.html',
  styleUrl: './products.css',
})
export class Products implements OnInit {
  private api = inject(ApiService);
  private cart = inject(CartService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);

  products = signal<Product[]>([]);
  totalCount = signal(0);
  totalPages = signal(0);
  loading = signal(false);
  error = signal('');
  added = signal('');

  // form fields (bound with ngModel)
  name = '';
  minPrice: number | null = null;
  maxPrice: number | null = null;
  inStock = false;
  sortBy = 'name';
  desc = false;
  page = 1;
  readonly pageSize = 8;

  private search$ = new Subject<string>();

  ngOnInit(): void {
    // 300 ms debounce on the search box
    this.search$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe((value) => {
        this.name = value;
        this.page = 1;
        this.updateUrl();
      });

    // URL is the source of truth: refresh / shared link restores the filters
    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((p) => {
      this.name = p.get('name') ?? '';
      this.minPrice = p.get('minPrice') ? Number(p.get('minPrice')) : null;
      this.maxPrice = p.get('maxPrice') ? Number(p.get('maxPrice')) : null;
      this.inStock = p.get('inStock') === 'true';
      this.sortBy = p.get('sortBy') ?? 'name';
      this.desc = p.get('desc') === 'true';
      this.page = Number(p.get('page') ?? 1);
      this.load();
    });
  }

  onSearch(value: string): void {
    this.search$.next(value.trim());
  }

  applyFilters(): void {
    this.page = 1;
    this.updateUrl();
  }

  goToPage(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.page = p;
    this.updateUrl();
  }

  clearFilters(): void {
    this.router.navigate([], { queryParams: {} });
  }

  addToCart(p: Product): void {
    this.cart.add(p);
    this.added.set(`${p.name} added to cart`);
    setTimeout(() => this.added.set(''), 1500);
  }

  private updateUrl(): void {
    const queryParams: Record<string, string | number | boolean | null> = {
      name: this.name || null,
      minPrice: this.minPrice,
      maxPrice: this.maxPrice,
      inStock: this.inStock ? true : null,
      sortBy: this.sortBy !== 'name' ? this.sortBy : null,
      desc: this.desc ? true : null,
      page: this.page > 1 ? this.page : null,
    };
    this.router.navigate([], { queryParams });
  }

  private load(): void {
    const q: ProductQuery = {
      name: this.name || undefined,
      minPrice: this.minPrice ?? undefined,
      maxPrice: this.maxPrice ?? undefined,
      inStock: this.inStock || undefined,
      sortBy: this.sortBy,
      desc: this.desc,
      page: this.page,
      pageSize: this.pageSize,
    };
    this.loading.set(true);
    this.error.set('');
    this.api.getProducts(q).subscribe({
      next: (r) => {
        this.products.set(r.items);
        this.totalCount.set(r.totalCount);
        this.totalPages.set(r.totalPages);
        this.loading.set(false);
      },
      error: (e) => {
        this.error.set(errorMessage(e));
        this.loading.set(false);
      },
    });
  }
}