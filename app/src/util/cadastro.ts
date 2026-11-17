import type { CadastroDeResidente, TipoCobertura } from '@/api/tipos';

import { cpfValido, soDigitos } from './cpf';
import { dataBrParaIso } from './datas';

/** O que a pessoa digita na tela de cadastro (tudo texto até validar). */
export interface FormularioDeCadastro {
  nome: string;
  cpf: string;
  dataNascimento: string;
  moraSozinho: boolean;
  temDoencaCronica: boolean;
  condicoesSaude: string;
  telefone: string;
  endereco: string;
  cep: string;
  tipoCobertura: TipoCobertura;
  andar: string;
  consentimento: boolean;
}

export const FORMULARIO_VAZIO: FormularioDeCadastro = {
  nome: '',
  cpf: '',
  dataNascimento: '',
  moraSozinho: false,
  temDoencaCronica: false,
  condicoesSaude: '',
  telefone: '',
  endereco: '',
  cep: '',
  tipoCobertura: 'laje',
  andar: '',
  consentimento: false,
};

export type ErrosDoFormulario = Partial<Record<keyof FormularioDeCadastro, string>>;

/** Confere os campos antes de ir à API. Devolve uma frase por campo com problema. */
export function validarCadastro(f: FormularioDeCadastro, hoje: Date = new Date()): ErrosDoFormulario {
  const erros: ErrosDoFormulario = {};
  if (f.nome.trim().length < 3) erros.nome = 'Escreva o nome completo.';
  if (!cpfValido(f.cpf)) erros.cpf = 'CPF inválido. Confira os números.';
  if (!dataBrParaIso(f.dataNascimento, hoje)) erros.dataNascimento = 'Use uma data real, como 25/03/1948.';
  if (f.endereco.trim().length < 5) erros.endereco = 'Escreva rua e número.';
  if (soDigitos(f.cep).length !== 8) erros.cep = 'O CEP tem 8 números.';
  if (f.telefone.trim() && ![10, 11].includes(soDigitos(f.telefone).length)) {
    erros.telefone = 'Telefone com DDD, 10 ou 11 números.';
  }
  if (f.andar.trim() && !/^\d{1,3}$/.test(f.andar.trim())) erros.andar = 'Use só números.';
  if (!f.consentimento) erros.consentimento = 'Sem o aceite da pessoa não dá para cadastrar.';
  return erros;
}

/** Converte o formulário (já validado) no corpo que a API espera. */
export function formularioParaPedido(f: FormularioDeCadastro, hoje: Date = new Date()): CadastroDeResidente {
  const dataNascimento = dataBrParaIso(f.dataNascimento, hoje);
  if (!dataNascimento) throw new Error('Data de nascimento inválida: valide antes de montar o pedido.');
  return {
    nome: f.nome.trim(),
    cpf: soDigitos(f.cpf),
    dataNascimento,
    moraSozinho: f.moraSozinho,
    temDoencaCronica: f.temDoencaCronica,
    condicoesSaude: f.temDoencaCronica && f.condicoesSaude.trim() ? f.condicoesSaude.trim() : null,
    telefoneContato: f.telefone.trim() ? soDigitos(f.telefone) : null,
    endereco: f.endereco.trim(),
    cep: soDigitos(f.cep),
    tipoCobertura: f.tipoCobertura,
    andar: f.andar.trim() ? Number(f.andar.trim()) : null,
    microareaId: null, // o servidor usa a microárea do ACS logado
    consentimentoConcedido: f.consentimento,
  };
}
