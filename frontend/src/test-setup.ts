import '@angular/compiler';
import '@analogjs/vitest-angular/setup-zone';

import { setupTestBed } from '@analogjs/vitest-angular/setup-testbed';

// Initializes the TestBed once per worker: Analog runs specs in the vmThreads pool, where a worker reused for
// several spec files keeps the previous file's TestBed (calling initTestEnvironment again throws).
setupTestBed({
  zoneless: false,
});
