export interface BillingAddress {
  /** The store ("enseigne"); null when the receipt names none. */
  storeName: string | null;
  street: string;
  postalCode: string;
  city: string;
}

/** The body of an expense creation or update. */
export interface SaveExpense {
  /** "2025-10-15", within the report's month. */
  date: string;
  description: string;
  /** In euros, at most two decimals. */
  amount: number;
  billingAddress: BillingAddress;
}

export interface ExpenseSummary extends SaveExpense {
  id: string;
}

export interface ExpenseDetails extends ExpenseSummary {
  /** Changes on every save; sent back on update so a change made meanwhile is not overwritten. */
  version: number;
}

/** "Fnac, 12 rue de Rivoli, 75001 Paris"; without the store when there is none. */
export function formatBillingAddress(address: BillingAddress): string {
  const parts = [address.storeName, address.street, `${address.postalCode} ${address.city}`];
  return parts.filter((part) => part !== null && part !== '').join(', ');
}
