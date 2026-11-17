import { Redirect } from 'expo-router';

import { useApp } from '@/contexto/AppContexto';

/** Porta de entrada: manda cada pessoa para a tela do seu papel. */
export default function Inicio() {
  const { sessao } = useApp();
  if (!sessao) return <Redirect href="/login" />;
  if (sessao.papel === 'Acs') return <Redirect href="/fila" />;
  if (sessao.papel === 'Familiar') return <Redirect href="/familiar" />;
  return <Redirect href="/sem-acesso" />;
}
