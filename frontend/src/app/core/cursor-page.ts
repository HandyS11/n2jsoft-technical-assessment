/** A slice of a sorted list; `next` asks for the following one, and is null after the last. */
export interface CursorPage<T> {
  items: T[];
  next: string | null;
}
