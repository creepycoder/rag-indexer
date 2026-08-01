import { HttpErrorResponse } from '@angular/common/http';

export function toErrorMessage(err: unknown): string {
  if (err instanceof HttpErrorResponse) {
    const body = err.error as { error?: string } | null;
    return body?.error ?? err.message;
  }
  return err instanceof Error ? err.message : String(err);
}
