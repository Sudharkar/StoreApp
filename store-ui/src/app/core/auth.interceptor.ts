import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, shareReplay, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthResponse } from './models';

// one shared refresh call, so many parallel 401s trigger only ONE refresh request
let refreshing$: Observable<AuthResponse> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const isAuthCall = req.url.includes('/auth/');

  const withToken = (r: HttpRequest<unknown>) => {
    const token = auth.accessToken;
    return token && !isAuthCall ? r.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : r;
  };

  return next(withToken(req)).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401 || isAuthCall || !auth.refreshToken) {
        return throwError(() => err);
      }

      if (!refreshing$) {
        refreshing$ = auth.refresh().pipe(
          finalize(() => (refreshing$ = null)),
          shareReplay(1),
        );
      }

      return refreshing$.pipe(
        switchMap(() => next(withToken(req))), // retry original request once
        catchError((e: HttpErrorResponse) => {
          if (e.status === 401) {
            auth.logout();
            router.navigate(['/login']);
          }
          return throwError(() => e);
        }),
      );
    }),
  );
};