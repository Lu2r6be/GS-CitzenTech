import json
import random
import unittest

import simulador_casas as sim

JOSE = sim.CASAS[0]


class LeituraDaRodadaTests(unittest.TestCase):
    def test_cenario_estavel_fica_perto_da_base(self):
        aleatorio = random.Random(1)
        for rodada in range(50):
            temperatura, _ = sim.leitura_da_rodada(JOSE, "estavel", rodada, aleatorio)
            self.assertAlmostEqual(temperatura, JOSE.temperatura_base, delta=sim.RUIDO_MAXIMO + 0.05)

    def test_onda_de_calor_sobe(self):
        aleatorio = random.Random(1)
        primeira, _ = sim.leitura_da_rodada(JOSE, "onda-de-calor", 0, aleatorio)
        quinta, _ = sim.leitura_da_rodada(JOSE, "onda-de-calor", 5, aleatorio)
        self.assertGreater(quinta, primeira + 2)

    def test_onda_de_frio_desce(self):
        aleatorio = random.Random(1)
        primeira, _ = sim.leitura_da_rodada(JOSE, "onda-de-frio", 0, aleatorio)
        quinta, _ = sim.leitura_da_rodada(JOSE, "onda-de-frio", 5, aleatorio)
        self.assertLess(quinta, primeira - 2)

    def test_variacao_tem_limite(self):
        aleatorio = random.Random(1)
        temperatura, _ = sim.leitura_da_rodada(JOSE, "onda-de-calor", 10_000, aleatorio)
        self.assertLessEqual(temperatura, JOSE.temperatura_base + sim.LIMITE_DA_VARIACAO + sim.RUIDO_MAXIMO + 0.05)

    def test_umidade_sempre_entre_0_e_100(self):
        aleatorio = random.Random(2)
        casa = sim.CasaSimulada("x", "úmida", 25, 100)
        for rodada in range(100):
            _, umidade = sim.leitura_da_rodada(casa, "estavel", rodada, aleatorio)
            self.assertTrue(0 <= umidade <= 100)

    def test_mesma_semente_repete_os_valores(self):
        a = [sim.leitura_da_rodada(JOSE, "estavel", r, random.Random(7)) for r in range(3)]
        b = [sim.leitura_da_rodada(JOSE, "estavel", r, random.Random(7)) for r in range(3)]
        self.assertEqual(a, b)

    def test_cenario_desconhecido_da_erro(self):
        with self.assertRaises(ValueError):
            sim.leitura_da_rodada(JOSE, "terremoto", 0, random.Random(1))


class MensagemTests(unittest.TestCase):
    def test_corpo_tem_o_formato_do_contrato(self):
        corpo = json.loads(sim.montar_corpo("sim-002", 33.4, 61.0))
        self.assertEqual(corpo, {"deviceId": "sim-002", "temperaturaC": 33.4, "umidade": 61.0})

    def test_ids_dos_dispositivos_sao_unicos(self):
        ids = [c.device_id for c in sim.CASAS]
        self.assertEqual(len(ids), len(set(ids)))

    def test_envio_sem_api_nao_derruba_o_simulador(self):
        status, mensagem = sim.enviar("http://127.0.0.1:9", "chave", "sim-002", 30, 50, timeout=1)
        self.assertIsNone(status)
        self.assertIn("sem conexão", mensagem)


class ExecucaoTests(unittest.TestCase):
    def test_executar_faz_todas_as_rodadas_sem_dormir_de_verdade(self):
        pausas = []
        sim.executar("http://127.0.0.1:9", "chave", "estavel", 5, 3, semente=1, dormir=pausas.append)
        self.assertEqual(pausas, [5, 5])  # 3 rodadas = 2 pausas entre elas


if __name__ == "__main__":
    unittest.main()
