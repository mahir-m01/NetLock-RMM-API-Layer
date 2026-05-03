"use client";

import Link from "next/link";
import type { Device } from "@/lib/types";

export function getDeviceNetbirdIp(device: Pick<Device, "netbirdIp" | "netBirdIp">): string | null {
  return device.netbirdIp ?? device.netBirdIp ?? null;
}

export function mergeDeviceNetbirdFields(device: Device, sourceDevices: Device[] | undefined): Device {
  const source = sourceDevices?.find((candidate) => candidate.id === device.id);
  if (!source) return device;

  return {
    ...device,
    netbirdIp: getDeviceNetbirdIp(device) ?? getDeviceNetbirdIp(source),
    netBirdIp: device.netBirdIp ?? source.netBirdIp,
    netbirdPeerId: device.netbirdPeerId ?? source.netbirdPeerId ?? source.netBirdPeerId,
    netBirdPeerId: device.netBirdPeerId ?? source.netBirdPeerId,
  };
}

export function NetbirdStatus({
  device,
  showNetworkLink = false,
}: {
  device: Pick<Device, "netbirdIp" | "netBirdIp">;
  showNetworkLink?: boolean;
}) {
  const ip = getDeviceNetbirdIp(device);

  if (ip) {
    return <span className="font-mono text-xs text-blue-400">{ip}</span>;
  }

  return (
    <span className="text-xs text-muted-foreground">
      NetBird not linked
      {showNetworkLink && (
        <>
          {" "}
          <Link href="/network" className="text-blue-400 hover:text-blue-300">
            Manage
          </Link>
        </>
      )}
    </span>
  );
}
