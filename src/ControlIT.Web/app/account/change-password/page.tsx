"use client";

import { useState } from "react";
import { changePassword, ApiError } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export default function ChangePasswordPage() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [error, setError] = useState("");
  const [success, setSuccess] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setSuccess(false);

    if (!currentPassword) {
      setError("Current password is required.");
      return;
    }
    if (newPassword.length < 12) {
      setError("New password must be at least 12 characters.");
      return;
    }
    if (newPassword === currentPassword) {
      setError("New password must differ from the current password.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("New password and confirmation do not match.");
      return;
    }

    setIsLoading(true);
    try {
      await changePassword({ currentPassword, newPassword });
      setSuccess(true);
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || "Failed to change password.");
      } else {
        setError("An unexpected error occurred. Please try again.");
      }
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="max-w-md space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-foreground">Change Password</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Update your account password. New password must be at least 12 characters.
        </p>
      </div>

      <div className="rounded-lg border border-border bg-card p-6">
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <label htmlFor="current-password" className="block text-xs font-medium text-muted-foreground">
              Current Password
            </label>
            <Input
              id="current-password"
              type="password"
              value={currentPassword}
              onChange={(e) => {
                setCurrentPassword(e.target.value);
                setError("");
                setSuccess(false);
              }}
              className="bg-input border-border text-foreground placeholder:text-muted-foreground"
              autoComplete="current-password"
              disabled={isLoading}
            />
          </div>

          <div className="space-y-1">
            <label htmlFor="new-password" className="block text-xs font-medium text-muted-foreground">
              New Password
            </label>
            <Input
              id="new-password"
              type="password"
              value={newPassword}
              onChange={(e) => {
                setNewPassword(e.target.value);
                setError("");
                setSuccess(false);
              }}
              className="bg-input border-border text-foreground placeholder:text-muted-foreground"
              autoComplete="new-password"
              disabled={isLoading}
            />
          </div>

          <div className="space-y-1">
            <label htmlFor="confirm-password" className="block text-xs font-medium text-muted-foreground">
              Confirm New Password
            </label>
            <Input
              id="confirm-password"
              type="password"
              value={confirmPassword}
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                setError("");
                setSuccess(false);
              }}
              className="bg-input border-border text-foreground placeholder:text-muted-foreground"
              autoComplete="new-password"
              disabled={isLoading}
            />
          </div>

          {error && <p className="text-xs text-red-400">{error}</p>}
          {success && (
            <p className="text-xs text-green-400">Password changed successfully.</p>
          )}

          <div className="pt-1">
            <Button type="submit" disabled={isLoading} className="w-full">
              {isLoading ? "Updating..." : "Update Password"}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
