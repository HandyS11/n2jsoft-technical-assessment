import { BillingAddress, formatBillingAddress } from './expense';

const address: BillingAddress = { storeName: 'Fnac', street: '12 rue de Rivoli', postalCode: '75001', city: 'Paris' };

describe('formatBillingAddress', () => {
  it('writes the store, the street, then the postal code and the city', () => {
    expect(formatBillingAddress(address)).toBe('Fnac, 12 rue de Rivoli, 75001 Paris');
  });

  it('leaves out a missing store', () => {
    expect(formatBillingAddress({ ...address, storeName: null })).toBe('12 rue de Rivoli, 75001 Paris');
  });
});
