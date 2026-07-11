import { useEffect, useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api, apiErrorMessage } from '../lib/api';
import { PageHeader, ErrorNote, Spinner, StatusPill } from '../components/ui';
import { useToast } from '../components/Toast';
import { useAuth } from '../auth/AuthContext';
import type { MfaSetup } from '../lib/types';

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

  // --- Multi-factor authentication ---
  const [mfaEnabled, setMfaEnabled] = useState(false);
  const [setup, setSetup] = useState<MfaSetup | null>(null);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [mfaCode, setMfaCode] = useState('');
  const [mfaError, setMfaError] = useState<string | null>(null);
  const [disablePassword, setDisablePassword] = useState('');

  useEffect(() => {
    if (user) setMfaEnabled(user.mfaEnabled);
  }, [user]);

  const beginMfaSetup = useMutation({
    mutationFn: () => api.post<MfaSetup>('/api/auth/mfa/setup'),
    onSuccess: ({ data }) => {
      setSetup(data);
      setMfaCode('');
      setMfaError(null);
    },
    onError: (e) => setMfaError(apiErrorMessage(e)),
  });

  const enableMfa = useMutation({
    mutationFn: () => api.post<string[]>('/api/auth/mfa/enable', { code: mfaCode }),
    onSuccess: ({ data }) => {
      setRecoveryCodes(data);
      setSetup(null);
      setMfaEnabled(true);
      setMfaError(null);
    },
    onError: (e) => setMfaError(apiErrorMessage(e, 'Invalid code.')),
  });

  const disableMfa = useMutation({
    mutationFn: () => api.post('/api/auth/mfa/disable', { currentPassword: disablePassword }),
    onSuccess: () => {
      setMfaEnabled(false);
      setDisablePassword('');
      setMfaError(null);
      toast('Two-factor authentication disabled.');
    },
    onError: (e) => setMfaError(apiErrorMessage(e)),
  });

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

        <div className="card space-y-4 p-5 lg:col-span-2">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-semibold text-ink">Two-factor authentication</h2>
            <StatusPill label={mfaEnabled ? 'Enabled' : 'Disabled'} tone={mfaEnabled ? 'success' : 'neutral'} />
          </div>
          {mfaError && <ErrorNote message={mfaError} />}

          {recoveryCodes ? (
            <div className="space-y-3">
              <p className="text-sm text-ink-muted">
                Save these recovery codes somewhere safe. Each can be used once if you lose access to your
                authenticator — they will not be shown again.
              </p>
              <div className="grid grid-cols-2 gap-2 rounded-md bg-canvas p-4 font-mono text-sm">
                {recoveryCodes.map((c) => (
                  <span key={c}>{c}</span>
                ))}
              </div>
              <div className="flex justify-end">
                <button className="btn-primary" onClick={() => setRecoveryCodes(null)}>
                  Done
                </button>
              </div>
            </div>
          ) : setup ? (
            <div className="space-y-3">
              <p className="text-sm text-ink-muted">
                Scan this key into your authenticator app (Google Authenticator, Authy, etc.), or enter it manually,
                then confirm with the 6-digit code it generates.
              </p>
              <div>
                <label className="field-label">Setup key</label>
                <input className="input bg-canvas font-mono text-sm" value={setup.sharedKey} readOnly />
              </div>
              <div>
                <label className="field-label">6-digit code</label>
                <input
                  className="input"
                  placeholder="123456"
                  value={mfaCode}
                  onChange={(e) => setMfaCode(e.target.value)}
                  autoFocus
                />
              </div>
              <div className="flex justify-end gap-2">
                <button
                  className="btn-secondary"
                  onClick={() => {
                    setSetup(null);
                    setMfaError(null);
                  }}
                >
                  Cancel
                </button>
                <button
                  className="btn-primary"
                  disabled={mfaCode.length !== 6 || enableMfa.isPending}
                  onClick={() => enableMfa.mutate()}
                >
                  {enableMfa.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Confirm and enable'}
                </button>
              </div>
            </div>
          ) : mfaEnabled ? (
            <div className="space-y-3">
              <p className="text-sm text-ink-muted">
                Enter your current password to turn two-factor authentication off.
              </p>
              <div>
                <label className="field-label">Current password</label>
                <input
                  className="input"
                  type="password"
                  value={disablePassword}
                  onChange={(e) => setDisablePassword(e.target.value)}
                />
              </div>
              <div className="flex justify-end">
                <button
                  className="btn-secondary"
                  disabled={!disablePassword || disableMfa.isPending}
                  onClick={() => disableMfa.mutate()}
                >
                  {disableMfa.isPending ? <Spinner className="h-4 w-4 text-ink" /> : 'Disable two-factor authentication'}
                </button>
              </div>
            </div>
          ) : (
            <div className="space-y-3">
              <p className="text-sm text-ink-muted">
                Add an authenticator app as a second step at sign-in, on top of your password.
              </p>
              <div className="flex justify-end">
                <button className="btn-primary" disabled={beginMfaSetup.isPending} onClick={() => beginMfaSetup.mutate()}>
                  {beginMfaSetup.isPending ? <Spinner className="h-4 w-4 text-white" /> : 'Set up two-factor authentication'}
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </>
  );
}
