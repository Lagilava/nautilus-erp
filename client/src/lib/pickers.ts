import { useQuery } from '@tanstack/react-query';
import { api } from './api';
import type { Paged, Product, Customer, Supplier, Warehouse } from './types';

// Reference lists for form pickers. Fetched in a single large page — adequate for the
// dropdowns; a server-side typeahead is a later enhancement for very large catalogs.
export function useProducts() {
  return useQuery({
    queryKey: ['picker', 'products'],
    queryFn: async () =>
      (await api.get<Paged<Product>>('/api/products', { params: { page: 1, pageSize: 200 } })).data.items,
  });
}

export function useCustomers() {
  return useQuery({
    queryKey: ['picker', 'customers'],
    queryFn: async () =>
      (await api.get<Paged<Customer>>('/api/customers', { params: { page: 1, pageSize: 200 } })).data.items,
  });
}

export function useSuppliers() {
  return useQuery({
    queryKey: ['picker', 'suppliers'],
    queryFn: async () =>
      (await api.get<Paged<Supplier>>('/api/suppliers', { params: { page: 1, pageSize: 200 } })).data.items,
  });
}

export function useWarehouses() {
  return useQuery({
    queryKey: ['picker', 'warehouses'],
    queryFn: async () => (await api.get<Warehouse[]>('/api/warehouses')).data,
  });
}
