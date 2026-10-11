/** The body of a report creation. */
export interface CreateExpenseReport {
  userId: string;
  year: number;
  /** 1 for January to 12 for December. */
  month: number;
}

export interface ExpenseReportDetails extends CreateExpenseReport {
  id: string;
  /** "Juste Leblanc - Octobre 2025", fixed when the report was created. */
  title: string;
}
