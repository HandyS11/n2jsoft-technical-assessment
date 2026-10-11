export interface ExpenseReportSummary {
  id: string;
  /** "Juste Leblanc - Octobre 2025", fixed when the report was created. */
  title: string;
  year: number;
  /** 1 for January to 12 for December. */
  month: number;
}
