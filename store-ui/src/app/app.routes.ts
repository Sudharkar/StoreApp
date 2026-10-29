import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/login/login').then((m) => m.Login),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/register/register').then((m) => m.Register),
  },
  {
    path: 'products',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/products/products').then((m) => m.Products),
  },
  { path: '', pathMatch: 'full', redirectTo: 'products' },
  { path: '**', redirectTo: 'products' },
];