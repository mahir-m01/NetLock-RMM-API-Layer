"use client";

import { useEffect, useState } from "react";
import { saveApiKey, readApiKey } from "@/lib/api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function ApiKeyGate({ children }: { children: React.ReactNode }) {
  const [hasKey, setHasKey] = useState<boolean | null>(null);
  const [value, setValue] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    const key = readApiKey();
    setHasKey(key.length > 0);
  }, []);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = value.trim();
    if (!trimmed) {
      setError("API key is required.");
      return;
    }
    saveApiKey(trimmed);
    setHasKey(true);
  }

  if (hasKey === null) {
    // Still checking localStorage — render nothing to avoid flicker
    return null;
  }

  if (!hasKey) {
    return (
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#001D35]">
        <div className="w-full max-w-sm rounded-xl border border-[rgba(107,148,193,0.18)] bg-[#003257] p-8 shadow-2xl">
          <h1 className="mb-1 text-xl font-semibold text-[#E9F1FF]">
            ControlIT
          </h1>
          <p className="mb-6 text-sm text-[#85AFDD]">
            Enter your API key to access the dashboard.
          </p>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Input
                type="password"
                placeholder="x-api-key"
                value={value}
                onChange={(e) => {
                  setValue(e.target.value);
                  setError("");
                }}
                className="border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF] placeholder:text-[#85AFDD] focus-visible:ring-[#A1CAFA]"
                autoFocus
                autoComplete="off"
              />
              {error && (
                <p className="mt-1 text-xs text-red-400">{error}</p>
              )}
            </div>
            <Button
              type="submit"
              className="w-full bg-[#A1CAFA] text-[#001D35] font-semibold hover:bg-[#D0E4FF]"
            >
              Continue
            </Button>
          </form>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
