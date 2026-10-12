import { HttpErrorResponse } from '@angular/common/http';
import { businessRuleCode, hasStatus } from './problem-details';

describe('hasStatus', () => {
  it('matches an HTTP error response with one of the statuses', () => {
    const error = new HttpErrorResponse({ status: 412 });

    expect(hasStatus(error, 409, 412)).toBe(true);
    expect(hasStatus(error, 404)).toBe(false);
  });

  it('is false for an error that is not an HTTP response', () => {
    expect(hasStatus(new Error('Service unavailable'), 404)).toBe(false);
  });
});

describe('businessRuleCode', () => {
  it('reads the code of a 422', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: { status: 422, code: 'expense_report.already_exists' },
    });

    expect(businessRuleCode(error)).toBe('expense_report.already_exists');
  });

  it('ignores a code sent with another status', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { code: 'expense_report.already_exists' },
    });

    expect(businessRuleCode(error)).toBeUndefined();
  });

  it.each([null, { status: 422 }, { code: 42 }])('is undefined for a 422 whose body is %j', (body) => {
    expect(businessRuleCode(new HttpErrorResponse({ status: 422, error: body }))).toBeUndefined();
  });

  it('is undefined for an error that is not an HTTP response', () => {
    expect(businessRuleCode(new Error('Service unavailable'))).toBeUndefined();
  });
});
