"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { getUsers, createUser, patchUser, ApiError } from "@/lib/api";
import { useAuth } from "@/components/providers/auth-provider";
import type { Role, CreateUserRequest } from "@/lib/types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { UserPlus, Copy, Check } from "lucide-react";

const ROLES: Role[] = ["SuperAdmin", "CpAdmin", "ClientAdmin", "Technician"];

function roleBadgeVariant(role: Role): "default" | "secondary" | "outline" | "destructive" {
  switch (role) {
    case "SuperAdmin": return "destructive";
    case "CpAdmin": return "default";
    case "ClientAdmin": return "secondary";
    default: return "outline";
  }
}

function CopyButton({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  async function handleCopy() {
    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }
  return (
    <Button variant="ghost" size="icon" className="h-7 w-7 shrink-0" onClick={handleCopy}>
      {copied ? <Check className="h-3.5 w-3.5 text-green-400" /> : <Copy className="h-3.5 w-3.5" />}
    </Button>
  );
}

function CreateUserDialog({
  open,
  onOpenChange,
  onCreated,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onCreated: (password: string) => void;
}) {
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<Role>("ClientAdmin");
  const [tenantId, setTenantId] = useState("");
  const [formError, setFormError] = useState("");

  const mutation = useMutation({
    mutationFn: (req: CreateUserRequest) => createUser(req),
    onSuccess: (data) => {
      onCreated(data.generatedPassword);
      onOpenChange(false);
      setEmail("");
      setRole("ClientAdmin");
      setTenantId("");
      setFormError("");
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        setFormError(err.message || "Failed to create user.");
      } else {
        setFormError("An unexpected error occurred.");
      }
    },
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setFormError("");
    if (!email.trim()) {
      setFormError("Email is required.");
      return;
    }
    const parsedTenant = tenantId.trim() ? parseInt(tenantId.trim(), 10) : null;
    if (tenantId.trim() && isNaN(parsedTenant!)) {
      setFormError("Tenant ID must be a number.");
      return;
    }
    mutation.mutate({
      email: email.trim(),
      role,
      tenantId: parsedTenant,
      assignedClients: null,
    });
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="bg-card border-border text-foreground">
        <DialogHeader>
          <DialogTitle>Create User</DialogTitle>
          <DialogDescription className="text-muted-foreground">
            A password will be generated automatically.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 pt-2">
          <div className="space-y-1">
            <label htmlFor="new-email" className="text-xs font-medium text-muted-foreground">
              Email
            </label>
            <Input
              id="new-email"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="user@example.com"
              className="bg-input border-border text-foreground placeholder:text-muted-foreground"
              disabled={mutation.isPending}
            />
          </div>
          <div className="space-y-1">
            <label className="text-xs font-medium text-muted-foreground">Role</label>
            <Select value={role} onValueChange={(v) => setRole(v as Role)} disabled={mutation.isPending}>
              <SelectTrigger className="bg-input border-border text-foreground">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {ROLES.map((r) => (
                  <SelectItem key={r} value={r}>{r}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <label htmlFor="tenant-id" className="text-xs font-medium text-muted-foreground">
              Tenant ID <span className="text-muted-foreground">(optional)</span>
            </label>
            <Input
              id="tenant-id"
              type="number"
              value={tenantId}
              onChange={(e) => setTenantId(e.target.value)}
              placeholder="Leave blank for none"
              className="bg-input border-border text-foreground placeholder:text-muted-foreground"
              disabled={mutation.isPending}
            />
          </div>
          {formError && <p className="text-xs text-red-400">{formError}</p>}
          <div className="flex justify-end gap-2 pt-1">
            <Button
              type="button"
              variant="ghost"
              onClick={() => onOpenChange(false)}
              disabled={mutation.isPending}
              className="text-muted-foreground hover:text-foreground"
            >
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? "Creating..." : "Create"}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function GeneratedPasswordDialog({
  password,
  onClose,
}: {
  password: string;
  onClose: () => void;
}) {
  return (
    <Dialog open onOpenChange={onClose}>
      <DialogContent className="bg-card border-border text-foreground">
        <DialogHeader>
          <DialogTitle>User Created</DialogTitle>
          <DialogDescription className="text-muted-foreground">
            Share this generated password with the user. It will not be shown again.
          </DialogDescription>
        </DialogHeader>
        <div className="flex items-center gap-2 rounded-md border border-border bg-input px-3 py-2 font-mono text-sm text-foreground">
          <span className="flex-1 select-all">{password}</span>
          <CopyButton text={password} />
        </div>
        <div className="flex justify-end pt-1">
          <Button onClick={onClose}>Done</Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export default function UsersPage() {
  const { user: currentUser } = useAuth();
  const queryClient = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);
  const [generatedPassword, setGeneratedPassword] = useState<string | null>(null);

  const isAllowed =
    currentUser?.role === "SuperAdmin" || currentUser?.role === "CpAdmin";

  const { data: users, isLoading, isError } = useQuery({
    queryKey: ["users"],
    queryFn: getUsers,
    enabled: isAllowed,
  });

  const deactivateMutation = useMutation({
    mutationFn: (id: number) => patchUser(id, { isActive: false }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["users"] }),
  });

  if (!isAllowed) {
    return (
      <div className="flex flex-1 items-center justify-center">
        <div className="text-center space-y-2">
          <p className="text-lg font-semibold text-foreground">Access Denied</p>
          <p className="text-sm text-muted-foreground">
            You do not have permission to view this page.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-semibold text-foreground">Users</h2>
          <p className="text-sm text-muted-foreground">Manage system users and roles.</p>
        </div>
        <Button onClick={() => setCreateOpen(true)} className="gap-2">
          <UserPlus className="h-4 w-4" />
          Create User
        </Button>
      </div>

      {isLoading && (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full rounded-md" />
          ))}
        </div>
      )}

      {isError && (
        <p className="text-sm text-red-400">Failed to load users. Please try again.</p>
      )}

      {users && !isLoading && (
        <div className="rounded-lg border border-border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50 text-muted-foreground">
              <tr>
                <th className="px-4 py-3 text-left font-medium">Email</th>
                <th className="px-4 py-3 text-left font-medium">Role</th>
                <th className="px-4 py-3 text-left font-medium">Tenant</th>
                <th className="px-4 py-3 text-left font-medium">Status</th>
                <th className="px-4 py-3 text-left font-medium">Last Login</th>
                <th className="px-4 py-3 text-left font-medium">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {users.map((u) => (
                <tr key={u.id} className="bg-card hover:bg-muted/30">
                  <td className="px-4 py-3 text-foreground font-mono text-xs">{u.email}</td>
                  <td className="px-4 py-3">
                    <Badge variant={roleBadgeVariant(u.role)}>{u.role}</Badge>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground">
                    {u.tenantId ?? <span className="text-xs italic">None</span>}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant={u.isActive ? "default" : "secondary"}>
                      {u.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-muted-foreground text-xs">
                    {u.lastLoginAt
                      ? new Date(u.lastLoginAt).toLocaleString()
                      : <span className="italic">Never</span>}
                  </td>
                  <td className="px-4 py-3">
                    {u.isActive && u.id !== currentUser?.id && (
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-destructive hover:text-destructive text-xs h-7 px-2"
                        disabled={deactivateMutation.isPending}
                        onClick={() => deactivateMutation.mutate(u.id)}
                      >
                        Deactivate
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <CreateUserDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        onCreated={(pw) => {
          queryClient.invalidateQueries({ queryKey: ["users"] });
          setGeneratedPassword(pw);
        }}
      />

      {generatedPassword && (
        <GeneratedPasswordDialog
          password={generatedPassword}
          onClose={() => setGeneratedPassword(null)}
        />
      )}
    </div>
  );
}
