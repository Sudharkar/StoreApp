export interface User {
  id: number;
  fullName: string;
  email: string;
  role: 'Admin' | 'Customer';
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAt: string;
  user: User;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface Product {
  id: number;
  sku: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface ProductQuery {
  name?: string;
  minPrice?: number;
  maxPrice?: number;
  inStock?: boolean;
  sortBy?: string;
  desc?: boolean;
  page: number;
  pageSize: number;
}

export interface SaveProductRequest {
  sku?: string;
  name: string;
  price: number;
  stockQuantity: number;
  isActive: boolean;
}

export interface OrderItem {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
}

export type OrderStatus = 'Pending' | 'Confirmed' | 'Shipped' | 'Cancelled';

export interface Order {
  id: number;
  customerId: number;
  customerName: string;
  status: OrderStatus;
  subTotal: number;
  discount: number;
  total: number;
  createdAt: string;
  items: OrderItem[];
}

export interface TopCustomer {
  customerId: number;
  fullName: string;
  email: string;
  totalSpend: number;
  orderCount: number;
}

export interface CartItem {
  productId: number;
  name: string;
  price: number;
  quantity: number;
  maxStock: number;
}