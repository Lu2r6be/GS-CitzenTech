import * as SecureStore from 'expo-secure-store';
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';

import { ClienteApi } from '@/api/cliente';
import type { Papel } from '@/api/tipos';
import { abrirBancoDoCelular } from '@/banco/bancoExpo';
import { prepararBanco } from '@/banco/esquema';
import { CacheLocal, Configuracao, VisitasLocais } from '@/banco/repositorios';
import { criarSincronizador, type ResumoDaSincronizacao } from '@/sincronizacao/sincronizador';
import { Carregando } from '@/componentes/Carregando';
import { EstadoDeErro } from '@/componentes/EstadoDeErro';

export interface Sessao {
  token: string;
  expiraEm: string;
  papel: Papel;
}

interface Contexto {
  sessao: Sessao | null;
  api: ClienteApi;
  visitas: VisitasLocais;
  cache: CacheLocal;
  sincronizar: () => Promise<ResumoDaSincronizacao>;
  servidor: string;
  trocarServidor: (endereco: string) => Promise<void>;
  entrar: (email: string, senha: string) => Promise<void>;
  sair: () => Promise<void>;
}

// O servidor padrão vem do arquivo .env (EXPO_PUBLIC_API_URL). No celular, "localhost" é o próprio
// celular, então para testar de verdade use o endereço do túnel ou o IP do computador.
const SERVIDOR_PADRAO = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';
const CHAVE_DA_SESSAO = 'sessao';
const CHAVE_DO_SERVIDOR = 'servidor';

const AppContext = createContext<Contexto | null>(null);

export function useApp(): Contexto {
  const contexto = useContext(AppContext);
  if (!contexto) throw new Error('useApp precisa estar dentro do AppProvider.');
  return contexto;
}

interface Servicos {
  visitas: VisitasLocais;
  cache: CacheLocal;
  configuracao: Configuracao;
}

export function AppProvider({ children }: { children: ReactNode }) {
  const [servicos, setServicos] = useState<Servicos | null>(null);
  const [sessao, setSessao] = useState<Sessao | null>(null);
  const [servidor, setServidor] = useState(SERVIDOR_PADRAO);
  const [erroDeInicio, setErroDeInicio] = useState<string | null>(null);

  // Refs: o cliente da API lê o valor mais novo sem ser recriado a cada troca de token/servidor.
  const tokenRef = useRef<string | null>(null);
  const servidorRef = useRef(SERVIDOR_PADRAO);
  const sairRef = useRef<() => Promise<void>>(async () => {});

  const api = useMemo(
    () =>
      new ClienteApi({
        baseUrl: () => servidorRef.current,
        token: () => tokenRef.current,
        aoSessaoExpirar: () => {
          void sairRef.current();
        },
      }),
    [],
  );

  useEffect(() => {
    let ativo = true;
    (async () => {
      try {
        const banco = await abrirBancoDoCelular();
        await prepararBanco(banco);
        const configuracao = new Configuracao(banco);
        const guardado = await configuracao.ler(CHAVE_DO_SERVIDOR);
        const guardada = await SecureStore.getItemAsync(CHAVE_DA_SESSAO);
        if (!ativo) return;

        if (guardado) {
          servidorRef.current = guardado;
          setServidor(guardado);
        }
        if (guardada) {
          const lida = JSON.parse(guardada) as Sessao;
          if (new Date(lida.expiraEm).getTime() > Date.now()) {
            tokenRef.current = lida.token;
            setSessao(lida);
          } else {
            await SecureStore.deleteItemAsync(CHAVE_DA_SESSAO);
          }
        }
        setServicos({ visitas: new VisitasLocais(banco), cache: new CacheLocal(banco), configuracao });
      } catch {
        if (ativo) setErroDeInicio('Não foi possível abrir o banco de dados do aplicativo.');
      }
    })();
    return () => {
      ativo = false;
    };
  }, []);

  const sair = useCallback(async () => {
    tokenRef.current = null;
    setSessao(null);
    await SecureStore.deleteItemAsync(CHAVE_DA_SESSAO);
    await servicos?.cache.limpar();
  }, [servicos]);
  sairRef.current = sair;

  const entrar = useCallback(
    async (email: string, senha: string) => {
      const resposta = await api.login(email.trim(), senha);
      const nova: Sessao = { token: resposta.token, expiraEm: resposta.expiraEm, papel: resposta.papel };
      tokenRef.current = nova.token;
      await SecureStore.setItemAsync(CHAVE_DA_SESSAO, JSON.stringify(nova));
      setSessao(nova);
    },
    [api],
  );

  const trocarServidor = useCallback(
    async (endereco: string) => {
      const limpo = endereco.trim().replace(/\/+$/, '');
      servidorRef.current = limpo;
      setServidor(limpo);
      await servicos?.configuracao.gravar(CHAVE_DO_SERVIDOR, limpo);
    },
    [servicos],
  );

  const sincronizar = useMemo(
    () => (servicos ? criarSincronizador(api, servicos.visitas) : async () => semNada),
    [api, servicos],
  );

  if (erroDeInicio) return <EstadoDeErro mensagem={erroDeInicio} />;
  if (!servicos) return <Carregando texto="Abrindo o aplicativo..." />;

  const valor: Contexto = {
    sessao,
    api,
    visitas: servicos.visitas,
    cache: servicos.cache,
    sincronizar,
    servidor,
    trocarServidor,
    entrar,
    sair,
  };
  return <AppContext.Provider value={valor}>{children}</AppContext.Provider>;
}

const semNada: ResumoDaSincronizacao = { enviadas: 0, jaExistiam: 0, rejeitadas: 0, restantes: 0, semRede: false };
