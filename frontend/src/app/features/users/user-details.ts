export interface Address {
  street: string;
  postalCode: string;
  city: string;
}

/** The body of a user creation or update. */
export interface SaveUser {
  firstName: string;
  lastName: string;
  address: Address;
  monthlyExpenseQuota: number;
}

export interface UserDetails extends SaveUser {
  id: string;
  isActive: boolean;
  /** Changes on every save; sent back on update so a change made meanwhile is not overwritten. */
  version: number;
}
