import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { PageHeader, ErrorNote, Spinner, StatusPill } from '../components/ui';
import { useToast } from '../components/Toast';
import { useAuth } from '../auth/AuthContext';

/**
 * Self-service account page. Deliberately cannot change email, roles, or branch — those are
 * privileged attributes only an administrator may alter.
 */
export function ProfilePage() {
  const { user } = useAuth();
  const qc = useQueryClient();
  const toast = useToast();

  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [profileError, setProfileError] = useState<string | null>(null);

  useEffect(() => {
    if (user) {
      setFirstName(user.firstName);
      setLastName(user.lastName);
    }
  }, [user]);

  const saveProfile = useMutation({
    mutationFn: () => api.put('/api/auth/me', { firstName, lastName }),
    onSuccess: () => {
      qc.invalidateQueries();
      toast('Profile updated.');
      setProfileError(null);
    },
    onError: (e) => setProfileError(apiErrorMessage(e)),
  });

  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [pwError, setPwError] = useState<string | null>(null);

  const changePassword = useMutation({
    mutationFn: () => api.post('/api/auth/change-password', { currentPassword, newPassword }),
    onSuccess: () => {
      toast('Password changed.');
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
      setPwError(null);
    },
    onError: (e) => setPwError(apiErrorMessage(e)),
  });

  const pwValid = currentPassword && newPassword.length >= 8 && newPassword === confirmPassword;

  return (
    <>
      <PageHeader title="My profile" subtitle="Update your details and password." />

      <div className="grid max-w-4xl grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="card space-y-4 p-5">
          <h2 className="text-base font-semibold text-ink">Details</h2>
          {profileError && <ErrorNote message={profileError} />}
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="field-label">First name</label>
              <input className="input" value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </div>
            <div>
              <label className="field-label">Last name</label>
              <input className="input" value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </div>
          </div>

          {/* Read-only, privileged attributes. */}
          <div>
            <label className="field-label">Email</label>
            <input className="input bg-canvas" value={user?.email ?? ''} disabled />
            <p className="mt-1 text-xs text-ink-muted">Contact an administrator to change your email.</p>
          </div>
          <div>
            <label className="field-label">Roles</label>
            <div className="flex flex-wrap gap-1.5">
              {user?.roles.map((r) => (
                <StatusPill key={r} label={r} tone="neutral" />
              ))}
            </div>
          </div>

          <div className="flex justify-end">
            <button
              className="btn-primary"
              disabled={saveProfile.isPending || !firstName || !lastName}
              onClick={() => saveProfile.mutate()}
            >
              {saveProfile.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Save details'}
            </button>
          </div>
        </div>

        <div className="card space-y-4 p-5">
          <h2 className="text-base font-semibold text-ink">Change password</h2>
          <p className="text-sm text-ink-muted">Your current password is required — a live session is not enough.</p>
          {pwError && <ErrorNote message={pwError} />}
          <div>
            <label className="field-label">Current password</label>
            <input
              className="input"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
            />
          </div>
          <div>
            <label className="field-label">New password</label>
            <input className="input" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} />
          </div>
          <div>
            <label className="field-label">Confirm new password</label>
            <input
              className={`input ${confirmPassword && confirmPassword !== newPassword ? 'input-error' : ''}`}
              type="password"
              value={confirmPassword}
              onChange={(e) => setConfirmPassword(e.target.value)}
            />
          </div>
          <div className="flex justify-end">
            <button className="btn-primary" disabled={!pwValid || changePassword.isPending} onClick={() => changePassword.mutate()}>
              {changePassword.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Change password'}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
