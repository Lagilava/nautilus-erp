import { Link } from 'react-router-dom';
import { Compass } from 'lucide-react';
import { PageHeader } from '../components/ui';

/**
 * Placeholder for the multi-step transactional flows (sales orders, purchase orders,
 * invoices). The backend endpoints and full order/invoice state machines already exist;
 * these screens are built on the same list + wizard patterns established in Products.
 */
export function ComingSoon({ title, endpoint }: { title: string; endpoint: string }) {
  return (
    <>
      <PageHeader title={title} />
      <div className="card flex flex-col items-center justify-center px-6 py-20 text-center">
        <div className="flex h-14 w-14 items-center justify-center rounded-full bg-lagoon-50 text-lagoon-500">
          <Compass className="h-7 w-7" />
        </div>
        <h2 className="mt-5 text-lg font-semibold text-ink">Screen in progress</h2>
        <p className="mt-2 max-w-md text-sm text-ink-muted">
          The <span className="font-medium text-ink-soft">{title}</span> API (
          <code className="rounded bg-canvas px-1.5 py-0.5 text-xs">{endpoint}</code>) is complete and
          tested. This screen follows the same list-and-form patterns used across the app.
        </p>
        <Link to="/" className="btn-secondary mt-6">
          Back to dashboard
        </Link>
      </div>
    </>
  );
}
