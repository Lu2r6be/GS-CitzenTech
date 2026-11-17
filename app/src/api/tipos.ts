// Formatos que a API devolve (ver docs/CONTRATO.md, seções 5 e 12).
// Os nomes dos campos são exatamente os do JSON, para ninguém precisar "traduzir" nada.

export type Nivel = 'azul' | 'verde' | 'amarelo' | 'vermelho' | 'cinza';
export type Papel = 'Acs' | 'Familiar' | 'Admin';
export type DiaDeRisco = 'nenhum' | 'calor' | 'frio';
export type Tendencia = 'subindo' | 'estavel' | 'descendo';
export type TipoCobertura = 'laje' | 'telha' | 'fibrocimento' | 'outro';
export type ResultadoVisita = 'tudoBem' | 'orientado' | 'encaminhadoUbs' | 'naoEncontrado';

export interface LoginResposta {
  token: string;
  expiraEm: string;
  papel: Papel;
}

export interface CasaNaFila {
  domicilioId: string;
  endereco: string;
  nomeResidente: string;
  nivel: Nivel;
  semSinal: boolean;
  temperaturaC: number | null;
  ultimaLeituraEm: string | null;
  pontosDeVulnerabilidade: number;
  ultimaVisitaEm: string | null;
}

export interface FilaDeVisitas {
  diaDeRisco: DiaDeRisco;
  casas: CasaNaFila[];
}

export interface LeituraResumo {
  recebidaEm: string;
  temperaturaC: number;
  umidade: number;
  indiceCalorC: number;
  nivel: Nivel;
}

export interface DetalheDaCasa {
  id: string;
  endereco: string;
  nomeResidente: string;
  cpfMascarado: string;
  tipoCobertura: TipoCobertura;
  nivel: Nivel;
  semSinal: boolean;
  ultimaLeituraEm: string | null;
  tendencia: Tendencia;
  ultimasLeituras: LeituraResumo[];
}

export interface CasaDoFamiliar {
  nivel: Nivel;
  semSinal: boolean;
  mensagem: string;
  temperaturaC: number | null;
  ultimaLeituraEm: string | null;
}

/** Uma visita feita offline, no formato que a API espera em POST /visitas/sincronizar. */
export interface VisitaParaEnviar {
  id: string;
  domicilioId: string;
  realizadaEm: string;
  resultado: ResultadoVisita;
  observacao?: string | null;
}

export type StatusDaSincronizacao = 'criada' | 'jaExistia' | 'rejeitada';

export interface ResultadoDaVisita {
  id: string;
  status: StatusDaSincronizacao;
  motivo?: string | null;
}

export interface ResultadoDaSincronizacao {
  visitas: ResultadoDaVisita[];
}

export interface CadastroDeResidente {
  nome: string;
  cpf: string;
  dataNascimento: string; // AAAA-MM-DD
  moraSozinho: boolean;
  temDoencaCronica: boolean;
  condicoesSaude: string | null;
  telefoneContato: string | null;
  endereco: string;
  cep: string;
  tipoCobertura: TipoCobertura;
  andar: number | null;
  microareaId: string | null;
  consentimentoConcedido: boolean;
}

export interface ResidenteCadastrado {
  residenteId: string;
  domicilioId: string;
  cpfMascarado: string;
}
