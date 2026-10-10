import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('links to the users page from the navigation', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const link = (fixture.nativeElement as HTMLElement).querySelector('nav a');

    expect(link?.textContent?.trim()).toBe('Utilisateurs');
    expect(link?.getAttribute('href')).toBe('/users');
  });
});
