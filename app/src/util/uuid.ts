import { randomUUID } from 'expo-crypto';

/** Id da visita, criado no celular. É ele que impede a visita de ser contada duas vezes. */
export function novoId(): string {
  return randomUUID();
}
