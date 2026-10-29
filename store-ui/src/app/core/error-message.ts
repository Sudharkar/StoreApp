import { HttpErrorResponse } from '@angular/common/http';

export function errorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) return 'Something went wrong. Please try again.';

  switch (err.status) {
    case 0:
      return 'Cannot reach the server. Check your connection and that the API is running.';
    case 401:
      return err.error?.detail ?? 'Your session has expired. Please log in again.';
    case 403:
      return 'You do not have permission to do this.';
    case 404:
      return err.error?.detail ?? 'The item was not found.';
    case 409:
      return err.error?.detail ?? 'Out of stock or the item changed. Please refresh and try again.';
    case 429:
      return 'Too many login attempts. Please wait a minute and try again.';
    case 400: {
      const errors = err.error?.errors;
      if (errors) return Object.values(errors).flat().join(' ');
      return err.error?.detail ?? 'Invalid request.';
    }
    default:
      return 'Server error. Please try again later.';
  }
}