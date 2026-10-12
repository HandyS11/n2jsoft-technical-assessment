import { Component, input } from '@angular/core';
import { PagedList } from './paged-list';

/** What the pagination bar reads of a paged list. */
export type Pager = Pick<PagedList<unknown>, 'page' | 'busy' | 'pageCount' | 'load'>;

/** *Précédent* / *Suivant* and « Page X sur Y » under a paged list; nothing until its first page shows. */
@Component({
  selector: 'app-pagination',
  template: `
    @if (list().page(); as page) {
      <nav aria-label="Pagination" class="mt-4 flex items-center justify-end gap-3">
        <button
          type="button"
          class="btn-sm"
          [disabled]="list().busy() || page.page <= 1"
          (click)="list().load(page.page - 1)"
        >
          Précédent
        </button>
        <span class="muted">Page {{ page.page }} sur {{ list().pageCount() }}</span>
        <button
          type="button"
          class="btn-sm"
          [disabled]="list().busy() || page.page >= list().pageCount()"
          (click)="list().load(page.page + 1)"
        >
          Suivant
        </button>
      </nav>
    }
  `,
})
export class PaginationComponent {
  readonly list = input.required<Pager>();
}
