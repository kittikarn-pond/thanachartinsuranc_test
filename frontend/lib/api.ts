export const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5000";

/**
 * Calls the REST API. Throws Error(message) with the API's { message },
 * or "NETWORK" / "HTTP_<status>" when there is none (see translateError).
 */
export async function call<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response;
  try {
    res = await fetch(`${API}${path}`, {
      ...init,
      headers: { "Content-Type": "application/json" },
      cache: "no-store",
    });
  } catch {
    throw new Error("NETWORK");
  }
  const body = await res.json().catch(() => null);
  if (!res.ok) throw new Error(body?.message ?? `HTTP_${res.status}`);
  return body as T;
}
