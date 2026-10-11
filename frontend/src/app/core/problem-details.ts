import { HttpErrorResponse } from '@angular/common/http';

/**
 * The stable `code` of a 422, a business rule the API refused (e.g. "expense_report.already_exists"); undefined for
 * any other error.
 */
export function businessRuleCode(error: unknown): string | undefined {
  if (!(error instanceof HttpErrorResponse) || error.status !== 422) {
    return undefined;
  }
  const code: unknown = (error.error as { code?: unknown } | null)?.code;
  return typeof code === 'string' ? code : undefined;
}
