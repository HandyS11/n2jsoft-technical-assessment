import { DatePipe } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';

describe('appConfig', () => {
  it('formats dates in French', () => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, DatePipe],
    });

    const date = TestBed.inject(DatePipe).transform('2025-10-15', 'fullDate');

    expect(date).toBe('mercredi 15 octobre 2025');
  });
});
