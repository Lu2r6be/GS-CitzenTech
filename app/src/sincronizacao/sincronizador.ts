import { ErroDeRede, type ClienteApi } from '@/api/cliente';
import type { VisitaParaEnviar } from '@/api/tipos';
import type { VisitasLocais } from '@/banco/repositorios';

export interface ResumoDaSincronizacao {
  enviadas: number;
  jaExistiam: number;
  rejeitadas: number;
  /** Quantas continuam no celular esperando sinal. */
  restantes: number;
  semRede: boolean;
}

/** A API aceita até 200 visitas por chamada (CONTRATO §12). */
export const TAMANHO_DO_LOTE = 200;

type ApiDeVisitas = Pick<ClienteApi, 'sincronizarVisitas'>;
type RepositorioDeVisitas = Pick<
  VisitasLocais,
  'pendentes' | 'marcarSincronizada' | 'marcarRejeitada'
>;

/**
 * Manda para a API as visitas guardadas no celular.
 *
 * Pontos que protegem o ACS de perder ou duplicar trabalho:
 *  - cada visita tem um id (GUID) criado no celular; a API ignora o que já recebeu, então
 *    reenviar depois de um erro de rede é seguro;
 *  - só marcamos "sincronizada" quando o servidor confirma aquele id;
 *  - sem rede, nada muda: as visitas continuam pendentes para a próxima tentativa;
 *  - duas sincronizações ao mesmo tempo (foco na tela + botão) viram uma só.
 */
export function criarSincronizador(
  api: ApiDeVisitas,
  visitas: RepositorioDeVisitas,
  agora: () => Date = () => new Date(),
): () => Promise<ResumoDaSincronizacao> {
  let emAndamento: Promise<ResumoDaSincronizacao> | null = null;

  async function rodar(): Promise<ResumoDaSincronizacao> {
    const resumo: ResumoDaSincronizacao = { enviadas: 0, jaExistiam: 0, rejeitadas: 0, restantes: 0, semRede: false };
    const pendentes = await visitas.pendentes();

    for (let inicio = 0; inicio < pendentes.length; inicio += TAMANHO_DO_LOTE) {
      const lote = pendentes.slice(inicio, inicio + TAMANHO_DO_LOTE);
      const corpo: VisitaParaEnviar[] = lote.map((v) => ({
        id: v.id,
        domicilioId: v.domicilioId,
        realizadaEm: v.realizadaEm,
        resultado: v.resultado,
        observacao: v.observacao,
      }));

      try {
        const resposta = await api.sincronizarVisitas(corpo);
        for (const item of resposta.visitas) {
          if (item.status === 'rejeitada') {
            await visitas.marcarRejeitada(item.id, item.motivo ?? 'O servidor não aceitou esta visita.');
            resumo.rejeitadas += 1;
          } else {
            await visitas.marcarSincronizada(item.id, agora());
            if (item.status === 'criada') resumo.enviadas += 1;
            else resumo.jaExistiam += 1;
          }
        }
      } catch (erro) {
        if (erro instanceof ErroDeRede) {
          resumo.semRede = true;
          break;
        }
        throw erro; // sessão expirada ou erro do servidor: quem chamou decide o que mostrar
      }
    }

    resumo.restantes = (await visitas.pendentes()).length;
    return resumo;
  }

  return () => {
    emAndamento ??= rodar().finally(() => {
      emAndamento = null;
    });
    return emAndamento;
  };
}
