import { describe, it, expect } from 'vitest';
import { AxiosError, AxiosHeaders } from 'axios';
import { apiErrorMessage } from './api';
import type { ProblemDetails } from './types';

function axiosErrorWith(problem: ProblemDetails | undefined, status = 400): AxiosError {
  const error = new AxiosError('Request failed', 'ERR_BAD_REQUEST');
  error.response = {
    data: problem,
    status,
    statusText: 'Bad Request',
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

describe('apiErrorMessage', () => {
  it('prefers the first field-level validation error', () => {
    const error = axiosErrorWith({ errors: { Email: ['Email is required.'], Password: ['Too short.'] } });
    expect(apiErrorMessage(error)).toBe('Email is required.');
  });

  it('falls back to detail when there are no field errors', () => {
    const error = axiosErrorWith({ detail: 'Invalid credentials.' });
    expect(apiErrorMessage(error)).toBe('Invalid credentials.');
  });

  it('falls back to title when there is no detail', () => {
    const error = axiosErrorWith({ title: 'unauthorized' });
    expect(apiErrorMessage(error)).toBe('unauthorized');
  });

  it('reports the server as unreachable when there is no response at all', () => {
    const error = new AxiosError('Network Error', 'ERR_NETWORK');
    expect(apiErrorMessage(error)).toContain('Cannot reach the server');
  });

  it('uses the caller-supplied fallback for a non-Axios error', () => {
    expect(apiErrorMessage(new Error('boom'), 'Something specific went wrong.')).toBe(
      'Something specific went wrong.',
    );
  });

  it('uses the default fallback when none is supplied and the response body is empty', () => {
    const error = axiosErrorWith(undefined);
    expect(apiErrorMessage(error)).toBe('Something went wrong.');
  });
});
