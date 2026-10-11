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
  /** The report's expenses, deleted ones left out: what the monthly quota counts. */
  expenseCount: number;
  /** The sum of their amounts, in euros. */
  total: number;
  /** The user's current quota. */
  monthlyExpenseQuota: number;
  /** The user is deleted: the report's expenses can no longer change. */
  isReadOnly: boolean;
}
